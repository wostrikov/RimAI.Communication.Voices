using System;
using Ustas.RimAI.Communication.UI;
using Ustas.RimAI.Communication.Voices.Data;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Voices.Patch
{
    public static class PersonaEditorPatch
    {
        public static void DrawFooter(PersonaEditorWindow window, Pawn pawn, Rect inRect)
        {
            try
            {
                if (!TTSConfig.IsEnabled)
                    return;
                DrawVoiceModelButton(inRect, pawn);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Voices] PersonaEditor footer error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void DrawVoiceModelButton(Rect inRect, Pawn pawn)
        {
            float buttonWidth = 120f;
            float buttonHeight = 20f;
            float bottomMargin = 90f;

            Rect buttonRect = new Rect(
                inRect.x + 360f,
                inRect.yMax - buttonHeight - bottomMargin,
                buttonWidth,
                buttonHeight
            );

            if (Widgets.ButtonText(buttonRect, "Ustas.RimAI.Communication.PersonaEditor.VoiceModel".Translate()))
            {
                Find.WindowStack.Add(new UI.VoiceSelectionWindow(pawn));
            }
        }
    }
}
