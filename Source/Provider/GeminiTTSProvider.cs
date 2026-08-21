using System;
using System.Threading;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Voices.Service;
using Verse;

namespace Ustas.RimAI.Communication.Voices.Provider
{
    public class GeminiTTSProvider : ITTSProvider
    {
        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                byte[] audioData = await GeminiTTSClient.GenerateSpeechAsync(request, cancellationToken);
                return audioData;
            }
            catch (Exception ex)
            {
                Log.Error($"RimTalkTTS: GeminiTTS generation failed: {ex.Message}");
                return null;
            }
        }

        public void Shutdown()
        {
        }

        public bool IsApiKeyValid(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                return false;
            }

            return apiKey.StartsWith("AIza") && apiKey.Length >= 35;
        }
    }
}
