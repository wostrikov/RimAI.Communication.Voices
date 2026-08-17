using CoreVoices = Ustas.RimAI.Core.Voices;

namespace Ustas.RimAI.Communication.Voices.Data
{
    /// <summary>
    /// RimWorld-facing wrapper over the Core credential resolver. The domain rules
    /// (no fallback to the gameplay or translation credentials) live in Core.
    /// </summary>
    public static class OpenAITtsCredential
    {
        public const string Variable = CoreVoices.TtsCredentialResolver.Canonical;

        public static string Resolve() => CoreVoices.TtsCredentialResolver.Resolve().Value ?? string.Empty;

        public static bool Present => CoreVoices.TtsCredentialResolver.Resolve().Present;

        public static string Display => CoreVoices.TtsCredentialResolver.Resolve().Display;
    }
}
