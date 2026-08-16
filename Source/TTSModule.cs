using System;
using Ustas.RimAI.Communication.Voices.Data;
using Verse;

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
            catch { }

            // Output TTS API configuration
            Log.Message("[RimAI.Voices] ========== TTS API Configuration ==========");
            Log.Message($"[RimAI.Voices] Provider: {_settings.ApiProvider}");
            var activeConfig = global::Ustas.RimAI.Communication.Settings.Get()?.GetActiveConfig();
            string effectiveModel = (_settings.ApiProvider == Data.TTSApiProvider.RimTalkSame || _settings.ApiProvider == Data.TTSApiProvider.OpenAI)
                ? (activeConfig?.SelectedModel == "Custom" ? activeConfig.CustomModelName : activeConfig?.SelectedModel)
                : _settings.Model;
            Log.Message($"[RimAI.Voices] Model: {(effectiveModel ?? "(not set)")}");
            
            string baseUrl = _settings.ApiProvider == Data.TTSApiProvider.Custom 
                ? (_settings.CustomBaseUrl ?? "(not set)")
                : (_settings.ApiProvider == Data.TTSApiProvider.DeepSeek 
                    ? "https://api.deepseek.com" 
                    : "https://api.openai.com");
            Log.Message($"[RimAI.Voices] BaseUrl: {baseUrl}");
            
            Log.Message($"[RimAI.Voices] Credential source: {((_settings.ApiProvider == Data.TTSApiProvider.RimTalkSame || _settings.ApiProvider == Data.TTSApiProvider.OpenAI) ? "OPENAI_RIMAI" : "provider-specific setting")}");
            Log.Message("[RimAI.Voices] ==========================================");

            Log.Message("[RimAI.Voices] TTS Module initialized");
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
            
            Log.Message("[RimAI.Voices] Game loaded, resetting TTS state");
            Service.TTSService.StopAll(permanentShutdown: false);
        }

        public void OnGameExit()
        {
            Log.Message("[RimAI.Voices] Game exiting, full shutdown");
            
            Service.TTSService.StopAll(permanentShutdown: true);
            Service.AudioPlaybackService.FullReset(); // Then reset state
        }

        public bool IsActive => _settings?.EnableTTS ?? false;

        public TTSSettings Settings => _settings;
    }
}
