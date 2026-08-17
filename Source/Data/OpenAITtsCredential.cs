using System;

namespace Ustas.RimAI.Communication.Voices.Data
{
    /// <summary>
    /// Voicing credential domain. Kept separate from the gameplay text credential
    /// (OPENAI_RIMAI) and from translation tooling (OPENAI_RIMTRANS): there is no
    /// fallback between the domains.
    /// </summary>
    public static class OpenAITtsCredential
    {
        public const string Variable = "OPENAI_RIMAI_TTS";

        public static string Resolve()
        {
            try
            {
                return (Environment.GetEnvironmentVariable(Variable) ?? string.Empty).Trim();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static bool Present => Resolve().Length > 0;

        public static string Display => Present ? Variable + " ✓" : Variable + " ✗";
    }
}
