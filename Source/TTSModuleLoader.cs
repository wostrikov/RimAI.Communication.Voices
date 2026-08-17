using HarmonyLib;
using System;
using System.Reflection;
using Ustas.RimAI.Communication.Voices.Integration;
using Ustas.RimAI.Core.Handshake;
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
                if (!RimAiHandshake.IsApproved(RimAiModuleIds.Voices))
                {
                    return;
                }

                Log.Message("[RimAI.Voices] Initializing TTS Module...");

                var harmony = new Harmony("ustas.rimai.communication.voices");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                TalkLifecycleBridge.Register();

                TTSModule.Instance.Initialize();

                UnityEngine.Application.quitting += OnApplicationQuitting;
                RimAiHandshakeRegistry.Current.MarkActivated(RimAiModuleIds.Voices);

                Log.Message("[RimAI.Voices] TTS Module initialized successfully");
            }
            catch (Exception ex)
            {
                RimAiHandshakeRegistry.Current.MarkFailed(RimAiModuleIds.Voices);
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