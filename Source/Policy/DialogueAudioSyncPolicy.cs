namespace Ustas.RimAI.Communication.Voices.Policy
{
    /// <summary>
    /// Authoritative bubble/audio ordering. A line may display only after TTS
    /// is inactive or the dialogue is unblocked and no other clip is playing.
    /// </summary>
    public static class DialogueAudioSyncPolicy
    {
        public static bool AllowDisplay(bool ttsActive, bool audioPlaying, bool dialogueBlocked)
        {
            if (!ttsActive)
                return true;
            if (audioPlaying)
                return false;
            return !dialogueBlocked;
        }

        public static bool ShouldStartPlayback(bool ttsActive, bool audioPlaying, bool dialogueBlocked)
        {
            return ttsActive && AllowDisplay(true, audioPlaying, dialogueBlocked);
        }
    }
}
