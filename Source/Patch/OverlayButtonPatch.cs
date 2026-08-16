using UnityEngine;
using HarmonyLib;
using System.Reflection;
using Verse;
using System;
using RimWorld;
using Ustas.RimAI.Communication.Voices.Service;
using Ustas.RimAI.Communication.Service;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Util;
using Ustas.RimAI.Communication.Voices.Data;

namespace Ustas.RimAI.Communication.Voices.Patch
{
    public static class OverlayButtonPatch
    {
        private static Rect resetButtonScreenRect = default;
        private static Rect generateButtonScreenRect = default;
        private static Rect ignoreButtonScreenRect = default;
        private static Rect displayButtonScreenRect = default;
        private static bool displayPassport = false;

        [HarmonyPatch]
        public static class Overlay_MapComponentOnGUI_Postfix
        {
            // Target the non-public instance method DrawSettingsDropdown on Ustas.RimAI.Communication.UI.Overlay
            static MethodBase TargetMethod()
            {
                return typeof(global::Ustas.RimAI.Communication.UI.Overlay).GetMethod("MapComponentOnGUI", BindingFlags.Public | BindingFlags.Instance);
            }

            static void Postfix(object __instance)
            {
                try
                {
                    if (__instance == null) return;
                    if (!TTSConfig.IsEnabled) return;

                    var overlayType = __instance.GetType();

                    // Prefer placing the Reset button to the left of the gear icon if available
                    var gearField = overlayType.GetField("_gearIconScreenRect", BindingFlags.NonPublic | BindingFlags.Instance);

                    var gearRect = (Rect)gearField.GetValue(__instance);
                    // Button sizing and positioning: place to the left of the gear icon with a small padding
                    float resetBtnWidth = 120f;
                    float generateBtnWidth = 150f;
                    float ignoreBtnWidth = 150f;
                    float displayBtnWidth = 150f;
                    float btnHeight = Mathf.Max(gearRect.height, 28f);
                    float padding = 6f;
                    resetButtonScreenRect.Set(gearRect.x - resetBtnWidth - padding, gearRect.y, resetBtnWidth, btnHeight);
                    generateButtonScreenRect.Set(gearRect.x - resetBtnWidth - generateBtnWidth - 2*padding, gearRect.y, generateBtnWidth, btnHeight);
                    ignoreButtonScreenRect.Set(gearRect.x - resetBtnWidth - generateBtnWidth - ignoreBtnWidth - 2*padding, gearRect.y, ignoreBtnWidth, btnHeight);
                    displayButtonScreenRect.Set(gearRect.x - resetBtnWidth - generateBtnWidth - ignoreBtnWidth - displayBtnWidth - 2*padding, gearRect.y, ignoreBtnWidth, btnHeight);

                    if (Widgets.ButtonText(resetButtonScreenRect, "Ustas.RimAI.Communication.Voices.Reset".Translate()))
                    {
                        ResetButtonFunc();
                    }
                    if (Widgets.ButtonText(generateButtonScreenRect, "Ustas.RimAI.Communication.Voices.Generate".Translate()))
                    {
                        generateButtonFunc();
                    }
                    if (Widgets.ButtonText(ignoreButtonScreenRect, "Ustas.RimAI.Communication.Voices.Ignore".Translate()))
                    {
                        ignoreButtonFunc();
                    }
                    if (Widgets.ButtonText(displayButtonScreenRect, "Ustas.RimAI.Communication.Voices.Display".Translate()))
                    {
                        displayButtonFunc();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimAI.Voices] Overlay_DrawSettingsDropdown_Postfix exception: {ex}");
                }
            }
        }

        [HarmonyPatch]
        public static class Overlay_HandleInput_Prefix
        {
            // Target the non-public instance method HandleInput on Ustas.RimAI.Communication.UI.Overlay
            static MethodBase TargetMethod()
            {
                return typeof(global::Ustas.RimAI.Communication.UI.Overlay).GetMethod("HandleInput", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            static bool Prefix(object __instance)
            {
                if (!TTSConfig.IsEnabled) return true;
                if (__instance == null) return true;
                
                Event currentEvent = Event.current;

                if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
                {
                    if (resetButtonScreenRect.Contains(currentEvent.mousePosition))
                    {
                        currentEvent.Use();
                        ResetButtonFunc();
                        return false; // Consume event
                    }
                    if (generateButtonScreenRect.Contains(currentEvent.mousePosition))
                    {
                        currentEvent.Use();
                        generateButtonFunc();
                        return false; // Consume event
                    }
                    if (ignoreButtonScreenRect.Contains(currentEvent.mousePosition))
                    {
                        currentEvent.Use();
                        ignoreButtonFunc();
                        return false; // Consume event
                    }
                    if (displayButtonScreenRect.Contains(currentEvent.mousePosition))
                    {
                        currentEvent.Use();
                        displayButtonFunc();
                        return false; // Consume event
                    }
                }

                return true;
            }
        }

        private static void ResetButtonFunc()
        {
            Messages.Message("Ustas.RimAI.Communication.Voices.ResetComplete".Translate(), MessageTypeDefOf.TaskCompletion, false);
            TTSService.StopAll(false);
        }

        private static void generateButtonFunc()
        {
            Messages.Message("Ustas.RimAI.Communication.Voices.GenerateComplete".Translate(), MessageTypeDefOf.TaskCompletion, false);
            // Select a pawn based on the current iteration strategy
            Pawn selectedPawn = PawnSelector.SelectNextAvailablePawn();

            if (selectedPawn != null)
            {
                // 1. ALWAYS try to get from the general pool first.
                bool talkGenerated;
                // If the pawn is a free colonist not in danger and the pool has requests
                if (!selectedPawn.IsFreeNonSlaveColonist || selectedPawn.IsQuestLodger() || TalkRequestPool.IsEmpty || global::Ustas.RimAI.Communication.Util.PawnUtil.IsInDanger(selectedPawn,true)) talkGenerated=false;
                else
                {
                    var request = TalkRequestPool.GetRequestFromPool(selectedPawn);
                    talkGenerated = request != null && TalkService.GenerateTalk(request);
                }

                // 2. If the pawn has a specific talk request, try generating it
                if (!talkGenerated)
                {
                    var pawnState = global::Ustas.RimAI.Communication.Data.Cache.Get(selectedPawn);
                    if (pawnState.GetNextTalkRequest() != null)
                    {
                        talkGenerated = TalkService.GenerateTalk(pawnState.GetNextTalkRequest());
                        if(talkGenerated && pawnState.TalkRequests.Count > 0)
                            pawnState.TalkRequests.RemoveFirst();
                    }
                }

                // 3. Fallback: generate based on current context if nothing else worked
                if (!talkGenerated)
                {
                    TalkRequest talkRequest = new TalkRequest(null, selectedPawn);
                    TalkService.GenerateTalk(talkRequest);
                }
            }
        }

        private static void ignoreButtonFunc()
        {
            Messages.Message("Ustas.RimAI.Communication.Voices.IgnoreComplete".Translate(), MessageTypeDefOf.TaskCompletion, false);
            
            foreach (var pawn in global::Ustas.RimAI.Communication.Data.Cache.GetAll())
            {
                pawn.IgnoreAllTalkResponses();
            }
        }
        
        private static void displayButtonFunc()
        {
            displayPassport = true;
            TalkService.DisplayTalk();
            displayPassport = false;
        }

        [HarmonyPatch]
        public static class CommonUtil_HasPassed_Prefix
        {
            static MethodBase TargetMethod()
            {
                return typeof(CommonUtil).GetMethod("HasPassed", BindingFlags.Public | BindingFlags.Static);
            }

            static bool Prefix(int pastTick, double seconds, ref bool __result)
            {
                if (displayPassport)
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }
    }
}