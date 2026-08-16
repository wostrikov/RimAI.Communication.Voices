using System.Threading.Tasks;
using Ustas.RimAI.Communication.Voices.Data;
using Verse;

namespace Ustas.RimAI.Communication.Voices.Service
{
    /// <summary>
    /// Prepares dialogue text using the canonical shared RimAI gameplay AI configuration.
    /// </summary>
    public static class InputPreProcessService
    {
        public static async Task<PreProcessResult> PreProcessAsync(string text, string targetLanguage, TTSSettings settings)
        {
            if (settings == null)
            {
                Log.Warning("[RimAI.Voices] preprocess settings is null");
                return null;
            }

            try
            {
                string promptTemplate = TTSConstant.GetTTSProcessingPrompt(settings);
                string language = string.IsNullOrWhiteSpace(targetLanguage)
                    ? VoiceSharedAiText.Language
                    : targetLanguage;
                string prompt = VoiceSharedAiText.SubstitutePrompt(promptTemplate, text)
                    .Replace("{language}", language)
                    .Replace("{text}", text ?? string.Empty);

                var (response, success) = await InputPreProcessClient.QueryAsync(prompt, text, settings);
                if (response == null)
                    return null;
                response.Text = CleanText(response.Text);

                if (success && !string.IsNullOrEmpty(response.Text))
                    return response;

                Log.Warning("[RimAI.Voices] Empty response from preprocess API");
                return null;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[RimAI.Voices] preprocess failed - {ex.Message}");
                return null;
            }
        }

        private static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            text = System.Text.RegularExpressions.Regex.Replace(
                        System.Text.RegularExpressions.Regex.Replace(
                            text.Normalize(System.Text.NormalizationForm.FormKC), @"\([^)]*\)", ""
                        )
                        , @"\s+", " "
                    ).Trim();

            if (TTSConfig.CurrentSupplier == TTSSettings.TTSSupplier.FishAudio)
            {
                text = text.Replace("[","(").Replace("]",")");
            }

            return text;
        }
    }
}
