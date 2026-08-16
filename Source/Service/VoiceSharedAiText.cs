using Ustas.RimAI.Core.Configuration;

namespace Ustas.RimAI.Communication.Voices.Service
{
    /// <summary>
    /// Voices consumes the canonical shared gameplay text-AI snapshot for
    /// preprocessing. TTS engine/voice settings stay module-owned.
    /// </summary>
    public static class VoiceSharedAiText
    {
        public static SharedTextAiSnapshot Snapshot =>
            SharedTextAiAccess.Current ?? SharedTextAiSnapshot.Inactive();

        public static string Language => Snapshot.Language;

        public static string Provider => Snapshot.ProviderId;

        public static string EffectiveModel =>
            Snapshot.HasActive ? Snapshot.EffectiveModel : string.Empty;

        public static string SubstitutePrompt(string template, string text)
        {
            return (template ?? string.Empty)
                .Replace("{language}", Language)
                .Replace("{text}", text ?? string.Empty);
        }
    }
}
