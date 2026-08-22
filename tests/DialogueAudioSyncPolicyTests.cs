using System;
using System.IO;
using Ustas.RimAI.Communication.Voices.Policy;

internal static class DialogueAudioSyncPolicyTests
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

        T(DialogueAudioSyncPolicy.AllowDisplay(false, true, true), "inactive-allows");
        T(!DialogueAudioSyncPolicy.ShouldStartPlayback(false, false, false), "inactive-no-playback");
        T(!DialogueAudioSyncPolicy.AllowDisplay(true, true, false), "playing-blocks");
        T(!DialogueAudioSyncPolicy.AllowDisplay(true, false, true), "blocked-no-bubble");
        T(!DialogueAudioSyncPolicy.ShouldStartPlayback(true, false, true), "blocked-no-playback");
        T(DialogueAudioSyncPolicy.AllowDisplay(true, false, false), "ready-allows");
        T(DialogueAudioSyncPolicy.ShouldStartPlayback(true, false, false), "ready-starts-playback");

        string bridge = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TalkLifecycleBridge.cs.src"));
        T(bridge.Contains("DialogueAudioSyncPolicy.AllowDisplay"), "bridge-uses-policy");
        T(bridge.Contains("DialogueAudioSyncPolicy.ShouldStartPlayback"), "bridge-starts-from-policy");
        T(!bridge.Contains("if (AudioPlaybackService.IsCurrentlyPlaying())"), "bridge-no-inline-playing-gate");
        return n;
    }
}
