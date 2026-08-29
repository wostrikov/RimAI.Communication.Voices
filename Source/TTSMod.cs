using Ustas.RimAI.Core.Handshake;
using Ustas.RimAI.Core.Modules;
using Verse;

namespace Ustas.RimAI.Communication.Voices
{
    public class TTSMod : Mod
    {
        public const string HandshakeModuleVersion = "1.0.0";
        public static System.Diagnostics.Stopwatch AppStopwatch = null;

        /// <summary>
        /// The settings instance RimWorld loaded from disk, published the moment
        /// it exists.
        ///
        /// RimWorld registers a Mod with LoadedModManager only after its
        /// constructor returns. Anything started from inside this constructor -
        /// and the handshake below starts the whole module - therefore gets null
        /// from LoadedModManager.GetMod and silently falls back to a fresh
        /// TTSSettings, whose EnableTTS is false and whose Supplier is FishAudio
        /// regardless of what the player chose. That is the whole of the "talk
        /// queued but the TTS module is not active" report: speech switched on
        /// in the settings panel, and a module holding a different object that
        /// says it is off.
        /// </summary>
        internal static Data.TTSSettings LoadedSettings { get; private set; }

        public TTSMod(ModContentPack content) : base(content)
        {
            LoadedSettings = GetSettings<Data.TTSSettings>();
            AppStopwatch = System.Diagnostics.Stopwatch.StartNew();
            RimAiHandshake.TryActivate(
                RimAiHandshakeDescriptor.Current(
                    RimAiModuleIds.Voices,
                    HandshakeModuleVersion,
                    isOptional: true),
                VoicesComposition.Current.Start);
        }

        public override string SettingsCategory()
        {
            return Content?.Name ?? "RimAI.Communication.Voices";
        }

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            RimAISettingsNavigation.Open("communication", "voices");
            var settings = GetSettings<Data.TTSSettings>();
            UI.SettingsUI.DrawTTSSettings(inRect, settings);
        }
    }
}
