using System;
using System.Threading;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Voices.Data;
using Ustas.RimAI.Communication.Voices.Service;
using Verse;

namespace Ustas.RimAI.Communication.Voices.Provider
{
    /// <summary>
    /// Provider wrapper for OpenAI speech synthesis.
    /// The credential comes from the OPENAI_RIMAI_TTS environment variable.
    /// </summary>
    public class OpenAITTSProvider : ITTSProvider
    {
        string _baseUrl = OpenAITTSClient.DefaultBaseUrl;

        public void SetBaseUrl(string baseUrl)
        {
            // The supplier region slot is shared with Azure regions, so anything that is
            // not an absolute endpoint falls back to the public OpenAI base URL.
            bool usable = !string.IsNullOrWhiteSpace(baseUrl)
                          && baseUrl.TrimStart().StartsWith("http", StringComparison.OrdinalIgnoreCase);

            _baseUrl = usable ? baseUrl.Trim().TrimEnd('/') : OpenAITTSClient.DefaultBaseUrl;
            OpenAITTSClient.SetBaseUrl(_baseUrl);
        }

        public string GetBaseUrl() => _baseUrl;

        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                return await OpenAITTSClient.GenerateSpeechAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Voices] OpenAI generation failed: {ex.Message}");
                return null;
            }
        }

        public void Shutdown()
        {
            // Shared HttpClient, nothing to release.
        }

        public bool IsApiKeyValid(string apiKey)
        {
            return !string.IsNullOrWhiteSpace(apiKey) || OpenAITtsCredential.Present;
        }
    }
}
