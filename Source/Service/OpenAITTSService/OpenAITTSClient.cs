using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Verse;
using Ustas.RimAI.Communication.Voices.Diagnostics;

namespace Ustas.RimAI.Communication.Voices.Service
{
    /// <summary>
    /// HTTP client for the OpenAI speech endpoint (POST /v1/audio/speech).
    /// The base URL stays configurable so Azure OpenAI and compatible gateways work too.
    /// </summary>
    public static class OpenAITTSClient
    {
        public const string DefaultBaseUrl = "https://api.openai.com/v1";

        /// <summary>Models known to accept free-form delivery instructions.</summary>
        public const string DefaultModel = "gpt-4o-mini-tts";

        public static readonly string[] KnownModels =
        {
            "gpt-4o-mini-tts",
            "tts-1",
            "tts-1-hd"
        };

        public static readonly string[] ResponseFormats =
        {
            "mp3",
            "wav",
            "opus",
            "aac",
            "flac"
        };

        // Two waits, not one. The server has HeaderTimeout to start answering - past that
        // the Edge fallback speaks the line instead, and the default 100 seconds left a
        // colonist silent that long. The audio then streams in about as fast as it is
        // spoken, so a long line legitimately takes longer than any fixed timeout short
        // enough for the first wait: a single 30-second cap on the whole exchange cut
        // such lines off mid-download ("no response within 30s" with 1 of 8 connections
        // in use, thrown from LoadIntoBufferAsync). The body is read as a stream and
        // abandoned only when it stops arriving for BodyIdleTimeout, or runs past
        // BodyTotalTimeout altogether.
        static readonly TimeSpan HeaderTimeout = TimeSpan.FromSeconds(30);
        static readonly TimeSpan BodyIdleTimeout = TimeSpan.FromSeconds(20);
        static readonly TimeSpan BodyTotalTimeout = TimeSpan.FromMinutes(2);
        static readonly HttpClient _http = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        static string _baseUrl = DefaultBaseUrl;

        public static void SetBaseUrl(string baseUrl)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
        }

        public static string GetBaseUrl() => _baseUrl;

        /// <summary>
        /// The legacy tts-1 family rejects the instructions field, so it is only sent to
        /// models that understand it.
        /// </summary>
        public static bool SupportsInstructions(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
                return false;
            return model.IndexOf("tts-1", StringComparison.OrdinalIgnoreCase) < 0;
        }

        public static async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Input))
                {
                    Log.Warning("[RimAI.Voices] OpenAITTSClient: input text is empty");
                    return null;
                }

                if (string.IsNullOrWhiteSpace(request.ApiKey))
                {
                    Log.Warning($"[RimAI.Voices] OpenAITTSClient: no credential, set {Data.OpenAITtsCredential.Variable}");
                    return null;
                }

                string model = string.IsNullOrWhiteSpace(request.Model) ? DefaultModel : request.Model;

                var body = new StringBuilder("{");
                AppendString(body, "model", model, first: true);
                AppendString(body, "input", request.Input);
                AppendString(body, "voice", string.IsNullOrWhiteSpace(request.Voice) ? "alloy" : request.Voice);
                AppendString(body, "response_format", NormalizeFormat(request.ResponseFormat));

                if (request.Speed > 0f && Math.Abs(request.Speed - 1.0f) > 0.01f)
                {
                    body.Append(",\"speed\":")
                        .Append(Math.Min(4.0f, Math.Max(0.25f, request.Speed)).ToString("F2", CultureInfo.InvariantCulture));
                }

                if (!string.IsNullOrWhiteSpace(request.Instructions) && SupportsInstructions(model))
                {
                    AppendString(body, "instructions", request.Instructions);
                }

                body.Append('}');

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/audio/speech")
                {
                    Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json")
                };
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
                // A fresh connection per line. Lines come minutes apart, and Mono will send the
                // next one down a kept-alive connection the other end has long since dropped,
                // then wait out the whole timeout for an answer that cannot come - which is
                // what every failure seen so far looked like: nothing, after an idle spell.
                httpRequest.Headers.ConnectionClose = true;

                RaiseConnectionLimit();
                HttpResponseMessage sent;
                using (var headerWait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    headerWait.CancelAfter(HeaderTimeout);
                    try
                    {
                        sent = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, headerWait.Token);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        Log.Error($"[RimAI.Voices] OpenAITTSClient: no response within {HeaderTimeout.TotalSeconds:0}s from {_baseUrl} ({ConnectionsInUse()})");
                        return null;
                    }
                }

                using var response = sent;
                if (!response.IsSuccessStatusCode)
                {
                    string error = response.Content != null ? await response.Content.ReadAsStringAsync() : string.Empty;
                    Log.Warning($"[RimAI.Voices] OpenAITTSClient: API returned {(int)response.StatusCode}: {Shorten(error)}");
                    return null;
                }

                byte[] audioData = await ReadBodyAsync(response, cancellationToken);
                if (audioData == null)
                {
                    return null;
                }

                if (audioData.Length == 0)
                {
                    Log.Warning("[RimAI.Voices] OpenAITTSClient: empty audio response");
                    return null;
                }

                return audioData;
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                ModuleLog.Message("[RimAI.Voices] OpenAITTSClient: request cancelled");
                return null;
            }
            catch (HttpRequestException ex)
            {
                Log.Error($"[RimAI.Voices] OpenAITTSClient: network error - {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Voices] OpenAITTSClient: {Describe(ex)}");
                return null;
            }
        }

        /// <summary>
        /// The audio as it streams in. Each read is raced against a delay rather than given
        /// a token, because Mono's network streams do not reliably honour cancellation; a
        /// read that loses the race is abandoned with the response, which closes it.
        /// </summary>
        static async Task<byte[]> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            using var stream = await response.Content.ReadAsStreamAsync();
            using var audio = new System.IO.MemoryStream();
            var buffer = new byte[16384];
            DateTime deadline = DateTime.UtcNow + BodyTotalTimeout;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Task<int> read = stream.ReadAsync(buffer, 0, buffer.Length);
                Task winner = await Task.WhenAny(read, Task.Delay(BodyIdleTimeout, cancellationToken));
                if (winner != read)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Log.Error($"[RimAI.Voices] OpenAITTSClient: audio from {_baseUrl} stopped arriving for "
                              + $"{BodyIdleTimeout.TotalSeconds:0}s after {audio.Length} bytes");
                    return null;
                }

                int count = await read;
                if (count <= 0)
                {
                    return audio.ToArray();
                }

                audio.Write(buffer, 0, count);
                if (DateTime.UtcNow > deadline)
                {
                    Log.Error($"[RimAI.Voices] OpenAITTSClient: audio from {_baseUrl} still streaming after "
                              + $"{BodyTotalTimeout.TotalSeconds:0}s ({audio.Length} bytes); line dropped");
                    return null;
                }
            }
        }

        /// <summary>
        /// Nobody cancels these requests but the client's own timeout, and Mono reports
        /// that either as a TaskCanceledException or as a WebException whose message
        /// says the request was cancelled - which reads as if something had stopped it
        /// on purpose, and used to be logged only with Detailed Logs on.
        /// </summary>
        static string Describe(Exception ex)
        {
            bool timedOut = ex is TaskCanceledException
                            || (ex is System.Net.WebException web && web.Status == System.Net.WebExceptionStatus.RequestCanceled);
            return timedOut
                ? $"no response within {HeaderTimeout.TotalSeconds:0}s from {_baseUrl} ({ConnectionsInUse()})"
                : $"unexpected error - {ex.GetType().Name}: {ex.Message}";
        }

        /// <summary>
        /// Mono lets a process hold two connections to one host by default, and these
        /// requests share api.openai.com with Memory's embeddings and anything else this
        /// modset sends there through HttpClient. A spoken line queued behind them for a free
        /// connection times out exactly as if OpenAI had never answered - which is what
        /// every failure so far looked like. The limit is raised for this endpoint only.
        /// </summary>
        static void RaiseConnectionLimit()
        {
            ServicePoint point = ServicePointManager.FindServicePoint(new Uri(_baseUrl));
            if (point.ConnectionLimit < 8)
                point.ConnectionLimit = 8;
        }

        /// <summary>The endpoint's connections in use, so a timeout says whether it waited for one.</summary>
        static string ConnectionsInUse()
        {
            ServicePoint point = ServicePointManager.FindServicePoint(new Uri(_baseUrl));
            return $"{point.CurrentConnections} of {point.ConnectionLimit} connections in use";
        }

        /// <summary>
        /// Ask the account which speech models it can actually use. Returns an empty list
        /// when the endpoint is unreachable so callers can keep the built-in list.
        /// </summary>
        public static async Task<List<string>> ListSpeechModelsAsync(string apiKey, CancellationToken cancellationToken = default)
        {
            var result = new List<string>();

            try
            {
                if (string.IsNullOrWhiteSpace(apiKey))
                    return result;

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/models");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                // The shared client no longer has a timeout of its own; the model list is small.
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                wait.CancelAfter(HeaderTimeout);
                using var response = await _http.SendAsync(request, wait.Token);
                if (!response.IsSuccessStatusCode)
                {
                    string error = response.Content != null ? await response.Content.ReadAsStringAsync() : string.Empty;
                    Log.Warning($"[RimAI.Voices] OpenAITTSClient: model list returned {(int)response.StatusCode}: {Shorten(error)}");
                    return result;
                }

                string json = await response.Content.ReadAsStringAsync();
                foreach (Match match in Regex.Matches(json ?? string.Empty, "\"id\"\\s*:\\s*\"(?<id>[^\"]+)\""))
                {
                    string id = match.Groups["id"].Value;
                    if (IsSpeechModel(id) && !result.Contains(id))
                        result.Add(id);
                }

                result.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Voices] OpenAITTSClient: failed to list models - {ex.Message}");
            }

            return result;
        }

        static bool IsSpeechModel(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            // Speech synthesis models are named tts-* or *-tts; transcription models
            // (whisper, *-transcribe) share the audio family but cannot synthesize.
            if (id.IndexOf("transcribe", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            return id.StartsWith("tts-", StringComparison.OrdinalIgnoreCase)
                   || id.EndsWith("-tts", StringComparison.OrdinalIgnoreCase);
        }

        static string NormalizeFormat(string format)
        {
            if (string.IsNullOrWhiteSpace(format))
                return "mp3";

            string trimmed = format.Trim().ToLowerInvariant();
            return ResponseFormats.Contains(trimmed) ? trimmed : "mp3";
        }

        static void AppendString(StringBuilder body, string name, string value, bool first = false)
        {
            if (!first)
                body.Append(',');
            body.Append('"').Append(name).Append("\":\"").Append(Escape(value)).Append('"');
        }

        static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        static string Shorten(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            string single = value.Replace("\r", " ").Replace("\n", " ");
            return single.Length <= 400 ? single : single.Substring(0, 400) + "…";
        }
    }
}
