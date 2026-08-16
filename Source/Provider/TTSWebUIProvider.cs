using System;
using System.Threading;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Voices.Service;
using Verse;

namespace Ustas.RimAI.Communication.Voices.Provider
{
    /// <summary>
    /// Provider wrapper for TTS-WebUI (rsxdalv/TTS-WebUI).
    /// TTS-WebUI is a comprehensive TTS WebUI that supports many models including:
    /// Bark, Tortoise, XTTSv2, CosyVoice, Kokoro, Parler TTS, F5-TTS, GPT-SoVITS, and more.
    /// It provides an OpenAI-compatible API at http://localhost:7778/v1/audio/speech
    /// </summary>
    public class TTSWebUIProvider : ITTSProvider
    {
        private string _baseUrl = "http://localhost:7778/v1";
        
        public void SetBaseUrl(string baseUrl)
        {
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                _baseUrl = baseUrl.TrimEnd('/');
                TTSWebUIClient.SetBaseUrl(_baseUrl);
            }
        }
        
        public string GetBaseUrl()
        {
            return _baseUrl;
        }

        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                byte[] audioData = await TTSWebUIClient.GenerateSpeechAsync(request, cancellationToken);
                return audioData;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Voices] TTSWebUI generation failed: {ex.Message}");
                return null;
            }
        }

        public void Shutdown()
        {
            // TTS-WebUI uses shared HttpClient, no special cleanup needed
        }

        public bool IsApiKeyValid(string apiKey)
        {
            // TTS-WebUI API key is optional - the OpenAI API extension can be configured
            // to require an API key or not. We accept any key including empty.
            // Return true to allow usage without API key configuration.
            return true;
        }
    }
}
