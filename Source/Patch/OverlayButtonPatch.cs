using UnityEngine;
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

        public static void Draw(Rect gearRect)
        {
            try
            {
                if (!TTSConfig.IsEnabled) return;

                float resetBtnWidth = 120f;
                float generateBtnWidth = 150f;
                float ignoreBtnWidth = 150f;
                float displayBtnWidth = 150f;
                float btnHeight = Mathf.Max(gearRect.height, 28f);
                float padding = 6f;
                resetButtonScreenRect.Set(gearRect.x - resetBtnWidth - padding, gearRect.y, resetBtnWidth, btnHeight);
                generateButtonScreenRect.Set(gearRect.x - resetBtnWidth - generateBtnWidth - 2 * padding, gearRect.y, generateBtnWidth, btnHeight);
                ignoreButtonScreenRect.Set(gearRect.x - resetBtnWidth - generateBtnWidth - ignoreBtnWidth - 2 * padding, gearRect.y, ignoreBtnWidth, btnHeight);
                displayButtonScreenRect.Set(gearRect.x - resetBtnWidth - generateBtnWidth - ignoreBtnWidth - displayBtnWidth - 2 * padding, gearRect.y, ignoreBtnWidth, btnHeight);

                if (Widgets.ButtonText(resetButtonScreenRect, "Ustas.RimAI.Communication.Voices.Reset".Translate()))
                    ResetButtonFunc();
                if (Widgets.ButtonText(generateButtonScreenRect, "Ustas.RimAI.Communication.Voices.Generate".Translate()))
                    generateButtonFunc();
                if (Widgets.ButtonText(ignoreButtonScreenRect, "Ustas.RimAI.Communication.Voices.Ignore".Translate()))
                    ignoreButtonFunc();
                if (Widgets.ButtonText(displayButtonScreenRect, "Ustas.RimAI.Communication.Voices.Display".Translate()))
                    displayButtonFunc();
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Voices] Overlay chrome draw exception: {ex}");
            }
        }

        public static bool TryConsumeClick(Event currentEvent)
        {
            if (!TTSConfig.IsEnabled) return false;
            if (currentEvent == null) return false;
            if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0)
                return false;

            if (resetButtonScreenRect.Contains(currentEvent.mousePosition))
            {
                currentEvent.Use();
                ResetButtonFunc();
                return true;
            }
            if (generateButtonScreenRect.Contains(currentEvent.mousePosition))
            {
                currentEvent.Use();
                generateButtonFunc();
                return true;
            }
            if (ignoreButtonScreenRect.Contains(currentEvent.mousePosition))
            {
                currentEvent.Use();
                ignoreButtonFunc();
                return true;
            }
            if (displayButtonScreenRect.Contains(currentEvent.mousePosition))
            {
                currentEvent.Use();
                displayButtonFunc();
                return true;
            }

            return false;
        }

        private static void ResetButtonFunc()
        {
            Messages.Message("Ustas.RimAI.Communication.Voices.ResetComplete".Translate(), MessageTypeDefOf.TaskCompletion, false);
            TTSService.StopAll(false);
        }

        private static void generateButtonFunc()
        {
            Messages.Message("Ustas.RimAI.Communication.Voices.GenerateComplete".Translate(), MessageTypeDefOf.TaskCompletion, false);
            Pawn selectedPawn = PawnSelector.SelectNextAvailablePawn();

            if (selectedPawn != null)
            {
                bool talkGenerated;
                if (!selectedPawn.IsFreeNonSlaveColonist || selectedPawn.IsQuestLodger() || TalkRequestPool.IsEmpty || PawnUtil.IsInDanger(selectedPawn, true)) talkGenerated = false;
                else
                {
                    var request = TalkRequestPool.GetRequestFromPool(selectedPawn);
                    talkGenerated = request != null && TalkService.GenerateTalk(request);
                }

                if (!talkGenerated)
                {
                    var pawnState = global::Ustas.RimAI.Communication.Data.Cache.Get(selectedPawn);
                    if (pawnState.GetNextTalkRequest() != null)
                    {
                        talkGenerated = TalkService.GenerateTalk(pawnState.GetNextTalkRequest());
                        if (talkGenerated && pawnState.TalkRequests.Count > 0)
                            pawnState.TalkRequests.RemoveFirst();
                    }
                }

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
                pawn.IgnoreAllTalkResponses();
        }

        private static void displayButtonFunc()
        {
            TalkService.DisplayTalk(ignoreReplyInterval: true);
        }
    }
}
