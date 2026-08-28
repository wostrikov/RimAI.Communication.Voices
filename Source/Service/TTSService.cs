using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Voices.Data;
using Ustas.RimAI.Communication.Voices.Policy;
using RimTalkPatches = Ustas.RimAI.Communication.Voices.Patch.RimTalkPatches;
using Verse;
using RimAI.Core.Runtime;

namespace Ustas.RimAI.Communication.Voices.Service
{
    /// <summary>
    /// Coordinates Text-to-Speech generation for dialogue.
    /// Each request has its own CancellationTokenSource for independent cancellation.
    /// </summary>
    public static class TTSService
    {
        private static int _lastGenerateTimeStampMilisecond = 0;
        private static int _waitingRequestCount = 0;
        private static readonly object _waitingRequestLock = new object();
        private static volatile bool _isShuttingDown = false;
        private static Provider.ITTSProvider _provider = new Provider.NoneProvider();

        private static readonly object _providerLock = new object();

        public static void SetProvider(TTSSettings.TTSSupplier supplier, TTSSettings settings = null)
        {
            lock (_providerLock)
            {
                // Shutdown current provider
                ShutdownCurrentProvider();

                // Reset module runtime state when switching providers
                ResetRuntimeState();

                // Create new provider
                _provider = CreateProvider(supplier, settings);
                
                Log.Message($"[RimAI.Voices] TTS provider set to {supplier}");
            }
        }

        private static void ShutdownCurrentProvider()
        {
            try
            {
                _provider?.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Voices] Error shutting down provider: {ex.Message}");
            }
        }

        private static void ResetRuntimeState()
        {
            try
            {
                StopAll(false);
                TtsAudioCache.Clear();
                _lastGenerateTimeStampMilisecond = 0;
                lock (_waitingRequestLock)
                {
                    _waitingRequestCount = 0;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Voices] Error resetting runtime state: {ex.Message}");
            }
        }

        private static Provider.ITTSProvider CreateProvider(TTSSettings.TTSSupplier supplier, TTSSettings settings)
        {
            switch (supplier)
            {
                case TTSSettings.TTSSupplier.FishAudio:
                    return new Provider.FishAudioProvider();
                case TTSSettings.TTSSupplier.CosyVoice:
                    return new Provider.CosyVoiceProvider();
                case TTSSettings.TTSSupplier.IndexTTS:
                    return new Provider.IndexTTSProvider();
                case TTSSettings.TTSSupplier.AzureTTS:
                    var azureProvider = new Provider.AzureTTSProvider();
                    if (settings != null)
                    {
                        string region = settings.GetSupplierRegion(supplier);
                        azureProvider.SetRegion(region);
                    }
                    return azureProvider;
                case TTSSettings.TTSSupplier.EdgeTTS:
                    return new Provider.EdgeTTSProvider();
                case TTSSettings.TTSSupplier.GeminiTTS:
                    return new Provider.GeminiTTSProvider();
                case TTSSettings.TTSSupplier.OpenAI:
                    var openAiProvider = new Provider.OpenAITTSProvider();
                    // SupplierRegion doubles as the base URL slot for OpenAI-compatible endpoints.
                    openAiProvider.SetBaseUrl(settings?.GetSupplierRegion(supplier));
                    return openAiProvider;
                case TTSSettings.TTSSupplier.TTSWebUI:
                    var ttsWebUIProvider = new Provider.TTSWebUIProvider();
                    if (settings != null)
                    {
                        // Use SupplierRegion to store the base URL for TTSWebUI
                        string baseUrl = settings.GetSupplierRegion(supplier);
                        if (!string.IsNullOrWhiteSpace(baseUrl))
                        {
                            ttsWebUIProvider.SetBaseUrl(baseUrl);
                        }
                    }
                    return ttsWebUIProvider;
                case TTSSettings.TTSSupplier.None:
                default:
                    return new Provider.NoneProvider();
            }
        }

        /// <summary>
        /// Initiate TTS generation for a dialogue. Runs asynchronously.
        /// </summary>
        public static void ProcessDialogue(string text, Pawn pawn, Guid dialogueId, TTSSettings settings)
        {
            // The voice is resolved here, on the caller's thread, because it inspects the
            // pawn. Everything after this point works from plain values.
            Voice.ResolvedPawnVoice voice = null;
            try
            {
                voice = Voice.PawnVoiceRenderer.Resolve(pawn, settings);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Voices] Failed to resolve voice for '{pawn?.LabelShort}': {ex.Message}");
            }

            // Perform early validation checks
            if (!ValidateDialogueRequest(text, voice, dialogueId, settings, out string reason))
            {
                Log.Message($"[RimAI.Voices] Rejected - {reason}");
                CleanupAndRelease(dialogueId);
                return;
            }
            
            // Start async generation
            RimAiBackground.Run(async () => 
            {
                await ProcessDialogueAsync(text, pawn, dialogueId, settings, voice);
            });
        }

        /// <summary>
        /// Validate if a dialogue request should be processed
        /// </summary>
        private static bool ValidateDialogueRequest(string text, Voice.ResolvedPawnVoice voice, Guid dialogueId, TTSSettings settings, out string reason)
        {
            // Early exit: shutting down
            if (_isShuttingDown)
            {
                reason = "Shutting down";
                return false;
            }

            TtsProviderKind preferred = MapSupplier(settings.Supplier);
            bool preferredKey = !string.IsNullOrWhiteSpace(GetApiKeyForSupplier(settings.Supplier, settings));
            var chain = TtsProviderChain.Build(preferred, preferredKey);
            bool anyUsable = false;
            for (int i = 0; i < chain.Count; i++)
            {
                var slot = chain[i];
                if (slot.Kind == TtsProviderKind.None)
                    continue;
                if (!slot.RequiresCredential || slot.CredentialPresent)
                {
                    anyUsable = true;
                    break;
                }
            }
            if (!anyUsable)
            {
                reason = $"No usable TTS provider in chain for {settings.Supplier}";
                return false;
            }

            // Early exit: empty text
            if (string.IsNullOrEmpty(text))
            {
                reason = "Empty text";
                return false;
            }

            // Early exit: the pawn is muted or no voice could be rendered
            if (voice == null || voice.Silent || string.IsNullOrEmpty(voice.VoiceId))
            {
                reason = "No voice assigned for this speaker";
                return false;
            }

            // Check if dialogue was cancelled
            if (RimTalkPatches.IsTalkIgnored(dialogueId))
            {
                reason = $"Dialogue {dialogueId} was ignored";
                return false;
            }

            // Check if TTS Module is active
            if (!IsModuleActiveAndEnabled(settings))
            {
                reason = "TTS module off";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// Check if dialogue should continue processing (used during async operations)
        /// </summary>
        private static bool ShouldContinueProcessing(Guid dialogueId, TTSSettings settings, out string reason)
        {
            if (RimTalkPatches.IsTalkIgnored(dialogueId))
            {
                reason = "Dialogue was ignored during generation";
                return false;
            }

            if (!IsModuleActiveAndEnabled(settings))
            {
                reason = "TTS module turned off during generation";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// Async TTS generation pipeline
        /// </summary>
        private static async Task ProcessDialogueAsync(string text, Pawn pawn, Guid dialogueId, TTSSettings settings, Voice.ResolvedPawnVoice voice)
        {
            try
            {
                // Process and translate text (using pawn-specific language if set)
                string finalInputText = await ProcessTextAsync(text, pawn, dialogueId, settings);
                if (finalInputText == null)
                {
                    CleanupAndRelease(dialogueId);
                    return;
                }

                string finalInstructText = null;

                // Check if should continue after preprocessing
                if (!ShouldContinueProcessing(dialogueId, settings, out string reason))
                {
                    Log.Message($"[RimAI.Voices] {reason} (discarding audio)");
                    CleanupAndRelease(dialogueId);
                    return;
                }

                // A repeated line from the same rendered voice does not need a new request.
                string cacheKey = Ustas.RimAI.Core.Voices.VoiceCacheKey.Compute(voice.Signature, finalInputText);
                if (TtsAudioCache.TryGet(cacheKey, out byte[] cachedAudio))
                {
                    HandleGenerationResult(dialogueId, cachedAudio, settings);
                    return;
                }

                // Apply cooldown
                await ApplyCooldownAsync(settings);
                
                // Generate speech
                byte[] audioData = await GenerateSpeechAsync(voice, finalInputText, finalInstructText, settings);
                TtsAudioCache.Store(cacheKey, audioData);

                // Final validation and playback setup
                HandleGenerationResult(dialogueId, audioData, settings);
            }
            catch (OperationCanceledException)
            {
                Log.Message($"[RimAI.Voices] Dialogue {dialogueId} generation cancelled");
                CleanupAndRelease(dialogueId);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Voices] Exception - {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                CleanupAndRelease(dialogueId);
            }
        }

        /// <summary>
        /// Process and translate text if needed
        /// </summary>
        private static async Task<string> ProcessTextAsync(string text, Pawn pawn, Guid dialogueId, TTSSettings settings)
        {
            if (!settings.EnableTextPreprocessing)
                return text;

            string language = VoiceSharedAiText.Language;
            var preProcessResult = await InputPreProcessService.PreProcessAsync(text, language, settings);

            if (preProcessResult != null && !string.IsNullOrEmpty(preProcessResult.Text))
                return preProcessResult.Text;

            Log.Warning("[RimAI.Voices] Translation/PreProcess returned empty result");
            return null;
        }

        /// <summary>
        /// Apply cooldown between requests
        /// </summary>
        private static async Task ApplyCooldownAsync(TTSSettings settings)
        {
            lock (_waitingRequestLock)
            {
                _waitingRequestCount++;
            }

            int nowMilisecond = (int)TTSMod.AppStopwatch.Elapsed.TotalMilliseconds;
            int cooldownMilisecond = settings.GetSupplierGenerateCooldown(settings.Supplier);
            int cooldownEndMilisecond = _waitingRequestCount * cooldownMilisecond + _lastGenerateTimeStampMilisecond;

            if (nowMilisecond < cooldownEndMilisecond)
            {
                await Task.Delay(cooldownEndMilisecond - nowMilisecond);
            }

            lock (_waitingRequestLock)
            {
                _lastGenerateTimeStampMilisecond = (int)TTSMod.AppStopwatch.Elapsed.TotalMilliseconds;
                _waitingRequestCount--;
            }
        }

        /// <summary>
        /// Generate speech using configured provider
        /// </summary>
        private static async Task<byte[]> GenerateSpeechAsync(Voice.ResolvedPawnVoice voice, string inputText, string instructText, TTSSettings settings)
        {
            TtsProviderKind preferred = MapSupplier(settings.Supplier);
            bool preferredKey = !string.IsNullOrWhiteSpace(GetApiKeyForSupplier(settings.Supplier, settings));
            var chain = TtsProviderChain.Build(preferred, preferredKey);
            TtsProviderOutcome outcome = await TtsProviderOrchestrator.ExecuteAsync(
                chain,
                slot => AttemptSlotAsync(slot, voice, inputText, instructText, settings));
            if (outcome.Class != TtsFailureClass.Success)
                return null;
            return outcome.Audio;
        }

        static async Task<TtsSlotResult> AttemptSlotAsync(
            TtsProviderSlot slot,
            Voice.ResolvedPawnVoice voice,
            string inputText,
            string instructText,
            TTSSettings settings)
        {
            TTSSettings.TTSSupplier supplier = UnmapSupplier(slot.Kind);
            var provider = CreateProvider(supplier, settings);
            var ttsRequest = new Service.TTSRequest
            {
                ApiKey = GetApiKeyForSupplier(supplier, settings),
                Model = voice.Model,
                Input = inputText,
                InstructText = instructText,
                Instructions = voice.Instructions,
                ResponseFormat = settings.GetSupplierResponseFormat(supplier),
                Voice = voice.VoiceId,
                Speed = voice.Speed,
                Pitch = voice.Pitch,
                Locale = voice.Locale,
                Volume = settings.GetSupplierVolume(supplier),
                Temperature = settings.GetSupplierTemperature(supplier),
                TopP = settings.GetSupplierTopP(supplier)
            };

            try
            {
                byte[] audio = await provider.GenerateSpeechAsync(ttsRequest).ConfigureAwait(false);
                if (audio == null || audio.Length == 0)
                    return new TtsSlotResult { Class = TtsFailureClass.Transient };
                return new TtsSlotResult { Class = TtsFailureClass.Success, Audio = audio };
            }
            catch (OperationCanceledException)
            {
                return new TtsSlotResult { Class = TtsFailureClass.Cancelled };
            }
            catch (TimeoutException)
            {
                return new TtsSlotResult { Class = TtsFailureClass.Transient };
            }
            catch (System.Net.Http.HttpRequestException)
            {
                return new TtsSlotResult { Class = TtsFailureClass.Transient };
            }
        }

        public static TtsProviderKind MapSupplier(TTSSettings.TTSSupplier supplier)
        {
            switch (supplier)
            {
                case TTSSettings.TTSSupplier.EdgeTTS: return TtsProviderKind.EdgeTts;
                case TTSSettings.TTSSupplier.OpenAI: return TtsProviderKind.OpenAi;
                case TTSSettings.TTSSupplier.AzureTTS: return TtsProviderKind.Azure;
                case TTSSettings.TTSSupplier.GeminiTTS: return TtsProviderKind.Gemini;
                case TTSSettings.TTSSupplier.FishAudio: return TtsProviderKind.FishAudio;
                case TTSSettings.TTSSupplier.CosyVoice: return TtsProviderKind.CosyVoice;
                case TTSSettings.TTSSupplier.IndexTTS: return TtsProviderKind.IndexTts;
                case TTSSettings.TTSSupplier.TTSWebUI: return TtsProviderKind.TtsWebUi;
                default: return TtsProviderKind.None;
            }
        }

        static TTSSettings.TTSSupplier UnmapSupplier(TtsProviderKind kind)
        {
            switch (kind)
            {
                case TtsProviderKind.EdgeTts: return TTSSettings.TTSSupplier.EdgeTTS;
                case TtsProviderKind.OpenAi: return TTSSettings.TTSSupplier.OpenAI;
                case TtsProviderKind.Azure: return TTSSettings.TTSSupplier.AzureTTS;
                case TtsProviderKind.Gemini: return TTSSettings.TTSSupplier.GeminiTTS;
                case TtsProviderKind.FishAudio: return TTSSettings.TTSSupplier.FishAudio;
                case TtsProviderKind.CosyVoice: return TTSSettings.TTSSupplier.CosyVoice;
                case TtsProviderKind.IndexTts: return TTSSettings.TTSSupplier.IndexTTS;
                case TtsProviderKind.TtsWebUi: return TTSSettings.TTSSupplier.TTSWebUI;
                default: return TTSSettings.TTSSupplier.None;
            }
        }

        /// <summary>
        /// Handle the result of TTS generation
        /// </summary>
        private static void HandleGenerationResult(Guid dialogueId, byte[] audioData, TTSSettings settings)
        {
            // Check if should continue
            if (!ShouldContinueProcessing(dialogueId, settings, out string reason))
            {
                Log.Message($"[RimAI.Voices] {reason} (discarding audio)");
                CleanupAndRelease(dialogueId);
                return;
            }

            if (audioData != null && audioData.Length > 0)
            {
                if (!RimTalkPatches.IsBlocked(dialogueId))
                {
                    Log.Message($"[RimAI.Voices] Dialogue {dialogueId} is no longer blocked after generation (discarding audio)");
                    CleanupFailedDialogue(dialogueId);
                }
                else
                {
                    AudioPlaybackService.SetAudioResult(dialogueId, audioData);
                }
                RimTalkPatches.ReleaseBlock(dialogueId);
            }
            else
            {
                Log.Warning("[RimAI.Voices] Failed - API returned no audio data");
                CleanupAndRelease(dialogueId);
            }
        }

        private static void CleanupFailedDialogue(Guid dialogueId)
        {
            if (dialogueId != Guid.Empty)
            {
                AudioPlaybackService.SetAudioResult(dialogueId, null);
            }
        }

        // Merge common cleanup + release pattern into one helper to simplify call sites
        private static void CleanupAndRelease(Guid dialogueId)
        {
            CleanupFailedDialogue(dialogueId);
            RimTalkPatches.ReleaseBlock(dialogueId);
        }

        private static bool IsModuleActiveAndEnabled(TTSSettings settings)
        {
            return TTSConfig.IsEnabled && settings != null && settings.isOnButton;
        }

        /// <summary>
        /// Effective credential for a supplier. OpenAI voicing owns its own credential
        /// domain (OPENAI_RIMAI_TTS) and never reuses the gameplay text credential.
        /// </summary>
        private static string GetApiKeyForSupplier(TTSSettings.TTSSupplier supplier, TTSSettings settings)
        {
            if (supplier == TTSSettings.TTSSupplier.OpenAI)
            {
                string fromEnvironment = Data.OpenAITtsCredential.Resolve();
                if (!string.IsNullOrWhiteSpace(fromEnvironment))
                    return fromEnvironment;
            }

            if (settings == null) return string.Empty;

            // Prefer SupplierApiKeys dictionary if present
            return settings.GetSupplierApiKey(supplier);
        }

        public static void StopAll(bool permanentShutdown = false, bool touchUnityAudio = true)
        {
            if (permanentShutdown)
            {
                _isShuttingDown = true;
                try
                {
                    _provider?.Shutdown();
                }
                catch { }
            }

            List<Guid> toCancel;
            lock (RimTalkPatches.blockedDialogues)
            {
                toCancel = RimTalkPatches.blockedDialogues.ToList();
            }
            
            // Cancel all pending TTS generation tasks
            foreach (var id in toCancel)
            {
                CancelDialogue(id);
            }
            
            lock (RimTalkPatches.blockedDialogues)
            {
                RimTalkPatches.blockedDialogues.Clear();
            }
            
            // Application.quitting runs after Unity has started native teardown.
            // Reading AudioSource.isPlaying there can access an already released
            // native object and crash outside the managed exception boundary.
            // Save/load cleanup still stops the live AudioSource normally; exit
            // cleanup is managed-only and lets process teardown own native audio.
            if (touchUnityAudio)
            {
                AudioPlaybackService.StopAndClear();
            }
        }

        public static void CancelDialogue(Guid dialogueId)
        {
            if (dialogueId == Guid.Empty) return;
            
            if (RimTalkPatches.IsBlocked(dialogueId))
            {
                CleanupAndRelease(dialogueId);
            }
            else
            {
                AudioPlaybackService.RemovePendingAudio(dialogueId);
            }
        }

        public static void ReloadMap(Map map)
        {
            if (map == null)
            {
                return;
            }

            try
            {
                int pawnCount = 0;
                try
                {
                    pawnCount = map.mapPawns.AllPawns.Count;
                }
                catch (Exception exCount)
                {
                    Log.Warning($"[RimAI.Voices] ReloadMap: failed to get pawn count for map '{map}': {exCount}");
                }

                foreach (var pawn in map.mapPawns.AllPawns)
                {
                    try
                    {
                        RimTalkPatches.AddPawnDialogueList(pawn);
                    }
                    catch (Exception exPawn)
                    {
                        try
                        {
                            var pawnId = pawn?.thingIDNumber.ToString() ?? "<null>";
                            var pawnName = pawn?.LabelShort ?? pawn?.Name?.ToString() ?? "<unnamed>";
                            Log.Error($"[RimAI.Voices] ReloadMap: AddPawnDialogueList failed for pawn '{pawnName}' (id={pawnId}): {exPawn}");
                        }
                        catch (Exception exInner)
                        {
                            // Best effort logging; avoid throwing from logger
                            Log.Error($"[RimAI.Voices] ReloadMap: failed to log pawn exception: {exInner}");
                        }
                    }
                }
}
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Voices] ReloadMap: Unexpected error iterating pawns on map '{map?.ToString() ?? "<null>"}': {ex}");
            }
        }
    }
}
