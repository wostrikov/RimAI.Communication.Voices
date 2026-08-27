using System;
using System.IO;
using Ustas.RimAI.Communication.Voices.Policy;

internal static class VoiceTextPreprocessAndShutdownTests
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

        string prompt = VoiceTextPreprocessPolicy.BuildPrompt(
            "Speak {language}: {text}",
            "uk",
            "Привіт");
        T(prompt == "Speak uk: Привіт", "prompt-fill");

        T(VoiceTextPreprocessPolicy.PrepareUserText("keep (aside)", false) == "keep (aside)", "brackets-off");
        T(VoiceTextPreprocessPolicy.PrepareUserText("keep (aside)", true) == "keep ...", "brackets-on");
        T(VoiceTextPreprocessPolicy.PrepareUserText("say [tag] now", true) == "say ... now", "square-brackets");

        string cleaned = VoiceTextPreprocessPolicy.CleanForTts("Hello   (whisper)  world", false);
        T(cleaned == "Hello world", "clean-parens-and-space");

        string fish = VoiceTextPreprocessPolicy.CleanForTts("say [emote]", true);
        T(fish == "say (emote)", "fish-audio-brackets");

        T(VoiceTextPreprocessPolicy.TryAccept("ok", true), "accept-success");
        T(!VoiceTextPreprocessPolicy.TryAccept("", true), "reject-empty");
        T(!VoiceTextPreprocessPolicy.TryAccept("ok", false), "reject-failed-query");

        var load = VoiceShutdownPolicy.ForLoad();
        T(load.StopAll && !load.PermanentShutdown && !load.FullResetPlayback, "load-plan");
        var exit = VoiceShutdownPolicy.ForExit();
        T(exit.StopAll && exit.PermanentShutdown && exit.FullResetPlayback, "exit-plan");

        string service = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "InputPreProcessService.cs.src"));
        T(service.Contains("VoiceTextPreprocessPolicy.BuildPrompt"), "service-builds-prompt");
        T(service.Contains("VoiceTextPreprocessPolicy.CleanForTts"), "service-cleans");
        T(service.Contains("VoiceTextPreprocessPolicy.TryAccept"), "service-accepts");

        string client = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "InputPreProcessClient.cs.src"));
        T(client.Contains("VoiceTextPreprocessPolicy.PrepareUserText"), "client-prepares-user-text");
        T(!client.Contains("private static string RemoveBrackets"), "client-no-local-brackets");

        string module = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TTSModule.cs.src"));
        T(module.Contains("VoiceShutdownPolicy.ForLoad()"), "module-load-plan");
        T(module.Contains("VoiceShutdownPolicy.ForExit()"), "module-exit-plan");
        T(module.Contains("touchUnityAudio: !plan.FullResetPlayback"), "exit-avoids-native-audio");

        string ttsService = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TTSService.cs.src"));
        T(ttsService.Contains("bool touchUnityAudio = true"), "stop-all-native-audio-policy");
        T(ttsService.Contains("if (touchUnityAudio)"), "stop-all-gates-native-audio");

        string composition = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "VoicesComposition.cs.src"));
        T(composition.Contains("TTSModule.Instance.OnGameExit()"), "composition-quit-calls-exit");
        return n;
    }
}
