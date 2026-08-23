using System;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Voices.Data;
using Ustas.RimAI.Communication.Voices.Policy;
using Ustas.RimAI.Communication.Client;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Util;
using Verse;

namespace Ustas.RimAI.Communication.Voices.Service
{
    public class PreProcessResult
    {
        public string Text;
        public string Emotion;
    }

    [Serializable]
    public class PreProcessResultJson
    {
        public string text;
        public string emotion;
    }

    /// <summary>
    /// LLM client for Voices text preprocessing. Always uses the canonical
    /// shared RimAI gameplay text-AI configuration.
    /// </summary>
    public static class InputPreProcessClient
    {
        public static async Task<(PreProcessResult response, bool success)> QueryAsync(string prompt, string text, TTSSettings settings)
        {
            if (settings == null)
            {
                Log.Warning("[RimAI.Voices] SimpleLLMClient: settings is null");
                return (null, false);
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                Log.Warning("[RimAI.Voices] Empty prompt provided to SimpleLLMClient");
                return (null, false);
            }

            return await QueryViaSharedConfigAsync(prompt, text, settings);
        }

        private static async Task<(PreProcessResult response, bool success)> QueryViaSharedConfigAsync(
            string prompt, string text, TTSSettings settings)
        {
            text = VoiceTextPreprocessPolicy.PrepareUserText(text, settings.RemoveBracketsInPreProcess);

            IAIClient client = await AIClientFactory.GetAIClientAsync();
            if (client == null)
            {
                Log.Warning("[RimAI.Voices] Shared RimAI gameplay AI configuration is not available");
                return (null, false);
            }

            Payload payload = await client.GetChatCompletionAsync(
                new System.Collections.Generic.List<(Role role, string message)>
                {
                    (Role.System, prompt)
                },
                new System.Collections.Generic.List<(Role role, string message)>
                {
                    (Role.User, text)
                });

            if (payload == null || !string.IsNullOrEmpty(payload.ErrorMessage) || string.IsNullOrWhiteSpace(payload.Response))
                return (null, false);

            try
            {
                var parsed = JsonUtil.DeserializeFromJson<PreProcessResultJson>(payload.Response);
                return (new PreProcessResult { Text = parsed.text, Emotion = parsed.emotion }, true);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Voices] Failed to parse structured preprocessing response: {ex.Message}");
                return (null, false);
            }
        }
    }
}
