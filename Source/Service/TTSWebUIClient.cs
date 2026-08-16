using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Verse;

namespace Ustas.RimAI.Communication.Voices.Service
{
    /// <summary>
    /// HTTP client for TTS-WebUI (rsxdalv/TTS-WebUI).
    /// TTS-WebUI provides an OpenAI-compatible API endpoint at /v1/audio/speech.
    /// Default endpoint: http://localhost:7778/v1/audio/speech
    /// Supported models include Bark, Tortoise, XTTSv2, CosyVoice, Kokoro, Parler TTS, F5-TTS, etc.
    /// </summary>
    public static class TTSWebUIClient
    {
        private static readonly HttpClient _http = new HttpClient();
        
        // Default base URL for TTS-WebUI OpenAI-compatible API
        private const string DefaultBaseUrl = "http://localhost:7778/v1";
        
        // Cache for base URL to allow runtime configuration
        private static string _baseUrl = DefaultBaseUrl;
        
        /// <summary>
        /// Set custom base URL for TTS-WebUI API
        /// </summary>
        public static void SetBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                _baseUrl = DefaultBaseUrl;
            }
            else
            {
                // Normalize: remove trailing slash
                _baseUrl = baseUrl.TrimEnd('/');
            }
        }
        
        /// <summary>
        /// Get current base URL
        /// </summary>
        public static string GetBaseUrl()
        {
            return _baseUrl;
        }

        /// <summary>
        /// Generate speech using TTS-WebUI OpenAI-compatible API.
        /// Endpoint: POST /v1/audio/speech
        /// </summary>
        public static async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Input))
                {
                    Log.Warning("[RimAI.Voices] TTSWebUIClient: Input text is empty");
                    return null;
                }

                string url = $"{_baseUrl}/audio/speech";
                
                // Build OpenAI-compatible request body
                // TTS-WebUI supports: model, input, voice, speed, response_format
                var requestBody = new Dictionary<string, object>
                {
                    { "input", request.Input },
                    { "voice", request.Voice ?? "default" }
                };
                
                // Add model if specified (TTS-WebUI supports various models via extensions)
                if (!string.IsNullOrWhiteSpace(request.Model))
                {
                    requestBody["model"] = request.Model;
                }
                
                // Add speed if not default
                if (request.Speed > 0 && Math.Abs(request.Speed - 1.0f) > 0.01f)
                {
                    requestBody["speed"] = request.Speed;
                }
                
                // Response format - prefer mp3 for smaller size, fallback to wav
                requestBody["response_format"] = "mp3";

                string jsonRequest = SerializeRequestBody(requestBody);
                
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
                httpRequest.Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                
                // Add API key if provided (TTS-WebUI OpenAI API extension supports optional API key)
                if (!string.IsNullOrWhiteSpace(request.ApiKey))
                {
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
                }
                
                using var response = await _http.SendAsync(httpRequest, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = response.Content != null ? await response.Content.ReadAsStringAsync() : string.Empty;
                    Log.Warning($"[RimAI.Voices] TTSWebUIClient: API returned {response.StatusCode}: {errorContent}");
                    return null;
                }
                
                // Read audio bytes directly from response
                byte[] audioData = await response.Content.ReadAsByteArrayAsync();
                
                if (audioData == null || audioData.Length == 0)
                {
                    Log.Warning("[RimAI.Voices] TTSWebUIClient: Empty audio response");
                    return null;
                }
                
                return audioData;
            }
            catch (HttpRequestException ex)
            {
                Log.Error($"[RimAI.Voices] TTSWebUIClient: Network error - {ex.Message}. Make sure TTS-WebUI is running at {_baseUrl}");
                return null;
            }
            catch (TaskCanceledException)
            {
                Log.Message("[RimAI.Voices] TTSWebUIClient: Request cancelled");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Voices] TTSWebUIClient: Unexpected error - {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// List available voices from TTS-WebUI (if supported by the active extension)
        /// GET /v1/audio/voices or /v1/voices
        /// </summary>
        public static async Task<List<(string id, string name)>> ListVoicesAsync(string apiKey = null)
        {
            var result = new List<(string id, string name)>();
            
            try
            {
                // Try the OpenAI-compatible voices endpoint
                string url = $"{_baseUrl}/audio/voices";
                
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                }
                
                using var response = await _http.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    // Some TTS-WebUI setups may not have this endpoint
                    Log.Message($"[RimAI.Voices] TTSWebUIClient: Voices endpoint not available ({response.StatusCode})");
                    return result;
                }
                
                string responseText = await response.Content.ReadAsStringAsync();
                
                // Try to parse voice list from JSON response
                // Expected format: {"voices": [{"id": "...", "name": "..."}, ...]} or similar
                var voices = ExtractVoicesFromJson(responseText);
                result.AddRange(voices);
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Voices] TTSWebUIClient: Failed to list voices - {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// Check if TTS-WebUI server is reachable
        /// </summary>
        public static async Task<bool> CheckConnectionAsync()
        {
            try
            {
                // Try health check or models endpoint
                string url = $"{_baseUrl}/models";
                
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await _http.SendAsync(request);
                
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Simple JSON serialization for request body
        /// </summary>
        private static string SerializeRequestBody(Dictionary<string, object> body)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            
            bool first = true;
            foreach (var kvp in body)
            {
                if (!first) sb.Append(",");
                first = false;
                
                sb.Append($"\"{kvp.Key}\":");
                
                if (kvp.Value is string str)
                {
                    sb.Append($"\"{EscapeJsonString(str)}\"");
                }
                else if (kvp.Value is float f)
                {
                    sb.Append(f.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                }
                else if (kvp.Value is double d)
                {
                    sb.Append(d.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                }
                else if (kvp.Value is int i)
                {
                    sb.Append(i);
                }
                else if (kvp.Value is bool b)
                {
                    sb.Append(b ? "true" : "false");
                }
                else
                {
                    sb.Append($"\"{kvp.Value}\"");
                }
            }
            
            sb.Append("}");
            return sb.ToString();
        }
        
        /// <summary>
        /// Escape special characters in JSON string
        /// </summary>
        private static string EscapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            
            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
        
        /// <summary>
        /// Extract voices from JSON response (basic parsing)
        /// </summary>
        private static List<(string id, string name)> ExtractVoicesFromJson(string json)
        {
            var result = new List<(string id, string name)>();
            
            if (string.IsNullOrWhiteSpace(json)) return result;
            
            try
            {
                // Simple regex-based extraction for voice id/name pairs
                // Expected format variations:
                // {"voices": [{"id": "voice1", "name": "Voice 1"}, ...]}
                // {"data": [{"id": "voice1", "name": "Voice 1"}, ...]}
                // or just an array: [{"id": "voice1", "name": "Voice 1"}, ...]
                
                var idPattern = new System.Text.RegularExpressions.Regex("\"id\"\\s*:\\s*\"([^\"]+)\"");
                var namePattern = new System.Text.RegularExpressions.Regex("\"name\"\\s*:\\s*\"([^\"]+)\"");
                
                var idMatches = idPattern.Matches(json);
                var nameMatches = namePattern.Matches(json);
                
                for (int i = 0; i < idMatches.Count; i++)
                {
                    string id = idMatches[i].Groups[1].Value;
                    string name = i < nameMatches.Count ? nameMatches[i].Groups[1].Value : id;
                    result.Add((id, name));
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Voices] TTSWebUIClient: Failed to parse voices JSON - {ex.Message}");
            }
            
            return result;
        }
    }
}
