using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Collections.Generic;
using Verse;
using Ustas.RimAI.Communication.Util;

namespace Ustas.RimAI.Communication.Voices.Service.FishAudioService;

/// <summary>
/// Client for Fish Audio TTS API using Python SDK via local HTTP server
/// Manages a persistent Python server process for handling concurrent requests
/// </summary>
public static class FishAudioTTSClient
{
    static string _pythonScriptPath;
    static bool _pythonScriptPathResolved;

    /// <summary>
    /// Resolved on first read, not at class load.
    ///
    /// This used to be a static readonly initialiser, which meant that merely
    /// naming the type ran a filesystem search, read LoadedModManager, and could
    /// call Log.Error. Application.quitting reached the type for the very first
    /// time through ShutdownServer, so on any session where TTS was never used,
    /// "shutting down" was actually the class initialising itself in the middle
    /// of Unity's teardown. A racing second read just recomputes the same path,
    /// which is cheaper than locking on a path that is read once in practice.
    /// </summary>
    internal static string PythonScriptPath
    {
        get
        {
            if (!_pythonScriptPathResolved)
            {
                _pythonScriptPathResolved = true;
                _pythonScriptPath = GetPythonScriptPath();
            }

            return _pythonScriptPath;
        }
    }
    internal static string _pythonExecutablePath;
    
    private static string GetPythonScriptPath()
    {
        return FishAudioTtsServerBootstrap.GetPythonScriptPath();
    }

    
    internal static Process _serverProcess;
    internal static HttpClient _httpClient;
    internal static readonly object _lock = new object();
    internal static bool _serverStarting = false;
    internal const int ServerPort = 5678;
    private static readonly string ServerUrl = $"http://127.0.0.1:{ServerPort}";

    /// <summary>
    /// Resolve Python executable path. Prefer bundled virtualenv under the mod, then env override, then system python.
    /// </summary>
    private static string ResolvePythonExecutablePath()
    {
        return FishAudioTtsServerBootstrap.ResolvePythonExecutablePath();
    }

    
    /// <summary>
    /// Check if required Python dependencies are installed
    /// </summary>
    private static async Task<bool> CheckPythonDependenciesAsync(string pythonExe)
    {
        return await FishAudioTtsServerBootstrap.CheckPythonDependenciesAsync(pythonExe);
    }

    
    /// <summary>
    /// Start the Python TTS server if not already running
    /// </summary>
    private static async Task<bool> EnsureServerRunningAsync()
    {
        return await FishAudioTtsServerBootstrap.EnsureServerRunningAsync();
    }

    
    /// <summary>
    /// Generate speech from text using Fish Audio TTS API via Python SDK
    /// Supports concurrent requests through HTTP server
    /// </summary>
    /// <param name="request">TTSRequest containing all parameters</param>
    /// <param name="cancellationToken">Cancellation token to cancel the request</param>
    public static async Task<byte[]> GenerateSpeechAsync(
        TTSRequest request,
        System.Threading.CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrEmpty(request.Input) || string.IsNullOrEmpty(request.ApiKey))
        {
            Log.Warning("FishAudio TTS: Text or API key is empty");
            return null;
        }
        try
        {
            // Ensure server is running, with retry on failure
            bool serverReady = await EnsureServerRunningAsync();
            if (!serverReady)
            {
                Log.Warning("FishAudio TTS: Server failed to start on first attempt, retrying once...");
                
                // Reset server state and try once more
                lock (_lock)
                {
                    if (_serverProcess != null && !_serverProcess.HasExited)
                    {
                        try
                        {
                            _serverProcess.Kill();
                        }
                        catch { }
                    }
                    _serverProcess = null;
                    _httpClient?.Dispose();
                    _httpClient = null;
                }
                
                // Wait a bit before retry
                await Task.Delay(1000);
                
                serverReady = await EnsureServerRunningAsync();
                if (!serverReady)
                {
                    Log.Error("FishAudio TTS: Server failed to start after retry");
                    return null;
                }
            }
            
            // Build request object mapping from TTSRequest
            var requestData = new PythonTTSRequest
            {
                api_key = request.ApiKey,
                text = request.Input,
                reference_id = request.Voice,
                model = request.Model,
                latency = "normal",
                speed = request.Speed,
                normalize = false,
                temperature = request.Temperature,
                top_p = request.TopP
            };
            
            string jsonContent = JsonUtil.SerializeToJson(requestData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            // Check cancellation before sending request
            cancellationToken.ThrowIfCancellationRequested();

            Logger.Debug($"FishAudio TTS: Sending request - {request.Input}");
            
            // Send HTTP request
            var response = await _httpClient.PostAsync(ServerUrl, content, cancellationToken);
            string responseText = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                // Parse error response to extract meaningful message
                string errorMessage = ExtractErrorMessage(responseText, response.StatusCode);
                Log.Error($"FishAudio TTS: {errorMessage}");
                return null;
            }
            
            // Parse response
            var result = JsonUtil.DeserializeFromJson<PythonTTSResponse>(responseText);
            
            if (result == null)
            {
                Log.Error($"FishAudio TTS: Failed to parse response: {responseText}");
                return null;
            }
            
            if (result.success && !string.IsNullOrEmpty(result.audio))
            {
                try
                {
                    byte[] audioData = Convert.FromBase64String(result.audio);
                    return audioData;
                }
                catch (FormatException ex)
                {
                    Log.Error($"FishAudio TTS: Invalid base64 audio data - {ex.Message}");
                    return null;
                }
            }
            else
            {
                string errorMsg = result.error ?? "Unknown error";
                if (!string.IsNullOrEmpty(result.traceback))
                {
                    Log.Error($"FishAudio TTS: Failed - {errorMsg}\nTraceback: {result.traceback}");
                }
                else
                {
                    Log.Error($"FishAudio TTS: Failed - {errorMsg}");
                }
                return null;
            }
        }
        catch (TaskCanceledException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Log.Warning("FishAudio TTS: Request timed out (30 seconds)");
            }
            return null;
        }
        catch (HttpRequestException ex)
        {
            Log.Error($"FishAudio TTS: HTTP request failed - {ex.Message}");
            
            // Server might have crashed, reset for next request
            lock (_lock)
            {
                if (_serverProcess != null && _serverProcess.HasExited)
                {
                    _serverProcess = null;
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            Log.Error($"FishAudio TTS: Unexpected error - {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Extract user-friendly error message from server response
    /// </summary>
    private static string ExtractErrorMessage(string responseText, System.Net.HttpStatusCode statusCode)
    {
        try
        {
            var errorResponse = JsonUtil.DeserializeFromJson<PythonTTSResponse>(responseText);
            
            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.error))
            {
                string error = errorResponse.error;
                
                // Return error message with first line of traceback if available
                string errorMessage = error;
                if (!string.IsNullOrEmpty(errorResponse.traceback))
                {
                    var tracebackLines = errorResponse.traceback.Split('\n');
                    if (tracebackLines.Length > 0)
                    {
                        errorMessage = $"{error}\n{tracebackLines[tracebackLines.Length - 1]}";
                    }
                }
                
                return errorMessage;
            }
            
            return $"Server returned {statusCode}: {responseText}";
        }
        catch
        {
            return $"Server returned {statusCode}: {responseText}";
        }
    }
    
    /// <summary>
    /// Shutdown the Python TTS server gracefully
    /// </summary>
    public static void ShutdownServer()
    {
        lock (_lock)
        {
            if (_serverProcess == null || _serverProcess.HasExited)
            {
                Log.Message("FishAudio TTS: Server already stopped");
                return;
            }
            
            try
            {
                Log.Message("FishAudio TTS: Sending shutdown command to server...");
                
                // Try to send shutdown command via HTTP
                var shutdownRequest = new Dictionary<string, string>
                {
                    { "command", "shutdown" }
                };
                
                string jsonContent = JsonUtil.SerializeToJson(shutdownRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // Use short timeout for shutdown command and disable cookies to avoid native cookie/container calls
                try
                {
                    var shortHandler = new HttpClientHandler { UseCookies = false };
                    using (var timeoutClient = new HttpClient(shortHandler) { Timeout = TimeSpan.FromSeconds(2) })
                    {
                        var task = timeoutClient.PostAsync(ServerUrl, content);
                        task.Wait(TimeSpan.FromSeconds(2));

                        if (task.IsCompleted && task.Result.IsSuccessStatusCode)
                        {
                            Log.Message("FishAudio TTS: Server shutdown command sent successfully");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"FishAudio TTS: Failed to send shutdown via cookie-less HttpClient - {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"FishAudio TTS: Failed to send shutdown command - {ex.Message}");
            }
            
            // Wait a bit for graceful shutdown
            try
            {
                if (!_serverProcess.WaitForExit(3000))
                {
                    Log.Warning("FishAudio TTS: Server did not exit gracefully, forcing termination");
                    _serverProcess.Kill();
                }
                else
                {
                    Log.Message("FishAudio TTS: Server exited gracefully");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"FishAudio TTS: Error during server shutdown - {ex.Message}");
            }
            finally
            {
                _serverProcess = null;
                _httpClient?.Dispose();
                _httpClient = null;
            }
        }
    }

    [DataContract]
    private class PythonTTSRequest
    {
        [DataMember(Name = "api_key")]
        public string api_key { get; set; }
        
        [DataMember(Name = "text")]
        public string text { get; set; }
        
        [DataMember(Name = "reference_id")]
        public string reference_id { get; set; }
        
        [DataMember(Name = "model")]
        public string model { get; set; }
        
        [DataMember(Name = "latency")]
        public string latency { get; set; }

        [DataMember(Name = "speed")]
        public float speed { get; set; }
        
        [DataMember(Name = "normalize")]
        public bool normalize { get; set; }
        
        [DataMember(Name = "temperature")]
        public float temperature { get; set; }
        
        [DataMember(Name = "top_p")]
        public float top_p { get; set; }
    }
    
    [DataContract]
    private class PythonTTSResponse
    {
        [DataMember(Name = "success")]
        public bool success { get; set; }
        
        [DataMember(Name = "audio")]
        public string audio { get; set; }
        
        [DataMember(Name = "size")]
        public int size { get; set; }
        
        [DataMember(Name = "error")]
        public string error { get; set; }
        
        [DataMember(Name = "traceback")]
        public string traceback { get; set; }
    }
}
