namespace Ustas.RimAI.Communication.Voices.Policy
{
    public readonly struct VoiceShutdownPlan
    {
        public VoiceShutdownPlan(bool stopAll, bool permanentShutdown, bool fullResetPlayback)
        {
            StopAll = stopAll;
            PermanentShutdown = permanentShutdown;
            FullResetPlayback = fullResetPlayback;
        }

        public bool StopAll { get; }
        public bool PermanentShutdown { get; }
        public bool FullResetPlayback { get; }
    }

    /// <summary>
    /// Authoritative audio-pipeline shutdown. Game exit is a permanent StopAll
    /// plus playback FullReset. Game load only stops in-flight work.
    /// </summary>
    public static class VoiceShutdownPolicy
    {
        public static VoiceShutdownPlan ForLoad() =>
            new VoiceShutdownPlan(stopAll: true, permanentShutdown: false, fullResetPlayback: false);

        public static VoiceShutdownPlan ForExit() =>
            new VoiceShutdownPlan(stopAll: true, permanentShutdown: true, fullResetPlayback: true);
    }
}
