using System.Threading;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Voices.Service;
using Ustas.RimAI.Communication.Voices.Service.EdgeTTSService;

namespace Ustas.RimAI.Communication.Voices.Provider
{
    /// <summary>
    /// Provider wrapper for Edge-TTS (Microsoft Edge's free TTS service)
    /// No API key required
    /// </summary>
    public class EdgeTTSProvider : ITTSProvider
    {
        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            // Edge-TTS doesn't need API key - it's free
            return await EdgeTTSClient.GenerateSpeechAsync(request, cancellationToken);
        }

        public void Shutdown()
        {
            // HTTP client doesn't need cleanup
        }

        public bool IsApiKeyValid(string apiKey)
        {
            // Edge-TTS doesn't require API key - always valid
            return true;
        }
    }
}
