using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Voices.Diagnostics;
using Ustas.RimAI.Core.Storage;
using Verse;

namespace Ustas.RimAI.Communication.Voices.Service
{
    /// <summary>
    /// Starts the local Ukrainian speech server when the TTS-WebUI supplier points at it
    /// and nothing answers there.
    ///
    /// The server - Piper, tts_uk and StyleTTS2 behind an OpenAI-style endpoint - is
    /// tools/voices/local_tts_server.py, installed with its own Python environment into
    /// %LOCALAPPDATA%/Ustas/LocalTTS by tools/voices/setup-local-tts.ps1. It is started
    /// with this process's id and exits when the game does, so nothing is left holding
    /// the GPU. Loading the three engines takes about half a minute; a line asked for in
    /// that time waits for them rather than failing.
    /// </summary>
    internal static class LocalTtsServer
    {
        private const int Port = 7778;
        private static readonly TimeSpan ReadyTimeout = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(1);
        private static readonly HttpClient Health = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        private static readonly object Gate = new object();
        private static Task<bool> _starting;
        private static DateTime _failedAt = DateTime.MinValue;

        private static string Root => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ustas", "LocalTTS");

        /// <summary>True when <paramref name="baseUrl"/> is this machine's local server.</summary>
        internal static bool Serves(string baseUrl)
        {
            return Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri) && uri.IsLoopback && uri.Port == Port;
        }

        /// <summary>
        /// Completes true once the server at <paramref name="baseUrl"/> answers ready, starting
        /// it if it is ours to start. Any other address is somebody else's server and is not
        /// waited for.
        /// </summary>
        internal static Task<bool> EnsureRunningAsync(string baseUrl)
        {
            if (!Serves(baseUrl))
                return Task.FromResult(true);

            lock (Gate)
            {
                bool retry = _starting != null && _starting.IsCompleted && !_starting.Result
                             && DateTime.UtcNow - _failedAt > RetryAfterFailure;
                if (_starting == null || retry)
                    _starting = StartAsync();
                return _starting;
            }
        }

        private static async Task<bool> StartAsync()
        {
            string state = await HealthAsync();
            if (state == null)
            {
                if (!Launch())
                    return Failed();
                ModuleLog.Message("[RimAI.Voices] Local TTS: started the server, waiting for its engines to load");
            }

            DateTime deadline = DateTime.UtcNow + ReadyTimeout;
            while (DateTime.UtcNow < deadline)
            {
                if (state == "ready")
                {
                    ModuleLog.Message("[RimAI.Voices] Local TTS: ready");
                    return true;
                }
                if (state != null && state.StartsWith("failed", StringComparison.Ordinal))
                {
                    Log.Error($"[RimAI.Voices] Local TTS: the server could not load its engines - {state}."
                              + $" Its log is {Path.Combine(Root, "server.log")}");
                    return Failed();
                }
                await Task.Delay(1000);
                state = await HealthAsync();
            }

            Log.Error($"[RimAI.Voices] Local TTS: not ready after {ReadyTimeout.TotalMinutes:0} minutes."
                      + $" Its log is {Path.Combine(Root, "server.log")}");
            return Failed();
        }

        private static bool Failed()
        {
            _failedAt = DateTime.UtcNow;
            return false;
        }

        /// <summary>Where the server writes its own account of each request.</summary>
        internal static string LogPath => Path.Combine(Root, "server.log");

        /// <summary>The server's /health answer, or null when nothing answers.</summary>
        internal static async Task<string> HealthAsync()
        {
            try
            {
                return (await Health.GetStringAsync($"http://127.0.0.1:{Port}/health")).Trim();
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (TaskCanceledException)
            {
                return null;
            }
        }

        private static bool Launch()
        {
            string python = Path.Combine(Root, "venv", "Scripts", "pythonw.exe");
            string script = Path.Combine(Root, "local_tts_server.py");
            if (!LocalStorage.Current.FileExists(python) || !LocalStorage.Current.FileExists(script))
            {
                Log.Warning("[RimAI.Voices] Local TTS is not installed: nothing answers on port "
                            + $"{Port} and {Root} has no server. Run tools\\voices\\setup-local-tts.ps1.");
                return false;
            }

            var start = new ProcessStartInfo
            {
                FileName = python,
                Arguments = $"\"{script}\" --port {Port} --parent-pid {Process.GetCurrentProcess().Id}"
                            + $" --log \"{Path.Combine(Root, "server.log")}\"",
                WorkingDirectory = Root,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            try
            {
                using (Process.Start(start))
                {
                }
                return true;
            }
            catch (Win32Exception exception)
            {
                Log.Error($"[RimAI.Voices] Local TTS: could not start {python} - {exception.Message}");
                return false;
            }
            catch (InvalidOperationException exception)
            {
                Log.Error($"[RimAI.Voices] Local TTS: could not start {python} - {exception.Message}");
                return false;
            }
        }
    }
}
