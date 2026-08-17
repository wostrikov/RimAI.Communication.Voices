using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using Ustas.RimAI.Communication.Voices.Data;
using Ustas.RimAI.Communication.Voices.Service;
using Ustas.RimAI.Communication.Voices.Patch;

namespace Ustas.RimAI.Communication.Voices.UI
{
    /// <summary>
    /// TTS settings UI renderer
    /// </summary>
    public static class SettingsUI
    {
        // Thread-safe queue for messages coming from background tasks
        private static System.Collections.Concurrent.ConcurrentQueue<(string text, MessageTypeDef type)> pendingMessages = new System.Collections.Concurrent.ConcurrentQueue<(string, MessageTypeDef)>();

        private static void EnqueueMessage(string text, MessageTypeDef type)
        {
            pendingMessages.Enqueue((text, type));
        }

        private static Vector2 scrollPosition = Vector2.zero;
        private static Vector2 mainScrollPosition = Vector2.zero;
        private static string processingPromptBuffer = "";
        private static bool processingPromptInitialized = false;
        // Buffers for upload UI
        private static string uploadPathBuffer = "";
        private static string uploadNameBuffer = "";
        private static string uploadTextBuffer = "";
        // Queue for actions that must run on the main thread (e.g. UI updates)
        private static System.Collections.Concurrent.ConcurrentQueue<System.Action> pendingActions = new System.Collections.Concurrent.ConcurrentQueue<System.Action>();

        private static void EnqueueMainThreadAction(System.Action a)
        {
            if (a == null) return;
            pendingActions.Enqueue(a);
        }

        public static void DrawTTSSettings(Rect inRect, TTSSettings settings)
        {
            // First, run any actions enqueued by background tasks that must execute on the main thread
            while (pendingActions.TryDequeue(out var act))
            {
                act?.Invoke();
            }

            // Flush any messages enqueued by background tasks on the main thread
            while (pendingMessages.TryDequeue(out var _m))
            {
                Messages.Message(_m.text, _m.type, false);
            }

            // Calculate content height dynamically based on selected supplier's voice model count
            float baseHeight = 2000f; // base for other sections
            float voiceModelRowHeight = 40f; // Height per voice model row (30f + 6f gap + padding)
            var supplierVoiceModels = settings.GetSupplierVoiceModels(settings.Supplier);
            int voiceModelCount = supplierVoiceModels?.Count ?? 0;
            // If supplier supports SiliconFlow uploads, include upload UI height estimate
            float uploadSectionHeight = (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS) ? 280f : 0f;
            // ResetModels button height (CosyVoice, IndexTTS, AzureTTS, EdgeTTS)
            float resetButtonHeight = (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS || settings.Supplier == TTSSettings.TTSSupplier.AzureTTS || settings.Supplier == TTSSettings.TTSSupplier.EdgeTTS) ? 40f : 0f;
            // OpenAI adds endpoint, model, format and instruction controls
            float openAiSectionHeight = settings.Supplier == TTSSettings.TTSSupplier.OpenAI ? 400f : 0f;
            // Rule list is only expanded on demand
            float rulesSectionHeight = showRulesList ? 60f : 0f;
            // Processing prompt area height (text area)
            float contentHeight = baseHeight + (voiceModelCount * voiceModelRowHeight) + uploadSectionHeight + resetButtonHeight + openAiSectionHeight + rulesSectionHeight;
            bool isOn = settings.EnableTTS;
            
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);

            Widgets.BeginScrollView(inRect, ref mainScrollPosition, viewRect);
            
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            // Enable TTS
            listing.CheckboxLabeled("Ustas.RimAI.Communication.Settings.TTS.Enable".Translate(), ref settings.EnableTTS, "Ustas.RimAI.Communication.Settings.TTS.EnableTooltip".Translate());

            // Handle TTS toggle
            if (isOn != settings.EnableTTS)
            {
                if (!settings.EnableTTS)
                {
                    // TTS turned OFF: stop all audio and clear state
                    AudioPlaybackService.StopAndClear();
                    Log.Message("[RimAI.Voices] TTS disabled via settings");
                    listing.End();
                    Widgets.EndScrollView();
                    return;
                }
                else
                {
                    // TTS turned ON: reload map to register all pawns
                    if (Find.CurrentMap != null)
                    {
                        TTSService.ReloadMap(Find.CurrentMap);
                        Log.Message("[RimAI.Voices] TTS enabled via settings, reloading map pawns");
                    }
                }
            }

            listing.Gap();

            listing.CheckboxLabeled("Ustas.RimAI.Communication.Settings.TTS.ButtonEnable".Translate(), ref settings.ButtonDisplay, "Ustas.RimAI.Communication.Settings.TTS.ButtonEnableTooltip".Translate());

            listing.Gap();

            DrawApiConfigSection(listing, settings);

            listing.Gap();

            DrawProcessingPromptSection(listing, settings);

            listing.Gap();

            // Supplier selection (TTS backend)
            Text.Font = GameFont.Medium;
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.TTSConfig".Translate());
            Text.Font = GameFont.Small;

            listing.Label("Ustas.RimAI.Communication.Settings.TTS.TTSSupplier".Translate());
            Rect supplierRect = listing.GetRect(Text.LineHeight);
            string supplierDisplay = SupplierString(settings.Supplier);

            if (Widgets.ButtonText(supplierRect, supplierDisplay))
            {
                var supplierOrder = new[]
                {
                    TTSSettings.TTSSupplier.OpenAI,
                    TTSSettings.TTSSupplier.FishAudio,
                    TTSSettings.TTSSupplier.CosyVoice,
                    TTSSettings.TTSSupplier.IndexTTS,
                    TTSSettings.TTSSupplier.AzureTTS,
                    TTSSettings.TTSSupplier.EdgeTTS,
                    TTSSettings.TTSSupplier.GeminiTTS,
                    TTSSettings.TTSSupplier.TTSWebUI,
                    TTSSettings.TTSSupplier.None
                };

                var options = new System.Collections.Generic.List<FloatMenuOption>();
                foreach (var candidate in supplierOrder)
                {
                    var selected = candidate;
                    options.Add(new FloatMenuOption(SupplierString(selected), delegate
                    {
                        SelectSupplier(settings, selected);
                    }));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap();

            // Per-supplier API key and model configuration
            if (settings.Supplier != TTSSettings.TTSSupplier.None)
            {
                // OpenAI voicing takes its credential from the environment, not from a text field
                if (settings.Supplier == TTSSettings.TTSSupplier.OpenAI)
                {
                    DrawOpenAiCredential(listing);
                    listing.Gap();
                }
                // EdgeTTS doesn't need API key - skip it
                else if (settings.Supplier != TTSSettings.TTSSupplier.EdgeTTS)
                {
                    listing.Label("Ustas.RimAI.Communication.Settings.TTS.ApiKey".Translate());
                    string currentApiKey = settings.GetSupplierApiKey(settings.Supplier);
                    string newApiKey = GUI.PasswordField(listing.GetRect(30f), currentApiKey ?? "", '•');
                    if (newApiKey != currentApiKey)
                    {
                        settings.SetSupplierApiKey(settings.Supplier, newApiKey);
                    }

                    listing.Gap();
                }

                // TTS Model Selection (example: FishAudio choices)
                if (settings.Supplier == TTSSettings.TTSSupplier.FishAudio)
                {
                    string currentModel = settings.GetSupplierModel(settings.Supplier);
                    listing.Label("Ustas.RimAI.Communication.Settings.TTS.ModelLabel".Translate(currentModel));
                    if (listing.RadioButton("Ustas.RimAI.Communication.Settings.TTS.ModelHighQuality".Translate(), currentModel == "fishaudio-1"))
                    {
                        settings.SetSupplierModel(settings.Supplier, "fishaudio-1");
                    }
                    if (listing.RadioButton("Ustas.RimAI.Communication.Settings.TTS.ModelFaster".Translate(), currentModel == "s1"))
                    {
                        settings.SetSupplierModel(settings.Supplier, "s1");
                    }
                }

                // CosyVoice model selection
                if (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice)
                {
                    string currentModel = settings.GetSupplierModel(settings.Supplier);
                    listing.Label("Ustas.RimAI.Communication.Settings.TTS.ModelLabel.CosyVoice".Translate(currentModel ?? "(not set)"));
                    if (listing.RadioButton("FunAudioLLM/CosyVoice2-0.5B", currentModel == "FunAudioLLM/CosyVoice2-0.5B"))
                    {
                        settings.SetSupplierModel(settings.Supplier, "FunAudioLLM/CosyVoice2-0.5B");
                    }
                    listing.Gap(6f);
                    listing.Label("Ustas.RimAI.Communication.Settings.TTS.CustomModelIdLabel".Translate());
                    string customModelCosy = listing.TextEntry(currentModel ?? "");
                    if (customModelCosy != currentModel)
                    {
                        settings.SetSupplierModel(settings.Supplier, customModelCosy);
                    }
                }

                // IndexTTS model selection
                if (settings.Supplier == TTSSettings.TTSSupplier.IndexTTS)
                {
                    string currentModel = settings.GetSupplierModel(settings.Supplier);
                    listing.Label("Ustas.RimAI.Communication.Settings.TTS.ModelLabel.IndexTTS".Translate(currentModel ?? "(not set)"));
                    if (listing.RadioButton("IndexTeam/IndexTTS-2", currentModel == "IndexTeam/IndexTTS-2"))
                    {
                        settings.SetSupplierModel(settings.Supplier, "IndexTeam/IndexTTS-2");
                    }
                    listing.Gap(6f);
                    listing.Label("Ustas.RimAI.Communication.Settings.TTS.CustomModelIdLabel".Translate());
                    string customModelIndex = listing.TextEntry(currentModel ?? "");
                    if (customModelIndex != currentModel)
                    {
                        settings.SetSupplierModel(settings.Supplier, customModelIndex);
                    }
                }

                // AzureTTS region configuration
                if (settings.Supplier == TTSSettings.TTSSupplier.AzureTTS)
                {
                    string currentRegion = settings.GetSupplierRegion(settings.Supplier);
                    listing.Label("Ustas.RimAI.Communication.Settings.TTS.AzureRegionLabel".Translate(currentRegion ?? "eastus"));
                    listing.Gap(6f);
                    
                    // Common Azure regions for TTS
                    var regionOptions = new[] { "eastus", "westus", "westus2", "eastus2", "westeurope", "northeurope", 
                                               "southeastasia", "eastasia", "australiaeast", "japaneast", "canadacentral" };
                    
                    Rect regionRect = listing.GetRect(30f);
                    string regionDisplay = currentRegion ?? "eastus";
                    if (Widgets.ButtonText(regionRect, regionDisplay))
                    {
                        var options = new System.Collections.Generic.List<FloatMenuOption>();
                        foreach (var region in regionOptions)
                        {
                            options.Add(new FloatMenuOption(region, delegate
                            {
                                settings.SetSupplierRegion(settings.Supplier, region);
                                // Update provider with new region
                                TTSService.SetProvider(settings.Supplier, settings);
                            }));
                        }
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                    
                    listing.Gap(6f);
                    listing.Label("Ustas.RimAI.Communication.Settings.TTS.CustomRegionLabel".Translate());
                    string customRegion = listing.TextEntry(currentRegion ?? "eastus");
                    if (customRegion != currentRegion)
                    {
                        settings.SetSupplierRegion(settings.Supplier, customRegion);
                        // Update provider with new region
                        TTSService.SetProvider(settings.Supplier, settings);
                    }
                }

                if (settings.Supplier == TTSSettings.TTSSupplier.OpenAI)
                {
                    DrawOpenAiSection(listing, settings);
                }

                // TTSWebUI base URL configuration
                if (settings.Supplier == TTSSettings.TTSSupplier.TTSWebUI)
                {
                    string currentBaseUrl = settings.GetSupplierRegion(settings.Supplier);
                    if (string.IsNullOrWhiteSpace(currentBaseUrl))
                    {
                        currentBaseUrl = "http://localhost:7778/v1";
                    }
                    
                    listing.Label("Ustas.RimAI.Communication.Settings.TTS.TTSWebUIBaseUrlLabel".Translate(currentBaseUrl));
                    listing.Gap(6f);
                    
                    // Common TTSWebUI base URLs
                    var urlOptions = new[] { 
                        "http://localhost:7778/v1",  // Default OpenAI API endpoint
                        "http://localhost:7770/v1",  // Gradio default port
                        "http://127.0.0.1:7778/v1"
                    };
                    
                    Rect urlRect = listing.GetRect(30f);
                    if (Widgets.ButtonText(urlRect, currentBaseUrl))
                    {
                        var options = new System.Collections.Generic.List<FloatMenuOption>();
                        foreach (var url in urlOptions)
                        {
                            options.Add(new FloatMenuOption(url, delegate
                            {
                                settings.SetSupplierRegion(settings.Supplier, url);
                                TTSService.SetProvider(settings.Supplier, settings);
                            }));
                        }
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                    
                    listing.Gap(6f);
                    listing.Label("Ustas.RimAI.Communication.Settings.TTS.TTSWebUICustomUrlLabel".Translate());
                    string customUrl = listing.TextEntry(currentBaseUrl);
                    if (customUrl != currentBaseUrl)
                    {
                        settings.SetSupplierRegion(settings.Supplier, customUrl);
                        TTSService.SetProvider(settings.Supplier, settings);
                    }
                    
                    listing.Gap(6f);
                    
                    // Model selection for TTSWebUI (user can specify model name)
                    string currentModel = settings.GetSupplierModel(settings.Supplier);
                    listing.Label("Ustas.RimAI.Communication.Settings.TTS.TTSWebUIModelLabel".Translate(currentModel ?? "(default)"));
                    listing.Gap(6f);
                    listing.Label("Ustas.RimAI.Communication.Settings.TTS.TTSWebUIModelHint".Translate());
                    string customModel = listing.TextEntry(currentModel ?? "");
                    if (customModel != currentModel)
                    {
                        settings.SetSupplierModel(settings.Supplier, customModel);
                    }
                }

                listing.Gap();
                
                int currentCooldown = settings.GetSupplierGenerateCooldown(settings.Supplier);
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.GenerateCooldownMiliSecondsLabel".Translate(currentCooldown.ToString()));
                int newCooldown = (int)listing.Slider(currentCooldown, 0, 20000);
                if (newCooldown != currentCooldown)
                    settings.SetSupplierGenerateCooldown(settings.Supplier, newCooldown);

                listing.Gap();

                float currentVolume = settings.GetSupplierVolume(settings.Supplier);
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.VolumeLabel".Translate(currentVolume.ToStringPercent()));
                float newVolume = listing.Slider(currentVolume, 0f, 1f);
                if (newVolume != currentVolume)
                    settings.SetSupplierVolume(settings.Supplier, newVolume);

                listing.Gap();

                // Sampling knobs only exist on the Fish Audio style backends
                if (TTSSettings.SupportsSampling(settings.Supplier))
                {
                    float currentTemp = settings.GetSupplierTemperature(settings.Supplier);
                    listing.Label("Ustas.RimAI.Communication.Settings.TTS.TemperatureLabel".Translate(currentTemp.ToString("F2")));
                    float newTemp = listing.Slider(currentTemp, 0.7f, 1.0f);
                    if (newTemp != currentTemp)
                        settings.SetSupplierTemperature(settings.Supplier, newTemp);

                    // Top P
                    float currentTopP = settings.GetSupplierTopP(settings.Supplier);
                    listing.Label("Ustas.RimAI.Communication.Settings.TTS.TopPLabel".Translate(currentTopP.ToString("F2")));
                    float newTopP = listing.Slider(currentTopP, 0.7f, 1.0f);
                    if (newTopP != currentTopP)
                        settings.SetSupplierTopP(settings.Supplier, newTopP);

                    listing.Gap();
                }

                // Speed slider (0.25 - 4.0)
                float currentSpeed = settings.GetSupplierSpeed(settings.Supplier);
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.SpeedLabel".Translate(currentSpeed.ToString("F2")));
                float newSpeed = listing.Slider(currentSpeed, 0.25f, 4.0f);
                if (newSpeed != currentSpeed)
                    settings.SetSupplierSpeed(settings.Supplier, newSpeed);

                listing.Gap();

                // Voice Models Section (per-supplier when a supplier is selected).
                System.Collections.Generic.List<VoiceModel> currentVoiceModels = settings.GetSupplierVoiceModels(settings.Supplier);
                DrawVoiceModelsSection(listing, settings, viewRect.width, currentVoiceModels);
            }

            listing.End();
            Widgets.EndScrollView();
        }

        private static System.Collections.Generic.List<string> openAiModelChoices = new System.Collections.Generic.List<string>();
        private static bool openAiModelsLoading = false;

        private static void SelectSupplier(TTSSettings settings, TTSSettings.TTSSupplier supplier)
        {
            settings.Supplier = supplier;

            if (supplier == TTSSettings.TTSSupplier.OpenAI)
            {
                EnsureOpenAiDefaults(settings);
                SeedVoiceRulesIfEmpty(settings, supplier);
            }

            TTSService.SetProvider(settings.Supplier, settings);
        }

        /// <summary>
        /// Fill in the endpoint, model and voice presets so OpenAI is usable straight after
        /// being selected instead of requiring manual entry.
        /// </summary>
        private static void EnsureOpenAiDefaults(TTSSettings settings)
        {
            string baseUrl = settings.GetSupplierRegion(TTSSettings.TTSSupplier.OpenAI);
            if (string.IsNullOrWhiteSpace(baseUrl) || !baseUrl.TrimStart().StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
            {
                settings.SetSupplierRegion(TTSSettings.TTSSupplier.OpenAI, Service.OpenAITTSClient.DefaultBaseUrl);
            }

            if (string.IsNullOrWhiteSpace(settings.GetSupplierModel(TTSSettings.TTSSupplier.OpenAI)))
            {
                settings.SetSupplierModel(TTSSettings.TTSSupplier.OpenAI, Service.OpenAITTSClient.DefaultModel);
            }

            var voiceModels = settings.GetSupplierVoiceModels(TTSSettings.TTSSupplier.OpenAI);
            if (voiceModels == null || voiceModels.Count == 0)
            {
                settings.SetSupplierVoiceModels(
                    TTSSettings.TTSSupplier.OpenAI,
                    TTSSettings.GetDefaultVoiceModels(TTSSettings.TTSSupplier.OpenAI));
            }
        }

        /// <summary>
        /// First-run convenience: a supplier that has no rules and no chosen default voice
        /// gets the generated spread, so pawns do not all share one voice out of the box.
        /// Existing user configuration is never overwritten.
        /// </summary>
        private static void SeedVoiceRulesIfEmpty(TTSSettings settings, TTSSettings.TTSSupplier supplier)
        {
            var existingRules = settings.GetSupplierVoiceRules(supplier);
            if (existingRules != null && existingRules.Count > 0)
                return;

            string defaultVoice = settings.GetSupplierDefaultVoiceModelId(supplier);
            bool defaultIsUnset = string.IsNullOrWhiteSpace(defaultVoice) || defaultVoice == VoiceModel.NONE_MODEL_ID;
            if (!defaultIsUnset)
                return;

            var generated = VoiceRulePresets.Generate(supplier, settings.GetSupplierVoiceModels(supplier));
            if (generated.Count == 0)
                return;

            settings.SetSupplierVoiceRules(supplier, generated);
            settings.SetSupplierDefaultVoiceModelId(supplier, VoiceModel.RULE_BASED_MODEL_ID);
        }

        private static void DrawOpenAiCredential(Listing_Standard listing)
        {
            bool present = Data.OpenAITtsCredential.Present;
            GUI.color = present ? Color.green : Color.red;
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.OpenAI.Credential".Translate(Data.OpenAITtsCredential.Display));
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.OpenAI.CredentialHint".Translate(Data.OpenAITtsCredential.Variable));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private static void DrawOpenAiSection(Listing_Standard listing, TTSSettings settings)
        {
            var supplier = TTSSettings.TTSSupplier.OpenAI;
            EnsureOpenAiDefaults(settings);

            string baseUrl = settings.GetSupplierRegion(supplier);
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.OpenAI.BaseUrlLabel".Translate(baseUrl));
            listing.Gap(6f);

            listing.Label("Ustas.RimAI.Communication.Settings.TTS.OpenAI.CustomUrlLabel".Translate());
            string editedUrl = listing.TextEntry(baseUrl);
            if (editedUrl != baseUrl)
            {
                settings.SetSupplierRegion(supplier, editedUrl);
                TTSService.SetProvider(supplier, settings);
            }

            listing.Gap(6f);

            // Model selection from a list instead of free text
            string currentModel = settings.GetSupplierModel(supplier);
            if (string.IsNullOrWhiteSpace(currentModel))
                currentModel = Service.OpenAITTSClient.DefaultModel;

            listing.Label("Ustas.RimAI.Communication.Settings.TTS.OpenAI.ModelLabel".Translate(currentModel));
            Rect modelRect = listing.GetRect(30f);
            if (Widgets.ButtonText(modelRect, currentModel))
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>();
                foreach (var candidate in BuildOpenAiModelChoices())
                {
                    var picked = candidate;
                    options.Add(new FloatMenuOption(picked, delegate
                    {
                        settings.SetSupplierModel(supplier, picked);
                    }));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap(6f);

            GUI.enabled = !openAiModelsLoading;
            Rect refreshRect = listing.GetRect(30f);
            if (Widgets.ButtonText(refreshRect, openAiModelsLoading
                    ? "Ustas.RimAI.Communication.Settings.TTS.OpenAI.ModelsLoading".Translate()
                    : "Ustas.RimAI.Communication.Settings.TTS.OpenAI.RefreshModels".Translate()))
            {
                RefreshOpenAiModels(settings);
            }
            GUI.enabled = true;

            listing.Gap(6f);

            // Audio container
            string currentFormat = settings.GetSupplierResponseFormat(supplier);
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.OpenAI.FormatLabel".Translate(currentFormat));
            Rect formatRect = listing.GetRect(30f);
            if (Widgets.ButtonText(formatRect, currentFormat))
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>();
                foreach (var format in Service.OpenAITTSClient.ResponseFormats)
                {
                    var picked = format;
                    options.Add(new FloatMenuOption(picked, delegate
                    {
                        settings.SetSupplierResponseFormat(supplier, picked);
                    }));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap(6f);

            // Delivery instructions (style steering)
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.OpenAI.InstructionsLabel".Translate());
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.OpenAI.InstructionsHint".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            string instructions = settings.GetSupplierInstructions(supplier) ?? "";
            Rect instructionsRect = listing.GetRect(70f);
            string editedInstructions = Widgets.TextArea(instructionsRect, instructions);
            if (editedInstructions != instructions)
                settings.SetSupplierInstructions(supplier, editedInstructions);

            listing.Gap(6f);
            Rect exampleRect = listing.GetRect(30f);
            if (Widgets.ButtonText(exampleRect, "Ustas.RimAI.Communication.Settings.TTS.OpenAI.InstructionsExample".Translate()))
            {
                settings.SetSupplierInstructions(
                    supplier,
                    "Ustas.RimAI.Communication.Settings.TTS.OpenAI.InstructionsExampleText".Translate());
            }
        }

        private static System.Collections.Generic.List<string> BuildOpenAiModelChoices()
        {
            var choices = new System.Collections.Generic.List<string>(Service.OpenAITTSClient.KnownModels);
            foreach (var fetched in openAiModelChoices)
            {
                if (!choices.Contains(fetched))
                    choices.Add(fetched);
            }
            return choices;
        }

        private static void RefreshOpenAiModels(TTSSettings settings)
        {
            string apiKey = Data.OpenAITtsCredential.Resolve();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Messages.Message(
                    "Ustas.RimAI.Communication.Settings.TTS.OpenAI.CredentialMissing".Translate(Data.OpenAITtsCredential.Variable),
                    MessageTypeDefOf.RejectInput,
                    false);
                return;
            }

            Service.OpenAITTSClient.SetBaseUrl(settings.GetSupplierRegion(TTSSettings.TTSSupplier.OpenAI));
            openAiModelsLoading = true;

            System.Threading.Tasks.Task.Run(async () =>
            {
                var models = await Service.OpenAITTSClient.ListSpeechModelsAsync(apiKey);
                EnqueueMainThreadAction(() =>
                {
                    openAiModelsLoading = false;
                    if (models != null && models.Count > 0)
                    {
                        openAiModelChoices = models;
                        Messages.Message(
                            "Ustas.RimAI.Communication.Settings.TTS.OpenAI.ModelsRefreshed".Translate(models.Count),
                            MessageTypeDefOf.TaskCompletion,
                            false);
                    }
                    else
                    {
                        Messages.Message(
                            "Ustas.RimAI.Communication.Settings.TTS.OpenAI.ModelsRefreshFailed".Translate(),
                            MessageTypeDefOf.RejectInput,
                            false);
                    }
                });
            });
        }

        private static void DrawProcessingPromptSection(Listing_Standard listing, TTSSettings settings)
        {
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.ProcessingPromptLabel".Translate());
            
            // Initialize buffer if needed - show default prompt if custom is empty
            if (!processingPromptInitialized)
            {
                processingPromptBuffer = string.IsNullOrWhiteSpace(settings.CustomTTSProcessingPrompt)
                    ? Data.TTSConstant.DefaultTTSProcessingPrompt
                    : settings.CustomTTSProcessingPrompt;
                processingPromptInitialized = true;
            }

            // Instructions
            Text.Font = GameFont.Tiny;
            GUI.color = Color.cyan;
            Rect tipRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(tipRect, "Ustas.RimAI.Communication.Settings.TTS.ProcessingPromptTip".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(6f);

            // Text area for prompt - display buffer which contains either custom or default
            float textAreaHeight = 120f;
            Rect textAreaRect = listing.GetRect(textAreaHeight);
            string displayPrompt = processingPromptBuffer;
            string newPrompt = Widgets.TextArea(textAreaRect, displayPrompt);

            // Only save if user actually modified the content
            if (newPrompt != displayPrompt)
            {
                processingPromptBuffer = newPrompt.Replace("\\n", "\n");
                settings.CustomTTSProcessingPrompt = processingPromptBuffer;
            }

            listing.Gap(6f);

            // Reset buttons - First row: FishAudio, CosyVoice, IndexTTS
            Rect resetButtonsRect1 = listing.GetRect(30f);
            float gap = 4f;
            float btnW = (resetButtonsRect1.width - gap * 2) / 3f;
            Rect fishRect = new Rect(resetButtonsRect1.x, resetButtonsRect1.y, btnW, resetButtonsRect1.height);
            Rect cosyRect = new Rect(resetButtonsRect1.x + btnW + gap, resetButtonsRect1.y, btnW, resetButtonsRect1.height);
            Rect indexRect = new Rect(resetButtonsRect1.x + (btnW + gap) * 2f, resetButtonsRect1.y, btnW, resetButtonsRect1.height);

            if (Widgets.ButtonText(fishRect, "Ustas.RimAI.Communication.Settings.TTS.ResetPrompt.FishAudio".Translate()))
            {
                settings.CustomTTSProcessingPrompt = "";
                processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt;
            }

            if (Widgets.ButtonText(cosyRect, "Ustas.RimAI.Communication.Settings.TTS.ResetPrompt.CosyVoice".Translate()))
            {
                settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_CosyVoice;
                processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_CosyVoice;
            }

            if (Widgets.ButtonText(indexRect, "Ustas.RimAI.Communication.Settings.TTS.ResetPrompt.IndexTTS".Translate()))
            {
                settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_IndexTTS;
                processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_IndexTTS;
            }

            // Reset buttons - Second row: AzureTTS, EdgeTTS, GeminiTTS
            listing.Gap(6f);
            Rect resetButtonsRect2 = listing.GetRect(30f);
            Rect azureRect = new Rect(resetButtonsRect2.x, resetButtonsRect2.y, btnW, resetButtonsRect2.height);
            Rect edgeRect = new Rect(resetButtonsRect2.x + btnW + gap, resetButtonsRect2.y, btnW, resetButtonsRect2.height);
            Rect geminiRect = new Rect(resetButtonsRect2.x + (btnW + gap) * 2f, resetButtonsRect2.y, btnW, resetButtonsRect2.height);

            if (Widgets.ButtonText(azureRect, "Ustas.RimAI.Communication.Settings.TTS.ResetPrompt.AzureTTS".Translate()))
            {
                settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_AzureTTS;
                processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_AzureTTS;
            }

            if (Widgets.ButtonText(edgeRect, "Ustas.RimAI.Communication.Settings.TTS.ResetPrompt.EdgeTTS".Translate()))
            {
                settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_EdgeTTS;
                processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_EdgeTTS;
            }

            if (Widgets.ButtonText(geminiRect, "Ustas.RimAI.Communication.Settings.TTS.ResetPrompt.GeminiTTS".Translate()))
            {
                settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_GeminiTTS;
                processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_GeminiTTS;
            }

            // Reset buttons - Third row: TTSWebUI, OpenAI
            listing.Gap(6f);
            Rect resetButtonsRect3 = listing.GetRect(30f);
            Rect ttswebuiRect = new Rect(resetButtonsRect3.x, resetButtonsRect3.y, btnW, resetButtonsRect3.height);
            Rect openAiRect = new Rect(resetButtonsRect3.x + btnW + gap, resetButtonsRect3.y, btnW, resetButtonsRect3.height);

            if (Widgets.ButtonText(ttswebuiRect, "Ustas.RimAI.Communication.Settings.TTS.ResetPrompt.TTSWebUI".Translate()))
            {
                settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_TTSWebUI;
                processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_TTSWebUI;
            }

            if (Widgets.ButtonText(openAiRect, "Ustas.RimAI.Communication.Settings.TTS.ResetPrompt.OpenAI".Translate()))
            {
                settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_OpenAI;
                processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_OpenAI;
            }
        }

        private static void DrawVoiceModelsSection(Listing_Standard listing, TTSSettings settings, float width, System.Collections.Generic.List<VoiceModel> voiceModels)
        {
            Text.Font = GameFont.Medium;
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.VoiceModels".Translate());
            Text.Font = GameFont.Small;

            listing.Label("Ustas.RimAI.Communication.Settings.TTS.DefaultVoiceModel".Translate());

            // Show default model selector (now includes RULE_BASED as an option)
            DrawSimpleDefaultVoiceSelector(listing, settings, voiceModels);

            listing.Gap(6f);

            // Rules button and list
            if (listing.ButtonText("Ustas.RimAI.Communication.Settings.TTS.Rules".Translate()))
            {
                // Toggle rules visibility (using a static variable)
                showRulesList = !showRulesList;
            }

            if (showRulesList)
            {
                listing.Gap(6f);
                DrawVoiceRulesList(listing, settings, width, voiceModels);
            }

            // Player reference voice selection (always shown)
            DrawPlayerVoiceSelector(listing, settings);

            // Voice model list (model configurations)
            DrawVoiceModelsList(listing, settings, width, voiceModels);
        }

        private static bool showRulesList = false;
        private static int selectedRuleIndex = -1;
        private static int lastClickedRuleIndex = -1;
        private static float lastClickTime = 0f;
        private static readonly float DOUBLE_CLICK_TIME = 0.5f; // 500ms window for double click

        private static void DrawSimpleDefaultVoiceSelector(Listing_Standard listing, TTSSettings settings, System.Collections.Generic.List<VoiceModel> voiceModels)
        {
            // Default model selector (shows names from current voice model list)
            string defaultModelId = settings.GetSupplierDefaultVoiceModelId(settings.Supplier);

            string currentDefaultName = "Ustas.RimAI.Communication.Settings.TTS.NotSet".Translate();
            if (!string.IsNullOrEmpty(defaultModelId))
            {
                if (defaultModelId == VoiceModel.NONE_MODEL_ID)
                {
                    currentDefaultName = "Ustas.RimAI.Communication.Settings.TTS.NoneModel".Translate();
                }
                else if (defaultModelId == VoiceModel.RULE_BASED_MODEL_ID)
                {
                    currentDefaultName = "Ustas.RimAI.Communication.Settings.TTS.RuleBased".Translate();
                }
                else if (voiceModels != null)
                {
                    var m = voiceModels.FirstOrDefault(x => x.ModelId == defaultModelId);
                    if (m != null)
                        currentDefaultName = m.GetDisplayName();
                }
            }

            if (listing.ButtonText("Ustas.RimAI.Communication.Settings.TTS.DefaultModel".Translate(currentDefaultName)))
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>();
                options.Add(new FloatMenuOption("Ustas.RimAI.Communication.Settings.TTS.ClearDefault".Translate(), delegate
                {
                    settings.SetSupplierDefaultVoiceModelId(settings.Supplier, null);
                }));

                // Add NONE pseudo-model option
                options.Add(new FloatMenuOption("Ustas.RimAI.Communication.Settings.TTS.NoneModel".Translate(), delegate
                {
                    settings.SetSupplierDefaultVoiceModelId(settings.Supplier, VoiceModel.NONE_MODEL_ID);
                }));

                // Add RULE_BASED option
                options.Add(new FloatMenuOption("Ustas.RimAI.Communication.Settings.TTS.RuleBased".Translate(), delegate
                {
                    settings.SetSupplierDefaultVoiceModelId(settings.Supplier, VoiceModel.RULE_BASED_MODEL_ID);
                }));

                if (voiceModels != null)
                {
                    foreach (var vm in voiceModels)
                    {
                        var display = vm.GetDisplayName();
                        options.Add(new FloatMenuOption(display, delegate
                        {
                            settings.SetSupplierDefaultVoiceModelId(settings.Supplier, vm.ModelId);
                        }));
                    }
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private static void DrawVoiceRulesList(Listing_Standard listing, TTSSettings settings, float width, System.Collections.Generic.List<VoiceModel> voiceModels)
        {
            var rules = settings.GetSupplierVoiceRules(settings.Supplier);
            
            // Rules list title
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.AdvancedMode.RulesList".Translate());
            
            // Container box for rules
            float ruleListHeight = Mathf.Max(200f, rules.Count * 35f + 10f);
            Rect ruleListOuterRect = listing.GetRect(ruleListHeight);
            
            Widgets.DrawBoxSolid(ruleListOuterRect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
            Widgets.DrawBox(ruleListOuterRect);
            
            Rect ruleListInnerRect = ruleListOuterRect.ContractedBy(5f);
            Rect ruleListViewRect = new Rect(0f, 0f, ruleListInnerRect.width - 20f, rules.Count * 35f);
            
            Vector2 ruleScrollPos = Vector2.zero;
            Widgets.BeginScrollView(ruleListInnerRect, ref ruleScrollPos, ruleListViewRect);
            
            float y = 0f;
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                Rect ruleRect = new Rect(0f, y, ruleListViewRect.width, 30f);
                
                // Highlight selected rule
                if (i == selectedRuleIndex)
                {
                    Widgets.DrawHighlight(ruleRect);
                }
                
                // Rule display text (truncated if needed)
                string displayText = rule.GetDisplayString(ruleRect.width - 10f);
                
                // Double click detection: check if same item clicked within time window
                if (Widgets.ButtonInvisible(ruleRect))
                {
                    float currentTime = Time.realtimeSinceStartup;
                    bool isDoubleClick = (i == lastClickedRuleIndex) && 
                                        (currentTime - lastClickTime < DOUBLE_CLICK_TIME);
                    
                    if (isDoubleClick)
                    {
                        // Double click - open editor
                        Find.WindowStack.Add(new VoiceRuleEditorWindow(rule, settings, () =>
                        {
                            settings.SetSupplierVoiceRules(settings.Supplier, rules);
                        }));
                        lastClickedRuleIndex = -1; // Reset to prevent triple-click
                    }
                    else
                    {
                        // Single click - select and record click
                        selectedRuleIndex = i;
                        lastClickedRuleIndex = i;
                        lastClickTime = currentTime;
                    }
                }
                
                Rect labelRect = new Rect(ruleRect.x + 5f, ruleRect.y, ruleRect.width - 10f, ruleRect.height);
                Widgets.Label(labelRect, displayText);
                
                y += 35f;
            }
            
            Widgets.EndScrollView();
            
            listing.Gap(6f);
            
            // Control buttons: ↑ ↓ + ×
            Rect buttonRowRect = listing.GetRect(30f);
            float buttonWidth = 40f;
            float buttonGap = 5f;
            
            Rect upButtonRect = new Rect(buttonRowRect.x, buttonRowRect.y, buttonWidth, 30f);
            if (Widgets.ButtonText(upButtonRect, "↑"))
            {
                if (selectedRuleIndex > 0)
                {
                    var temp = rules[selectedRuleIndex];
                    rules[selectedRuleIndex] = rules[selectedRuleIndex - 1];
                    rules[selectedRuleIndex - 1] = temp;
                    selectedRuleIndex--;
                    settings.SetSupplierVoiceRules(settings.Supplier, rules);
                }
            }
            
            Rect downButtonRect = new Rect(upButtonRect.xMax + buttonGap, buttonRowRect.y, buttonWidth, 30f);
            if (Widgets.ButtonText(downButtonRect, "↓"))
            {
                if (selectedRuleIndex >= 0 && selectedRuleIndex < rules.Count - 1)
                {
                    var temp = rules[selectedRuleIndex];
                    rules[selectedRuleIndex] = rules[selectedRuleIndex + 1];
                    rules[selectedRuleIndex + 1] = temp;
                    selectedRuleIndex++;
                    settings.SetSupplierVoiceRules(settings.Supplier, rules);
                }
            }
            
            Rect addButtonRect = new Rect(downButtonRect.xMax + buttonGap, buttonRowRect.y, buttonWidth, 30f);
            if (Widgets.ButtonText(addButtonRect, "+"))
            {
                var newRule = new VoiceAssignmentRule();
                Find.WindowStack.Add(new VoiceRuleEditorWindow(newRule, settings, () =>
                {
                    rules.Add(newRule);
                    settings.SetSupplierVoiceRules(settings.Supplier, rules);
                    selectedRuleIndex = rules.Count - 1;
                }));
            }
            
            Rect deleteButtonRect = new Rect(addButtonRect.xMax + buttonGap, buttonRowRect.y, buttonWidth, 30f);
            if (Widgets.ButtonText(deleteButtonRect, "×"))
            {
                if (selectedRuleIndex >= 0 && selectedRuleIndex < rules.Count)
                {
                    rules.RemoveAt(selectedRuleIndex);
                    settings.SetSupplierVoiceRules(settings.Supplier, rules);
                    selectedRuleIndex = -1;
                }
            }

            listing.Gap(6f);

            Rect generateRect = listing.GetRect(30f);
            if (Widgets.ButtonText(generateRect, "Ustas.RimAI.Communication.Settings.TTS.GenerateRules".Translate()))
            {
                GenerateVoiceRules(settings, voiceModels);
            }

            TooltipHandler.TipRegion(generateRect, "Ustas.RimAI.Communication.Settings.TTS.GenerateRulesTooltip".Translate());

            listing.Gap();
        }

        /// <summary>
        /// Replace the rule list with a generated gender and age spread over the configured
        /// voices, and switch the default voice to rule-based so pawns actually differ.
        /// </summary>
        private static void GenerateVoiceRules(TTSSettings settings, System.Collections.Generic.List<VoiceModel> voiceModels)
        {
            var generated = VoiceRulePresets.Generate(settings.Supplier, voiceModels);
            if (generated.Count == 0)
            {
                Messages.Message(
                    "Ustas.RimAI.Communication.Settings.TTS.RulesGenerateFailed".Translate(),
                    MessageTypeDefOf.RejectInput,
                    false);
                return;
            }

            settings.SetSupplierVoiceRules(settings.Supplier, generated);
            settings.SetSupplierDefaultVoiceModelId(settings.Supplier, VoiceModel.RULE_BASED_MODEL_ID);
            selectedRuleIndex = -1;

            Messages.Message(
                "Ustas.RimAI.Communication.Settings.TTS.RulesGenerated".Translate(generated.Count),
                MessageTypeDefOf.TaskCompletion,
                false);
        }

        private static void DrawPlayerVoiceSelector(Listing_Standard listing, TTSSettings settings)
        {
            // Player reference voice selection (single-line dropdown using supplier voice models)
            listing.Gap(6f);
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.PlayerVoiceModel".Translate());
            Rect playerRect = listing.GetRect(Text.LineHeight);

            string currentPlayerSelectionName;
            var playerModelId = settings.PlayerReferenceVoiceModelId;
            if (playerModelId == VoiceModel.NONE_MODEL_ID)
            {
                currentPlayerSelectionName = "Ustas.RimAI.Communication.Settings.TTS.NoneModel".Translate();
            }
            else
            {
                var vm = settings.GetSupplierVoiceModels(settings.Supplier)?.FirstOrDefault(x => x.ModelId == playerModelId);
                currentPlayerSelectionName = vm?.GetDisplayName() ?? playerModelId;
            }

            if (Widgets.ButtonText(playerRect, currentPlayerSelectionName))
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>();

                // None
                options.Add(new FloatMenuOption("Ustas.RimAI.Communication.Settings.TTS.NoneModel".Translate(), delegate
                {
                    settings.PlayerReferenceVoiceModelId = VoiceModel.NONE_MODEL_ID;
                    RimTalkPatches.UpdatePlayerPawnVoice();
                }));

                var list = settings.GetSupplierVoiceModels(settings.Supplier);
                if (list != null)
                {
                    foreach (var vm in list)
                    {
                        var display = vm.GetDisplayName();
                        var id = vm.ModelId ?? "";
                        options.Add(new FloatMenuOption(display, delegate
                        {
                            settings.PlayerReferenceVoiceModelId = id;
                            RimTalkPatches.UpdatePlayerPawnVoice();
                        }));
                    }
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap();
        }

        private static void DrawVoiceModelsList(Listing_Standard listing, TTSSettings settings, float width, System.Collections.Generic.List<VoiceModel> voiceModels)
        {
            // Header with add/remove buttons (similar to RimTalk API configs)
            Rect headerRect = listing.GetRect(24f);
            Rect addButtonRect = new Rect(headerRect.x + headerRect.width - 65f, headerRect.y, 30f, 24f);
            Rect removeButtonRect = new Rect(headerRect.x + headerRect.width - 30f, headerRect.y, 30f, 24f);
            headerRect.width -= 70f;

            Widgets.Label(headerRect, "Ustas.RimAI.Communication.Settings.TTS.ModelConfigurations".Translate());

            listing.Gap(6f);

            // Upload user voice section (only shown when supplier supports SiliconFlow)
            if (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS)
            {
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.UploadUserVoiceLabel".Translate());
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.UploadFilePath".Translate());
                uploadPathBuffer = listing.TextEntry(uploadPathBuffer ?? "");
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.UploadName".Translate());
                uploadNameBuffer = listing.TextEntry(uploadNameBuffer ?? "");
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.UploadTextPreview".Translate());
                uploadTextBuffer = listing.TextEntry(uploadTextBuffer ?? "");
                Rect uploadRect = listing.GetRect(30f);
                if (Widgets.ButtonText(uploadRect, "Ustas.RimAI.Communication.Settings.TTS.UploadButton".Translate()))
                {
                    // Validate local file
                    if (string.IsNullOrWhiteSpace(uploadPathBuffer) || !System.IO.File.Exists(uploadPathBuffer))
                    {
                        Messages.Message("Ustas.RimAI.Communication.Voices.UploadFailed.LocalFileNotFound".Translate(), MessageTypeDefOf.RejectInput, false);
                    }
                    else if (string.IsNullOrWhiteSpace(uploadNameBuffer))
                    {
                        Messages.Message("Ustas.RimAI.Communication.Voices.UploadFailed.NameEmpty".Translate(), MessageTypeDefOf.RejectInput, false);
                    }
                    else
                    {
                        // Kick off upload in background
                        var apiKey = settings.GetSupplierApiKey(settings.Supplier);
                        var model = settings.GetSupplierModel(settings.Supplier);
                        System.Threading.Tasks.Task.Run(async () =>
                        {
                            var uri = await Service.SiliconFlowClient.UploadUserVoiceAsync(apiKey, model, uploadPathBuffer, uploadNameBuffer, uploadTextBuffer);
                            if (!string.IsNullOrWhiteSpace(uri))
                            {
                                // Defer the Refresh and message to run on the main thread
                                EnqueueMainThreadAction(() =>
                                {
                                    Refresh();
                                    Messages.Message("Ustas.RimAI.Communication.Voices.UploadComplete".Translate(), MessageTypeDefOf.TaskCompletion, false);
                                });
                            }
                            else
                            {
                                EnqueueMainThreadAction(() => Messages.Message("Ustas.RimAI.Communication.Voices.UploadFailed.ServerError".Translate(), MessageTypeDefOf.RejectInput, false));
                            }
                        });
                    }
                }

                listing.Gap(6f);
            }

            if (Widgets.ButtonText(addButtonRect, "+"))
            {
                if (voiceModels == null)
                    voiceModels = new System.Collections.Generic.List<VoiceModel>();
                voiceModels.Add(new VoiceModel { ModelName = "", ModelId = "" });
                settings.SetSupplierVoiceModels(settings.Supplier, voiceModels);
            }

            GUI.enabled = voiceModels != null && voiceModels.Count > 0;
            if (Widgets.ButtonText(removeButtonRect, "−"))
            {
                if (voiceModels != null && voiceModels.Count > 0)
                {
                    voiceModels.RemoveAt(voiceModels.Count - 1);
                    settings.SetSupplierVoiceModels(settings.Supplier, voiceModels);
                }
            }
            GUI.enabled = true;

            listing.Gap(6f);

            // Column descriptions
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.ColumnDescription".Translate());
            listing.Gap(6f);

            // Draw table headers
            Rect tableHeaderRect = listing.GetRect(24f);
            float x = tableHeaderRect.x;
            float y = tableHeaderRect.y;
            float height = tableHeaderRect.height;

            x += 60f; // Space for reorder buttons

            float nameWidth = (width - 130f) * 0.4f;
            float idWidth = (width - 130f) * 0.4f;

            Rect nameHeaderRect = new Rect(x, y, nameWidth, height);
            Widgets.Label(nameHeaderRect, "Ustas.RimAI.Communication.Settings.TTS.ColumnModelName".Translate());
            x += nameWidth + 5f;

            Rect idHeaderRect = new Rect(x, y, idWidth, height);
            Widgets.Label(idHeaderRect, "Ustas.RimAI.Communication.Settings.TTS.ColumnModelID".Translate());

            // Draw each model config row
            if (voiceModels != null)
            {
                for (int i = 0; i < voiceModels.Count; i++)
                {
                    DrawModelConfigRow(listing, voiceModels[i], i, voiceModels, width);
                }
            }

            if (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS || settings.Supplier == TTSSettings.TTSSupplier.AzureTTS || settings.Supplier == TTSSettings.TTSSupplier.EdgeTTS || settings.Supplier == TTSSettings.TTSSupplier.GeminiTTS || settings.Supplier == TTSSettings.TTSSupplier.OpenAI)
            {
                listing.Gap(6f);
                // Single Reset Models button placed after the full list
                Rect resetAllRect = listing.GetRect(30f);
                if (Widgets.ButtonText(resetAllRect, "Ustas.RimAI.Communication.Settings.TTS.ResetModelsButton".Translate()))
                {
                    Refresh();
                }
            }

            // Voice library button for AzureTTS/EdgeTTS
            if (settings.Supplier == TTSSettings.TTSSupplier.AzureTTS || settings.Supplier == TTSSettings.TTSSupplier.EdgeTTS)
            {
                listing.Gap(6f);
                Rect voiceLibraryRect = listing.GetRect(30f);
                string buttonLabel = settings.Supplier == TTSSettings.TTSSupplier.AzureTTS 
                    ? "Ustas.RimAI.Communication.Settings.TTS.AzureVoiceLibrary".Translate() 
                    : "Ustas.RimAI.Communication.Settings.TTS.EdgeVoiceLibrary".Translate();
                if (Widgets.ButtonText(voiceLibraryRect, buttonLabel))
                {
                    Find.WindowStack.Add(new VoiceLibraryWindow(settings.Supplier));
                }
            }
        }

        private static void Refresh()
        {
            var settings = TTSConfig.Settings;
            var voiceModels = settings.GetSupplierVoiceModels(settings.Supplier);
            var presets = TTSSettings.GetDefaultVoiceModels(settings.Supplier);
            if (presets != null && presets.Count > 0)
            {
                // Merge presets with existing user models: keep presets first, then
                // append any custom/empty entries that aren't already in presets.
                var merged = new System.Collections.Generic.List<VoiceModel>();
                foreach (var p in presets)
                {
                    if (p == null) continue;
                    merged.Add(new VoiceModel { ModelId = p.ModelId, ModelName = p.ModelName });
                }

                if (voiceModels != null)
                {
                    foreach (var vm in voiceModels)
                    {
                        if (vm == null) continue;
                        // preserve blank/custom entries (no ModelId) and any models not present in presets
                        if (string.IsNullOrWhiteSpace(vm.ModelId) || !merged.Any(x => x.ModelId == vm.ModelId))
                        {
                            merged.Add(new VoiceModel { ModelId = vm.ModelId, ModelName = vm.ModelName });
                        }
                    }
                }

                settings.SetSupplierVoiceModels(settings.Supplier, merged);
                voiceModels = settings.GetSupplierVoiceModels(settings.Supplier);
            }

        // Also: when ResetModels is pressed above we attempt to sync user-uploaded voices from SiliconFlow.
        // The network call is done asynchronously and will merge any returned user voices into the settings.
        // (This runs when the user pressed ResetModels; the above code already applied system presets.)
            if (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS)
            {
                var apiKey = settings.GetSupplierApiKey(settings.Supplier);
                var supplier = settings.Supplier;
                System.Threading.Tasks.Task.Run(async () =>
                {
                    var list = await Service.SiliconFlowClient.ListUserVoicesAsync(apiKey);
                    if (list != null && list.Count > 0)
                    {
                        var current = settings.GetSupplierVoiceModels(supplier) ?? new System.Collections.Generic.List<Data.VoiceModel>();
                        bool changed = false;
                        foreach (var t in list)
                        {
                            if (!current.Exists(x => x.ModelId == t.Item1))
                            {
                                current.Add(new Data.VoiceModel(t.Item1, t.Item2));
                                changed = true;
                            }
                        }
                        if (changed)
                        {
                            settings.SetSupplierVoiceModels(supplier, current);
                        }
                        // Notify user that sync completed (enqueue to show on main thread)
                        EnqueueMessage("Ustas.RimAI.Communication.Voices.SyncComplete".Translate(), MessageTypeDefOf.TaskCompletion);
                    }
                });
            }
        }

        private static void DrawModelConfigRow(Listing_Standard listing, VoiceModel model, int index, System.Collections.Generic.List<VoiceModel> models, float width)
        {
            Rect rowRect = listing.GetRect(30f);
            float x = rowRect.x;
            float y = rowRect.y;
            float height = rowRect.height;

            // Reorder buttons
            Rect upButtonRect = new Rect(x, y, 24f, height);
            if (Widgets.ButtonText(upButtonRect, "▲") && index > 0)
            {
                (models[index], models[index - 1]) = (models[index - 1], models[index]);
            }
            x += 30f;

            Rect downButtonRect = new Rect(x, y, 24f, height);
            if (Widgets.ButtonText(downButtonRect, "▼") && index < models.Count - 1)
            {
                (models[index], models[index + 1]) = (models[index + 1], models[index]);
            }
            x += 30f;

            float nameWidth = (width - 130f) * 0.4f;
            float idWidth = (width - 130f) * 0.4f;

            // Model Name field
            Rect nameRect = new Rect(x, y, nameWidth, height);
            model.ModelName = Widgets.TextField(nameRect, model.ModelName ?? "");
            x += nameWidth + 5f;

            // Model ID field
            Rect idRect = new Rect(x, y, idWidth, height);
            model.ModelId = Widgets.TextField(idRect, model.ModelId ?? "");

            // Delete button for this row
            Rect delRect = new Rect(idRect.xMax + 5f, y, 24f, height);
            if (Widgets.ButtonText(delRect, "X"))
            {
                // If looks like a SiliconFlow user voice (speech:...), attempt deletion
                string toDeleteId = model.ModelId ?? "";
                if (!string.IsNullOrWhiteSpace(toDeleteId) && toDeleteId.StartsWith("speech:"))
                {
                    var apiKey = LoadedModManager.GetMod(typeof(TTSMod)) is TTSMod mod ? (mod.GetSettings<TTSSettings>()?.GetSupplierApiKey(mod.GetSettings<TTSSettings>().Supplier) ?? "") : "";
                    var supplier = LoadedModManager.GetMod(typeof(TTSMod)) is TTSMod _m2 ? _m2.GetSettings<TTSSettings>().Supplier : TTSSettings.TTSSupplier.None;
                    // Use background task to delete
                    System.Threading.Tasks.Task.Run(async () =>
                    {
                        bool ok = await Service.SiliconFlowClient.DeleteUserVoiceAsync(apiKey, toDeleteId);
                        if (ok)
                            EnqueueMessage("Ustas.RimAI.Communication.Voices.DeleteComplete".Translate(), MessageTypeDefOf.TaskCompletion);
                        else
                            EnqueueMessage("Ustas.RimAI.Communication.Voices.DeleteFailed".Translate(), MessageTypeDefOf.RejectInput);
                    });
                }

                // Remove locally regardless (server deletion attempted above)
                if (models != null && index >= 0 && index < models.Count)
                {
                    models.RemoveAt(index);
                }
            }
        }

        private static void DrawApiConfigSection(Listing_Standard listing, TTSSettings settings)
        {
            Text.Font = GameFont.Medium;
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.LLMApiConfig".Translate());
            Text.Font = GameFont.Small;

            listing.Gap(6f);

            listing.CheckboxLabeled(
                "Ustas.RimAI.Communication.Settings.TTS.UseLlmPreprocess".Translate(),
                ref settings.EnableTextPreprocessing);

            listing.Gap(6f);

            string model = VoiceSharedAiText.EffectiveModel;
            if (string.IsNullOrWhiteSpace(model))
                model = "Ustas.RimAI.Communication.Settings.TTS.NotSet".Translate();
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.SharedModel".Translate(model));
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.SharedLanguage".Translate(VoiceSharedAiText.Language));
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.SharedLanguageHint".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.Gap(6f);

            listing.CheckboxLabeled(
                "Ustas.RimAI.Communication.Settings.TTS.RemoveBracketsInPreProcess".Translate(),
                ref settings.RemoveBracketsInPreProcess,
                "Ustas.RimAI.Communication.Settings.TTS.RemoveBracketsInPreProcessTooltip".Translate());
        }

        private static string SupplierString(TTSSettings.TTSSupplier supplier)
        {
            return supplier switch
            {
                TTSSettings.TTSSupplier.FishAudio => "Ustas.RimAI.Communication.Settings.TTS.TTSSupplier.FishAudio".Translate(),
                TTSSettings.TTSSupplier.CosyVoice => "Ustas.RimAI.Communication.Settings.TTS.TTSSupplier.CosyVoice".Translate(),
                TTSSettings.TTSSupplier.IndexTTS => "Ustas.RimAI.Communication.Settings.TTS.TTSSupplier.IndexTTS".Translate(),
                TTSSettings.TTSSupplier.AzureTTS => "Ustas.RimAI.Communication.Settings.TTS.TTSSupplier.AzureTTS".Translate(),
                TTSSettings.TTSSupplier.EdgeTTS => "Ustas.RimAI.Communication.Settings.TTS.TTSSupplier.EdgeTTS".Translate(),
                TTSSettings.TTSSupplier.GeminiTTS => "Ustas.RimAI.Communication.Settings.TTS.TTSSupplier.GeminiTTS".Translate(),
                TTSSettings.TTSSupplier.TTSWebUI => "Ustas.RimAI.Communication.Settings.TTS.TTSSupplier.TTSWebUI".Translate(),
                TTSSettings.TTSSupplier.OpenAI => "Ustas.RimAI.Communication.Settings.TTS.TTSSupplier.OpenAI".Translate(),
                TTSSettings.TTSSupplier.None => "Ustas.RimAI.Communication.Settings.TTS.None".Translate(),
                _ => supplier.ToString(),
            };
        }
    }
}
