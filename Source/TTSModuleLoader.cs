using HarmonyLib;
using System;
using System.Reflection;
using Ustas.RimAI.Communication.Voices.Integration;
using Verse;

namespace Ustas.RimAI.Communication.Voices
{
    /// <summary>
    /// Entry point for TTS module - applies Harmony patches to hook into main RimTalk
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TTSModuleLoader
    {
        static TTSModuleLoader()
        {
            try
            {
                Log.Message("[RimAI.Voices] Initializing TTS Module...");
                
                var harmony = new Harmony("ustas.rimai.communication.voices");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                TalkLifecycleBridge.Register();
                
                TTSModule.Instance.Initialize();
                
                // Register application quit handler for proper cleanup
                UnityEngine.Application.quitting += OnApplicationQuitting;
                
                Log.Message("[RimAI.Voices] TTS Module initialized successfully");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Voices] Failed to initialize: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void OnApplicationQuitting()
        {
            try
            {
                Log.Message("[RimAI.Voices] Application quitting, performing cleanup...");
                TTSModule.Instance.OnGameExit();
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Voices] Error during application quit: {ex.Message}");
            }
        }
    }
}