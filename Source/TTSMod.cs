using Ustas.RimAI.Core.Modules;
using Verse;

namespace Ustas.RimAI.Communication.Voices
{
    public class TTSMod : Mod
    {
        public static System.Diagnostics.Stopwatch AppStopwatch = null;

        public TTSMod(ModContentPack content) : base(content)
        {
            GetSettings<Data.TTSSettings>();
            AppStopwatch = System.Diagnostics.Stopwatch.StartNew();
            RimAIModuleRegistry.Current.Register(
                new RimAIModuleDescriptor(
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
            RimAISettingsNavigation.Open("communication", "voices");
            var settings = GetSettings<Data.TTSSettings>();
            UI.SettingsUI.DrawTTSSettings(inRect, settings);
        }
    }
}
