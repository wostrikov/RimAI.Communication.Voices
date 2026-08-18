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
using Ustas.RimAI.Core.Storage;

namespace Ustas.RimAI.Communication.Voices.Service.FishAudioService;

internal static class FishAudioTtsServerBootstrap
{
    
    internal static string GetPythonScriptPath()
    {
        try
        {
            // Method 1: Try Assembly.Location
            string assemblyLocation = typeof(FishAudioTTSClient).Assembly.Location;
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                string assemblyDir = Path.GetDirectoryName(assemblyLocation);
                if (!string.IsNullOrEmpty(assemblyDir))
                {
                    string scriptPath = Path.Combine(assemblyDir, "..", "..", "Source", "Service", "FishAudioService", "fish_audio_tts.py");
                    if (LocalStorage.Current.FileExists(scriptPath))
                    {
                        return scriptPath;
                    }
                }
            }
            
            // Method 2: Try from RimWorld Mods directory structure
            // Assembly is in: Mods/rimtalk/1.6/Assemblies/Ustas.RimAI.Communication.dll
            // Script is in:   Mods/rimtalk/Source/Service/fish_audio_tts.py
            var loadedMods = Verse.LoadedModManager.RunningMods;
            foreach (var mod in loadedMods)
            {
                if (mod.Name.Contains("Ustas.RimAI.Communication") || mod.PackageId.Contains("rimtalk"))
                {
                    string scriptPath = Path.Combine(mod.RootDir.ToString(), "Source", "Service", "FishAudioService", "fish_audio_tts.py");
                    if (LocalStorage.Current.FileExists(scriptPath))
                    {
                        return scriptPath;
                    }
                }
            }
            
            Log.Error("FishAudio TTS: Could not locate fish_audio_tts.py");
            return "";
        }
        catch (Exception ex)
        {
            Log.Error($"FishAudio TTS: Failed to get Python script path - {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// Resolve Python executable path. Prefer bundled virtualenv under the mod, then env override, then system python.
    /// </summary>
    internal static string ResolvePythonExecutablePath()
    {
        lock (FishAudioTTSClient._lock)
        {
            if (!string.IsNullOrEmpty(FishAudioTTSClient._pythonExecutablePath))
            {
                return FishAudioTTSClient._pythonExecutablePath;
            }
        }

        var candidates = new List<string>();

        // Environment override
        var envPython = Environment.GetEnvironmentVariable("RIMTALK_TTS_PYTHON");
        if (!string.IsNullOrWhiteSpace(envPython))
        {
            candidates.Add(envPython.Trim());
        }

        // Bundled python environment alongside the mod (e.g., Mods/RimTalkTTS/python_env/python.exe)
        try
        {
            if (!string.IsNullOrEmpty(FishAudioTTSClient.PythonScriptPath))
            {
                string scriptDir = Path.GetDirectoryName(FishAudioTTSClient.PythonScriptPath);
                string modRoot = Directory.GetParent(scriptDir)?.Parent?.FullName; // Service -> Source -> ModRoot

                if (!string.IsNullOrEmpty(modRoot))
                {
                    candidates.Add(Path.Combine(modRoot, "python_env", "python.exe"));
                    candidates.Add(Path.Combine(modRoot, "python_env", "Scripts", "python.exe"));
                    candidates.Add(Path.Combine(modRoot, "python", "python.exe"));
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"FishAudio TTS: Failed to probe bundled python path - {ex.Message}");
        }

        // Fallback to system python on PATH
        candidates.Add("python");
        candidates.Add("python.exe");

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            // If candidate is a full path, ensure it exists
            if (candidate.Contains(Path.DirectorySeparatorChar) || candidate.Contains("/"))
            {
                if (LocalStorage.Current.FileExists(candidate))
                {
                    lock (FishAudioTTSClient._lock)
                    {
                        FishAudioTTSClient._pythonExecutablePath = candidate;
                    }
                    Log.Message($"FishAudio TTS: Using bundled Python at '{candidate}'");
                    return candidate;
                }
            }
            else
            {
                // Assume it's available via PATH
                lock (FishAudioTTSClient._lock)
                {
                    FishAudioTTSClient._pythonExecutablePath = candidate;
                }
                Log.Message($"FishAudio TTS: Using system Python executable '{candidate}'");
                return candidate;
            }
        }

        Log.Error("FishAudio TTS: No valid Python executable found. Set RIMTALK_TTS_PYTHON to a valid path or place a python_env next to the mod.");
        return "";
    }
    
    /// <summary>
    /// Check if required Python dependencies are installed
    /// </summary>
    internal static async Task<bool> CheckPythonDependenciesAsync(string pythonExe)
    {
        try
        {
            string checkScript = Path.Combine(Path.GetDirectoryName(FishAudioTTSClient.PythonScriptPath), "check_dependencies.py");
            
            // If check script doesn't exist, skip the check (backward compatibility)
            if (!LocalStorage.Current.FileExists(checkScript))
            {
                Log.Warning("FishAudio TTS: Dependency check script not found, skipping validation");
                return true;
            }
            
            var processInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{checkScript}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            
            using (var process = new Process { StartInfo = processInfo })
            {
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                
                bool exited = process.WaitForExit(5000); // 5 second timeout
                
                if (!exited)
                {
                    Log.Warning("FishAudio TTS: Dependency check timed out");
                    process.Kill();
                    return true; // Don't block if check fails
                }
                
                if (process.ExitCode != 0)
                {
                    Log.Error($"FishAudio TTS: Dependency check failed:\n{output}\n{error}");
                    return false;
                }
                
                Log.Message($"FishAudio TTS: Dependencies verified:\n{output}");
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"FishAudio TTS: Failed to check dependencies - {ex.Message}");
            return true; // Don't block on check failure
        }
    }
    
    /// <summary>
    /// Start the Python TTS server if not already running
    /// </summary>
    internal static async Task<bool> EnsureServerRunningAsync()
    {
        // Check if server is already running
        lock (FishAudioTTSClient._lock)
        {
            if (FishAudioTTSClient._serverProcess != null && !FishAudioTTSClient._serverProcess.HasExited)
            {
                return true;
            }
        }
        
        // Check if another thread is starting the server
        bool shouldWait = false;
        lock (FishAudioTTSClient._lock)
        {
            if (FishAudioTTSClient._serverStarting)
            {
                shouldWait = true;
            }
            else
            {
                FishAudioTTSClient._serverStarting = true;
            }
        }
        
        // If another thread is starting, wait for it to complete
        if (shouldWait)
        {
            int waitCount = 0;
            while (waitCount < 100) // Wait up to 10 seconds
            {
                await Task.Delay(100);
                waitCount++;
                
                lock (FishAudioTTSClient._lock)
                {
                    // Check if server is now running
                    if (FishAudioTTSClient._serverProcess != null && !FishAudioTTSClient._serverProcess.HasExited)
                    {
                        return true;
                    }
                    
                    // Check if startup failed (flag cleared but no process)
                    if (!FishAudioTTSClient._serverStarting)
                    {
                        Log.Warning("FishAudio TTS: Server startup failed while waiting");
                        return false;
                    }
                }
            }
            
            Log.Warning("FishAudio TTS: Server startup timeout while waiting");
            return false;
        }
        
        try
        {
            // Validate Python script path
            if (string.IsNullOrEmpty(FishAudioTTSClient.PythonScriptPath))
            {
                Log.Error("FishAudio TTS: Python script path is not initialized");
                return false;
            }
            
            if (!LocalStorage.Current.FileExists(FishAudioTTSClient.PythonScriptPath))
            {
                Log.Error($"FishAudio TTS: Python script not found at: {FishAudioTTSClient.PythonScriptPath}");
                return false;
            }

            // Resolve Python executable (bundled env or system python)
            string pythonExe = ResolvePythonExecutablePath();
            if (string.IsNullOrEmpty(pythonExe))
            {
                return false;
            }
            
            // Check Python dependencies before starting server
            if (!await CheckPythonDependenciesAsync(pythonExe))
            {
                Log.Error("FishAudio TTS: Python dependencies check failed. Please install: pip install fish-audio-sdk");
                return false;
            }
            
            Log.Message("FishAudio TTS: Starting Python server...");
            
            // Get current process ID to pass to Python server
            int currentProcessId = Process.GetCurrentProcess().Id;
            
            var processInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{FishAudioTTSClient.PythonScriptPath}\" {FishAudioTTSClient.ServerPort} {currentProcessId}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var process = new Process { StartInfo = processInfo };
            
            bool started = false;
            bool hasFatalError = false;
            StringBuilder errorOutput = new StringBuilder();
            
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Log.Message($"FishAudio TTS Server: {e.Data}");
                    if (e.Data.Contains("\"status\": \"ready\""))
                    {
                        started = true;
                    }
                }
            };
            
            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    // Collect error output for diagnosis
                    errorOutput.AppendLine(e.Data);
                    
                    // Check for fatal errors that indicate missing dependencies
                    if (e.Data.Contains("ModuleNotFoundError") || e.Data.Contains("No module named"))
                    {
                        hasFatalError = true;
                        Log.Error($"FishAudio TTS: Python dependency missing - {e.Data}");
                        Log.Error("FishAudio TTS: Please install fishaudio package: pip install fish-audio-sdk");
                    }
                    else if (e.Data.Contains("ImportError"))
                    {
                        hasFatalError = true;
                        Log.Error($"FishAudio TTS: Python import error - {e.Data}");
                    }
                    // Python server logs HTTP requests to stderr - treat as debug, not error
                    else if (e.Data.Contains("[TTS Server]") || e.Data.Contains("POST /") || e.Data.Contains("GET /"))
                    {
                        Log.Message($"FishAudio TTS Server: {e.Data}");
                    }
                    else
                    {
                        Log.Warning($"FishAudio TTS Server stderr: {e.Data}");
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            // Wait for server to be ready (max 15 seconds - increased for slow systems)
            int waitCount = 0;
            while (!started && waitCount < 150)
            {
                await Task.Delay(100);
                waitCount++;
                
                // Check if process crashed during startup
                if (process.HasExited)
                {
                    Log.Error($"FishAudio TTS: Python process exited during startup with code {process.ExitCode}");
                    return false;
                }
            }
            
            if (!started || hasFatalError)
            {
                if (hasFatalError)
                {
                    Log.Error("FishAudio TTS: Server startup failed due to fatal error (see above)");
                    Log.Error("FishAudio TTS: Complete error output:");
                    Log.Error(errorOutput.ToString());
                }
                else
                {
                    Log.Error("FishAudio TTS: Server failed to start within 15 seconds timeout");
                    if (errorOutput.Length > 0)
                    {
                        Log.Error("FishAudio TTS: Error output:");
                        Log.Error(errorOutput.ToString());
                    }
                }
                
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch (Exception killEx)
                {
                    Log.Warning($"FishAudio TTS: Failed to kill non-responsive process - {killEx.Message}");
                }
                return false;
            }
            
            lock (FishAudioTTSClient._lock)
            {
                FishAudioTTSClient._serverProcess = process;
                // Use InfiniteTimeSpan - timeout is controlled per-request via CancellationToken
                // Create HttpClient with cookies disabled to avoid Mono/Win32 cookie/container codepaths
                try
                {
                    var handler = new HttpClientHandler { UseCookies = false };
                    FishAudioTTSClient._httpClient = new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
                }
                catch (Exception ex)
                {
                    // Fallback to default HttpClient if handler creation fails for any reason
                    Log.Warning($"FishAudio TTS: Failed to create cookie-less HttpClient handler - {ex.Message}. Falling back to default HttpClient.");
                    FishAudioTTSClient._httpClient = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"FishAudio TTS: Failed to start server - {ex.Message}");
            return false;
        }
        finally
        {
            lock (FishAudioTTSClient._lock)
            {
                FishAudioTTSClient._serverStarting = false;
            }
        }
    }
}
