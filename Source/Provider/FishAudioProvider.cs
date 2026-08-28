using System.Threading;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Voices.Service;
using Ustas.RimAI.Communication.Voices.Service.FishAudioService;

namespace Ustas.RimAI.Communication.Voices.Provider
{
    /// <summary>
    /// ITTSProvider implementation that delegates to FishAudioTTSClient
    /// </summary>
    public class FishAudioProvider : ITTSProvider
    {
        // Whether this provider ever reached the client. Kept here rather than on
        // the client because reading a field of that type is itself enough to run
        // its initialiser, which is the thing being avoided.
        bool _used;

        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            _used = true;
            // Delegate to existing FishAudio client which accepts parameter list
            return await FishAudioTTSClient.GenerateSpeechAsync(
                request,
                cancellationToken);
        }

        public void Shutdown()
        {
            // Nothing was started, so there is nothing to stop. Calling through
            // anyway would load the client during Application.quitting, which is
            // how a session that never spoke a word still ran a filesystem search
            // and a Verse lookup inside Unity's teardown.
            if (!_used)
            {
                return;
            }

            FishAudioTTSClient.ShutdownServer();
        }

        public bool IsApiKeyValid(string apiKey)
        {
            return !string.IsNullOrWhiteSpace(apiKey);
        }
    }
}
