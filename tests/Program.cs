using System;

internal static class Program
{
    public static int Main()
    {
        int n = TtsProviderOrchestrationTests.Run()
            + DialogueAudioSyncPolicyTests.Run()
            + VoiceTextPreprocessAndShutdownTests.Run()
            + PawnVoiceBindingTests.Run()
            + TtsSettingsInstanceTests.Run();
        Console.WriteLine("VOICES_FOCUSED_TESTS_OK passed=" + n);
        Console.WriteLine("TESTS total=" + n + " failed=0");
        return 0;
    }
}
