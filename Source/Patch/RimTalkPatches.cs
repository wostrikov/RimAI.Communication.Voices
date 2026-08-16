using HarmonyLib;
using System;
using RimWorld;
using UnityEngine;
using System.Collections.Generic;
using Verse;
using Ustas.RimAI.Communication.Voices.Service;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Voices.Data;

namespace Ustas.RimAI.Communication.Voices.Patch
{
    /// <summary>
    /// RimWorld/base-game Harmony patches and TTS dialogue-block helpers.
    /// Communication talk hooks live in TalkLifecycleBridge, not sibling Harmony.
    /// </summary>
    public static class RimTalkPatches
    {
        private static bool _pendingToggle = false;
        private static bool _pendingToggleValue = false;
        private static readonly object _pendingToggleLock = new object();
        private static string _pendingToggleMessage = "";

        public static bool IsTalkIgnored(Guid dialogueId)
        {
            try
            {
                return TalkHistory.IsTalkIgnored(dialogueId);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Voices] IsTalkIgnored exception: {ex}");
            }
            return false;
        }

        public static readonly HashSet<Guid> blockedDialogues = new HashSet<Guid>();
        private static readonly object _blockLock = new object();

        [HarmonyPatch(typeof(Pawn), "Discard")]
        public static class PawnDiscard_Patch
        {
            static void Prefix(Pawn __instance, bool silentlyRemoveReferences)
            {
                try
                {
                    if (__instance != null)
                        Data.PawnVoiceManager.RemovePawn(__instance);
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimAI.Voices] PawnDiscard_Patch exception: {ex}");
                }
            }
        }

        public static void RequestBlock(Guid dialogueId)
        {
            lock (_blockLock)
            {
                blockedDialogues.Add(dialogueId);
            }
        }

        public static void ReleaseBlock(Guid dialogueId)
        {
            lock (_blockLock)
            {
                blockedDialogues.Remove(dialogueId);
            }
        }

        public static bool IsBlocked(Guid dialogueId)
        {
            lock (_blockLock)
            {
                return blockedDialogues.Contains(dialogueId);
            }
        }

        [StaticConstructorOnStartup]
        [HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
        public static class TogglePatch
        {
            private static readonly Texture2D RimTalkToggleIcon = ContentFinder<Texture2D>.Get("UI/ToggleButton");

            public static void Postfix(WidgetRow row, bool worldView)
            {
                if (!TTSConfig.IsEnabled)
                    return;
                if (worldView || row is null)
                    return;

                var settings = TTSConfig.Settings;
                if (settings.ButtonDisplay != true)
                    return;

                bool onOff = settings.isOnButton;
                row.ToggleableIcon(ref onOff, RimTalkToggleIcon, "",
                    SoundDefOf.Mouseover_ButtonToggle);

                if (onOff != settings.isOnButton)
                {
                    settings.isOnButton = onOff;
                    lock (_pendingToggleLock)
                    {
                        _pendingToggle = true;
                        _pendingToggleValue = onOff;
                        _pendingToggleMessage = "Ustas.RimAI.Communication.Voices.OnOffUpdated".Translate(onOff ? "Ustas.RimAI.Communication.Voices.On".Translate() : "Ustas.RimAI.Communication.Voices.Off".Translate());
                    }
                }
            }
        }

        [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
        public static class Update_PendingToggleExecutor
        {
            static void Postfix()
            {
                if (!TTSConfig.IsEnabled)
                    return;
                if (!_pendingToggle) return;
                bool onOff;
                string msg;
                lock (_pendingToggleLock)
                {
                    onOff = _pendingToggleValue;
                    msg = _pendingToggleMessage;
                    _pendingToggle = false;
                    _pendingToggleMessage = "";
                }

                try
                {
                    Messages.Message(msg, MessageTypeDefOf.TaskCompletion, false);
                    if (!onOff)
                        TTSService.StopAll(false);
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimAI.Voices] PendingToggleExecutor exception: {ex}");
                }
            }
        }

        public static void AddPawnDialogueList(Pawn pawn)
        {
        }

        public static bool TTSModuleIsActive()
        {
            return TTSConfig.IsEnabled
                && TTSConfig.Settings.isOnButton;
        }

        public static void UpdatePlayerPawnVoice()
        {
            var pawn = global::Ustas.RimAI.Communication.Data.Cache.GetPlayer();
            var settings = TTSConfig.Settings;
            Data.PawnVoiceManager.SetVoiceModel(pawn, settings.PlayerReferenceVoiceModelId);
        }
    }
}
