using System.Text;
using System.Text.RegularExpressions;

namespace Ustas.RimAI.Communication.Voices.Policy
{
    /// <summary>
    /// Authoritative LLM preprocess/translate transforms before TTS.
    /// Hosts own the network call; this type owns prompt fill, bracket
    /// stripping, TTS cleanup, and accept/reject of the model result.
    /// </summary>
    public static class VoiceTextPreprocessPolicy
    {
        public static string BuildPrompt(string template, string language, string text)
        {
            return (template ?? string.Empty)
                .Replace("{language}", language ?? string.Empty)
                .Replace("{text}", text ?? string.Empty);
        }

        public static string PrepareUserText(string text, bool removeBrackets)
        {
            if (!removeBrackets)
                return text ?? string.Empty;
            return RemoveBracketedSpans(text);
        }

        public static string RemoveBracketedSpans(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            text = Regex.Replace(text, @"\([^()]*\)", "...");
            text = Regex.Replace(text, @"\uff08[^\uff08\uff09]*\uff09", "...");
            text = Regex.Replace(text, @"\[[^\[\]]*\]", "...");
            text = Regex.Replace(text, @"\u3010[^\u3010\u3011]*\u3011", "...");
            text = Regex.Replace(text, @"\*[^*]*\*", "...");
            text = Regex.Replace(text, @"<[^<>]*>", "...");
            text = Regex.Replace(text, @"/[^/]*/", "...");
            text = Regex.Replace(text, @"\\[^\\]*\\", "...");
            text = Regex.Replace(text, @"#[^#]*#", "...");
            return text;
        }

        public static string CleanForTts(string text, bool fishAudioSupplier)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            text = Regex.Replace(
                Regex.Replace(
                    text.Normalize(NormalizationForm.FormKC),
                    @"\([^)]*\)",
                    ""),
                @"\s+",
                " ").Trim();

            if (fishAudioSupplier)
                text = text.Replace("[", "(").Replace("]", ")");

            return text;
        }

        public static bool TryAccept(string cleanedText, bool querySucceeded)
        {
            return querySucceeded && !string.IsNullOrEmpty(cleanedText);
        }
    }
}
