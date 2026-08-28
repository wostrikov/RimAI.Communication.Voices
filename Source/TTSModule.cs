using System;
using Ustas.RimAI.Communication.Voices.Data;
using Ustas.RimAI.Communication.Voices.Policy;
using Verse;
using Ustas.RimAI.Communication.Voices.Diagnostics;

namespace Ustas.RimAI.Communication.Voices
{
    /// <summary>
    /// Main implementation of TTS module with lifecycle management
    /// </summary>
    public class TTSModule : ITTSModule
    {
        private static TTSModule _instance;
        private TTSSettings _settings;
        
        public static TTSModule Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new TTSModule();
                return _instance;
            }
        }

        private TTSModule()
        {
            // Initialize with default settings to prevent null reference
            // Will be replaced with actual mod settings when Initialize() is called
            _settings = new TTSSettings();
        }
        
        public TTSSettings GetSettings()
        {
            return _settings;
        }

        public void Initialize()
        {
            // Load TTS settings
            var modInstance = LoadedModManager.GetMod(typeof(TTSMod)) as TTSMod;
            if (modInstance != null)
            {
                _settings = modInstance.GetSettings<TTSSettings>();
            }
            else
            {
                _settings = new TTSSettings();
            }

            // Apply configured provider implementation
            try
            {
                Service.TTSService.SetProvider(_settings.Supplier, _settings);
            }
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY - settings apply must not throw into the host, but the player has to learn their provider did not take
            catch (System.Exception ex)
            {
                Log.Warning("[RimAI.Voices] Could not apply speech provider '"
                    + _settings.Supplier + "': " + ex.Message);
            }

            ModuleLog.Message("[RimAI.Voices] ========== Shared RimAI text-AI ==========");
            ModuleLog.Message($"[RimAI.Voices] Provider: {Service.VoiceSharedAiText.Provider}");
            ModuleLog.Message($"[RimAI.Voices] Model: {(string.IsNullOrWhiteSpace(Service.VoiceSharedAiText.EffectiveModel) ? "(not set)" : Service.VoiceSharedAiText.EffectiveModel)}");
            ModuleLog.Message($"[RimAI.Voices] Language: {Service.VoiceSharedAiText.Language}");
            ModuleLog.Message("[RimAI.Voices] ==========================================");

            ModuleLog.Message("[RimAI.Voices] TTS Module initialized");
        }

        public void OnDialogueGenerated(string text, Pawn pawn, Guid dialogueId)
        {
            if (!IsActive) return;
            if (string.IsNullOrEmpty(text)) return;
            if (pawn == null) return;

            // Start TTS generation asynchronously
            Service.TTSService.ProcessDialogue(text, pawn, dialogueId, _settings);
        }

        public void OnDialogueCancelled(Guid dialogueId)
        {
            if (!IsActive) return;
            if (dialogueId == Guid.Empty) return;

            Service.TTSService.CancelDialogue(dialogueId);
        }

        public void OnGameLoaded()
        {
            if (!IsActive) return;
            
            ModuleLog.Message("[RimAI.Voices] Game loaded, resetting TTS state");
            ApplyShutdown(VoiceShutdownPolicy.ForLoad());
        }

        public void OnGameExit()
        {
            ModuleLog.Message("[RimAI.Voices] Game exiting, full shutdown");
            ApplyShutdown(VoiceShutdownPolicy.ForExit());
        }

        static void ApplyShutdown(VoiceShutdownPlan plan)
        {
            if (plan.StopAll)
                Service.TTSService.StopAll(
                    plan.PermanentShutdown,
                    touchUnityAudio: !plan.FullResetPlayback);
            if (plan.FullResetPlayback)
                Service.AudioPlaybackService.FullReset();
        }

        public bool IsActive => _settings?.EnableTTS ?? false;

        public TTSSettings Settings => _settings;
    }
}
