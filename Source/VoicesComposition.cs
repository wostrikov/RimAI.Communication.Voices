using System;
using System.Reflection;
using HarmonyLib;
using Ustas.RimAI.Communication.Voices.Integration;
using Ustas.RimAI.Core.Composition;
using Ustas.RimAI.Core.Diagnostics;
using Ustas.RimAI.Core.Handshake;
using Ustas.RimAI.Core.Modules;
using UnityEngine;

namespace Ustas.RimAI.Communication.Voices;

/// <summary>
/// Module composition root for RimAI.Communication.Voices. Owns Harmony, talk
/// lifecycle bridge, and TTS module initialization.
/// </summary>
public sealed class VoicesComposition : IRimAiModuleComposition
{
    public static VoicesComposition Current { get; } = new();

    bool _quitHooked;

    public string ModuleId => RimAiModuleIds.Voices;

    public bool IsStarted { get; private set; }

    public void Start()
    {
        if (IsStarted)
            return;

        try
        {
            RimAIModuleRegistry.Current.Register(
                new RimAIModuleDescriptor(
                    "voices",
                    "RimAI.Communication.Voices",
                    "RimAI.Communication.Voices",
                    "Communication",
                    "RimAI.Communication"));

            RimAiLog.Info(RimAiLogCategory.Voices, "[RimAI.Voices] Initializing TTS Module...");
            var harmony = new Harmony("ustas.rimai.communication.voices");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            TalkLifecycleBridge.Register();
            VoicesPipelineProbe.Register();
            TTSModule.Instance.Initialize();

            if (!_quitHooked)
            {
                Application.quitting += OnApplicationQuitting;
                _quitHooked = true;
            }

            RimAiLog.Info(RimAiLogCategory.Voices, "[RimAI.Voices] TTS Module initialized successfully");
            IsStarted = true;
        }
        // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — Voices module start must not crash RimWorld boot
        catch (Exception ex)
        {
            RimAiLog.Error(RimAiLogCategory.Voices, "[RimAI.Voices] Failed to initialize", ex);
        }
    }

    public void Stop()
    {
        IsStarted = false;
    }

    static void OnApplicationQuitting()
    {
        try
        {
            RimAiLog.Info(RimAiLogCategory.Voices, "[RimAI.Voices] Application quitting, performing cleanup...");
            TTSModule.Instance.OnGameExit();
        }
        // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — quit cleanup must not throw during process exit
        catch (Exception ex)
        {
            RimAiLog.Error(RimAiLogCategory.Voices, "[RimAI.Voices] Error during application quit", ex);
        }
    }
}
