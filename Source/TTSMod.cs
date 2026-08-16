using Verse;

namespace Ustas.RimAI.Communication.Voices
{
    /// <summary>
    /// Mod class for TTS module to handle settings
    /// </summary>
    public class TTSMod : Mod
    {
        public static System.Diagnostics.Stopwatch AppStopwatch = null;

        public TTSMod(ModContentPack content) : base(content)
        {
            // Settings are automatically loaded by Verse framework
            GetSettings<Data.TTSSettings>();
            AppStopwatch = System.Diagnostics.Stopwatch.StartNew();
            Ustas.RimAI.Core.Modules.RimAIModuleRegistry.Current.Register(
                new Ustas.RimAI.Core.Modules.RimAIModuleDescriptor(
                    "voices",
                    "RimAI.Communication.Voices",
                    "RimAI.Communication.Voices",
                    "Communication",
                    "RimAI.Communication"));
        }

        public override string SettingsCategory()
        {
            return Content?.Name ?? "RimAI.Communication.Voices";
        }

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            HarmonyLib.AccessTools.TypeByName("Ustas.RimAI.Core.Modules.RimAISettingsNavigation")
                ?.GetMethod("Open")
                ?.Invoke(null, new object[] { "communication", "voices" });
            var settings = GetSettings<Data.TTSSettings>();
            UI.SettingsUI.DrawTTSSettings(inRect, settings);
        }
    }
}