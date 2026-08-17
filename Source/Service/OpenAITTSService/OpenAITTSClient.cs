using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Verse;

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

        static readonly HttpClient _http = new HttpClient();
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

                using var response = await _http.SendAsync(httpRequest, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    string error = response.Content != null ? await response.Content.ReadAsStringAsync() : string.Empty;
                    Log.Warning($"[RimAI.Voices] OpenAITTSClient: API returned {(int)response.StatusCode}: {Shorten(error)}");
                    return null;
                }

                byte[] audioData = await response.Content.ReadAsByteArrayAsync();
                if (audioData == null || audioData.Length == 0)
                {
                    Log.Warning("[RimAI.Voices] OpenAITTSClient: empty audio response");
                    return null;
                }

                return audioData;
            }
            catch (TaskCanceledException)
            {
                Log.Message("[RimAI.Voices] OpenAITTSClient: request cancelled");
                return null;
            }
            catch (HttpRequestException ex)
            {
                Log.Error($"[RimAI.Voices] OpenAITTSClient: network error - {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Voices] OpenAITTSClient: unexpected error - {ex.GetType().Name}: {ex.Message}");
                return null;
            }
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

                using var response = await _http.SendAsync(request, cancellationToken);
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
