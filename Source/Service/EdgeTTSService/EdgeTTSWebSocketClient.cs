using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace Ustas.RimAI.Communication.Voices.Service.EdgeTTSService
{
    public class EdgeTTSWebSocketClient : IDisposable
    {
        private const string BASE_URL = "speech.platform.bing.com/consumer/speech/synthesize/readaloud";
        
        private const string TRUSTED_CLIENT_TOKEN = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
        
        // WebSocket URL
        private static readonly string WSS_URL = $"wss://{BASE_URL}/edge/v1?TrustedClientToken={TRUSTED_CLIENT_TOKEN}";
        
        private const long WIN_EPOCH = 11644473600;
        
        private const string CHROMIUM_FULL_VERSION = "143.0.3650.75";
        private static readonly string CHROMIUM_MAJOR_VERSION = CHROMIUM_FULL_VERSION.Split('.')[0];
        private static readonly string SEC_MS_GEC_VERSION = $"1-{CHROMIUM_FULL_VERSION}";
        
        private static double clockSkewSeconds = 0.0;
        
        private ClientWebSocket webSocket;
        private CancellationTokenSource cts;
        private bool isDisposed = false;
        
        private const string OUTPUT_FORMAT = "audio-24khz-48kbitrate-mono-mp3";
        
        private const int MAX_RETRIES = 3;
        
        private static string GenerateSecMsGec()
        {
            double ticks = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + clockSkewSeconds;
            
            ticks += WIN_EPOCH;
            
            ticks -= ticks % 300;
            
            ticks *= 1e9 / 100;
            
            string strToHash = $"{ticks:F0}{TRUSTED_CLIENT_TOKEN}";
            
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(strToHash));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToUpper();
            }
        }
        
        private static string GenerateMuid()
        {
            byte[] bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return BitConverter.ToString(bytes).Replace("-", "").ToUpper();
        }
        
        private static string GenerateConnectId()
        {
            return Guid.NewGuid().ToString("N");
        }
        
        private static string BuildWebSocketUrl()
        {
            string connectId = GenerateConnectId();
            string secMsGec = GenerateSecMsGec();
            return $"{WSS_URL}&ConnectionId={connectId}&Sec-MS-GEC={secMsGec}&Sec-MS-GEC-Version={SEC_MS_GEC_VERSION}";
        }
        
        public async Task<byte[]> SynthesizeAsync(string text, string voice = "zh-CN-XiaoxiaoNeural", 
            string rate = "+0%", string volume = "+0%", string pitch = null, string locale = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            
            for (int retry = 0; retry < MAX_RETRIES; retry++)
            {
                try
                {
                    string wssUrl = BuildWebSocketUrl();
                    byte[] result = await TrySynthesizeAsync(wssUrl, text, voice, rate, volume, pitch, locale);
                    if (result != null && result.Length > 0)
                    {
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[EdgeTTS] Attempt {retry + 1}/{MAX_RETRIES} failed: {ex.Message}");
                    }
                    
                    if (ex.Message.Contains("403"))
                    {
                        clockSkewSeconds += 300;
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[EdgeTTS] Adjusting clock skew to {clockSkewSeconds} seconds");
                        }
                    }
                    
                    if (ex.Message.Contains("ResourceExhausted") || ex.Message.Contains("1013"))
                    {
                        await Task.Delay(1000 * (retry + 1));
                    }
                }
            }
            
            Log.Error("[EdgeTTS] All attempts failed");
            return null;
        }
        
        private async Task<byte[]> TrySynthesizeAsync(string wssUrl, string text, string voice, string rate, string volume, string pitch, string locale)
        {
            try
            {
                webSocket = new ClientWebSocket();
                cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                
                webSocket.Options.SetRequestHeader("User-Agent", 
                    $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{CHROMIUM_MAJOR_VERSION}.0.0.0 Safari/537.36 Edg/{CHROMIUM_MAJOR_VERSION}.0.0.0");
                webSocket.Options.SetRequestHeader("Accept-Encoding", "gzip, deflate, br, zstd");
                webSocket.Options.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
                webSocket.Options.SetRequestHeader("Pragma", "no-cache");
                webSocket.Options.SetRequestHeader("Cache-Control", "no-cache");
                webSocket.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
                
                webSocket.Options.SetRequestHeader("Cookie", $"muid={GenerateMuid()};");
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[EdgeTTS] Connecting to: {wssUrl.Substring(0, Math.Min(100, wssUrl.Length))}...");
                }
                
                await webSocket.ConnectAsync(new Uri(wssUrl), cts.Token);
                
                if (webSocket.State != WebSocketState.Open)
                {
                    throw new Exception($"WebSocket connection failed, state: {webSocket.State}");
                }
                
                if (Prefs.DevMode)
                {
                    Log.Message("[EdgeTTS] WebSocket connected successfully");
                }
                
                await SendConfigMessageAsync();
                
                string ssml = BuildSSML(text, voice, rate, volume, pitch, locale);
                await SendSSMLMessageAsync(ssml);
                
                byte[] audioData = await ReceiveAudioAsync();
                
                return audioData;
            }
            finally
            {
                CloseWebSocket();
            }
        }
        
        private static string DateToString()
        {
            return DateTime.UtcNow.ToString("ddd MMM dd yyyy HH:mm:ss") + " GMT+0000 (Coordinated Universal Time)";
        }
        
        private async Task SendConfigMessageAsync()
        {
            string timestamp = DateToString();
            
            string configMessage = 
                $"X-Timestamp:{timestamp}\r\n" +
                "Content-Type:application/json; charset=utf-8\r\n" +
                "Path:speech.config\r\n\r\n" +
                "{\"context\":{\"synthesis\":{\"audio\":{" +
                "\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"true\",\"wordBoundaryEnabled\":\"false\"}," +
                $"\"outputFormat\":\"{OUTPUT_FORMAT}\"" +
                "}}}}\r\n";
            
            byte[] buffer = Encoding.UTF8.GetBytes(configMessage);
            await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cts.Token);
            
            if (Prefs.DevMode)
            {
                Log.Message("[EdgeTTS] Config message sent");
            }
        }
        
        private async Task SendSSMLMessageAsync(string ssml)
        {
            string timestamp = DateToString();
            string requestId = GenerateConnectId();
            
            // Non-obvious edge case — read carefully before changing. (X-Timestamp Microsoft Edge bug edge-tts)
            string ssmlMessage = 
                $"X-RequestId:{requestId}\r\n" +
                "Content-Type:application/ssml+xml\r\n" +
                $"X-Timestamp:{timestamp}Z\r\n" +
                "Path:ssml\r\n\r\n" +
                ssml;
            
            byte[] buffer = Encoding.UTF8.GetBytes(ssmlMessage);
            await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cts.Token);
            
            if (Prefs.DevMode)
            {
                Log.Message($"[EdgeTTS] SSML message sent, RequestId: {requestId}");
            }
        }
        
        private async Task<byte[]> ReceiveAudioAsync()
        {
            var audioChunks = new List<byte[]>();
            byte[] buffer = new byte[16384];
            bool audioReceived = false;
            
            while (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[EdgeTTS] WebSocket closed: {result.CloseStatus} - {result.CloseStatusDescription}");
                        }
                        break;
                    }
                    
                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        if (result.Count > 2)
                        {
                            int headerLength = (buffer[0] << 8) | buffer[1];
                            if (result.Count > headerLength + 2)
                            {
                                string header = Encoding.UTF8.GetString(buffer, 2, headerLength);
                                if (header.Contains("Path:audio"))
                                {
                                    if (header.Contains("Content-Type:audio/mpeg"))
                                    {
                                        int audioStart = headerLength + 2;
                                        int audioLength = result.Count - audioStart;
                                        if (audioLength > 0)
                                        {
                                            byte[] audioChunk = new byte[audioLength];
                                            Array.Copy(buffer, audioStart, audioChunk, 0, audioLength);
                                            audioChunks.Add(audioChunk);
                                            audioReceived = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        
                        if (message.Contains("Path:turn.end"))
                        {
                            if (Prefs.DevMode)
                            {
                                Log.Message("[EdgeTTS] Received turn.end");
                            }
                            break;
                        }
                        
                        if (message.Contains("Path:response") && message.Contains("error"))
                        {
                            Log.Error($"[EdgeTTS] Server error: {message}");
                            throw new Exception($"Server error: {message}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Log.Warning("[EdgeTTS] Operation cancelled (timeout)");
                    break;
                }
            }
            
            if (!audioReceived)
            {
                Log.Warning("[EdgeTTS] No audio data received");
                return null;
            }
            
            if (audioChunks.Count == 0)
            {
                return null;
            }
            
            int totalLength = 0;
            foreach (var chunk in audioChunks)
            {
                totalLength += chunk.Length;
            }
            
            byte[] audioData = new byte[totalLength];
            int offset = 0;
            foreach (var chunk in audioChunks)
            {
                Array.Copy(chunk, 0, audioData, offset, chunk.Length);
                offset += chunk.Length;
            }
            
            if (Prefs.DevMode)
            {
                Log.Message($"[EdgeTTS] Received {audioChunks.Count} audio chunks, total {totalLength} bytes");
            }
            
            return audioData;
        }
        
        private string BuildSSML(string text, string voice, string rate, string volume, string pitch, string locale)
        {
            text = System.Security.SecurityElement.Escape(text);

            // The document language must follow the selected voice, otherwise a
            // Ukrainian voice is asked to read Ukrainian text as en-US.
            string language = string.IsNullOrWhiteSpace(locale) ? LocaleFromVoice(voice) : locale.Trim();
            string prosodyPitch = string.IsNullOrWhiteSpace(pitch) ? "+0Hz" : pitch.Trim();

            return $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{language}'>" +
                   $"<voice name='{voice}'>" +
                   $"<prosody pitch='{prosodyPitch}' rate='{rate}' volume='{volume}'>" +
                   text +
                   "</prosody>" +
                   "</voice>" +
                   "</speak>";
        }

        /// <summary>Neural voice names carry their locale as a prefix, e.g. uk-UA-OstapNeural.</summary>
        private static string LocaleFromVoice(string voice)
        {
            if (!string.IsNullOrWhiteSpace(voice))
            {
                string[] parts = voice.Split('-');
                if (parts.Length >= 3 && parts[0].Length == 2 && parts[1].Length == 2)
                    return parts[0].ToLowerInvariant() + "-" + parts[1].ToUpperInvariant();
            }

            return "en-US";
        }
        
        private void CloseWebSocket()
        {
            try
            {
                if (webSocket != null && webSocket.State == WebSocketState.Open)
                {
                    webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None).Wait(1000);
                }
            }
            catch { }
            finally
            {
                webSocket?.Dispose();
                webSocket = null;
                cts?.Dispose();
                cts = null;
            }
        }
        
        public void Dispose()
        {
            if (!isDisposed)
            {
                CloseWebSocket();
                isDisposed = true;
            }
        }
    }
}
