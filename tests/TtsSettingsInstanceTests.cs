using System;
using System.IO;

/// <summary>
/// The settings instance the module runs on.
///
/// TTSModule.Initialize used to ask LoadedModManager.GetMod for the settings.
/// It runs from inside the TTSMod constructor by way of the handshake, and
/// RimWorld registers a Mod with LoadedModManager only after that constructor
/// returns - so the lookup returned null every time and the fallback installed
/// a fresh TTSSettings. Fresh means EnableTTS false and Supplier FishAudio,
/// whatever the player had chosen.
///
/// It was silent for as long as nothing reported it. The log said "TTS provider
/// set to FishAudio" while the saved settings said OpenAI, and every generated
/// line produced "talk queued but the TTS module is not active" - seventeen of
/// them in one twenty-five minute session with speech switched on in the panel.
///
/// These are source-shape assertions because the failure is an ordering one:
/// reproducing it needs RimWorld's own mod-loading sequence, and what can be
/// checked here is that the code no longer depends on that sequence.
/// </summary>
internal static class TtsSettingsInstanceTests
{
    public static int Run()
    {
        int n = 0;
        void T(bool x, string s)
        {
            if (!x)
                throw new Exception("FAILED " + s);
            n++;
        }

        string mod = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TTSMod.cs.src"));
        string module = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TTSModule.cs.src"));

        // The constructor is the only place the loaded instance is reachable, so
        // it has to publish it, and before it starts anything that will want it.
        T(mod.Contains("LoadedSettings = GetSettings<Data.TTSSettings>();"), "mod-publishes-loaded-settings");
        T(mod.IndexOf("LoadedSettings = GetSettings") < mod.IndexOf("RimAiHandshake.TryActivate"),
            "mod-publishes-before-starting-the-module");

        // And the module has to prefer it over the lookup that cannot work yet.
        T(module.Contains("TTSMod.LoadedSettings"), "module-prefers-published-settings");
        int published = module.IndexOf("TTSMod.LoadedSettings", StringComparison.Ordinal);
        int lookup = module.IndexOf("LoadedModManager.GetMod", StringComparison.Ordinal);
        T(published >= 0 && (lookup < 0 || published < lookup), "module-asks-the-lookup-second-at-most");

        // Falling back to defaults means every choice the player made is being
        // ignored. That is a problem to report, not chatter to hide.
        T(module.Contains("RimAiLog.Warning"), "module-reports-running-on-defaults");

        return n;
    }
}
