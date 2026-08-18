using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using Ustas.RimAI.Communication.Voices.Data;
using Ustas.RimAI.Communication.Voices.Service;
using Ustas.RimAI.Communication.Voices.Patch;

namespace Ustas.RimAI.Communication.Voices.UI
{
    using static SettingsUIState;

    internal static class SettingsUIProcessingPromptPanel
    {
    internal static void DrawProcessingPromptSection(Listing_Standard listing, TTSSettings settings)
    {
        listing.Label("Ustas.RimAI.Communication.Settings.TTS.ProcessingPromptLabel".Translate());
        
        // Initialize buffer if needed - show default prompt if custom is empty
        if (!processingPromptInitialized)
        {
            processingPromptBuffer = string.IsNullOrWhiteSpace(settings.CustomTTSProcessingPrompt)
                ? Data.TTSConstant.DefaultTTSProcessingPrompt
                : settings.CustomTTSProcessingPrompt;
            processingPromptInitialized = true;
        }

        // Instructions
        Text.Font = GameFont.Tiny;
        GUI.color = Color.cyan;
        Rect tipRect = listing.GetRect(Text.LineHeight);
        Widgets.Label(tipRect, "Ustas.RimAI.Communication.Settings.TTS.ProcessingPromptTip".Translate());
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        listing.Gap(6f);

        // Text area for prompt - display buffer which contains either custom or default
        float textAreaHeight = 120f;
        Rect textAreaRect = listing.GetRect(textAreaHeight);
        string displayPrompt = processingPromptBuffer;
        string newPrompt = Widgets.TextArea(textAreaRect, displayPrompt);

        // Only save if user actually modified the content
        if (newPrompt != displayPrompt)
        {
            processingPromptBuffer = newPrompt.Replace("\\n", "\n");
            settings.CustomTTSProcessingPrompt = processingPromptBuffer;
        }

        listing.Gap(6f);

        // Reset buttons - First row: FishAudio, CosyVoice, IndexTTS
        Rect resetButtonsRect1 = listing.GetRect(30f);
        float gap = 4f;
        float btnW = (resetButtonsRect1.width - gap * 2) / 3f;
        Rect fishRect = new Rect(resetButtonsRect1.x, resetButtonsRect1.y, btnW, resetButtonsRect1.height);
        Rect cosyRect = new Rect(resetButtonsRect1.x + btnW + gap, resetButtonsRect1.y, btnW, resetButtonsRect1.height);
        Rect indexRect = new Rect(resetButtonsRect1.x + (btnW + gap) * 2f, resetButtonsRect1.y, btnW, resetButtonsRect1.height);

        if (Widgets.ButtonText(fishRect, "Ustas.RimAI.Communication.Settings.TTS.ResetPrompt.FishAudio".Translate()))
        {
            settings.CustomTTSProcessingPrompt = "";
            processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt;
        }

        if (Widgets.ButtonText(cosyRect, "Ustas.RimAI.Communication.Settings.TTS.ResetPrompt.CosyVoice".Translate()))
        {
            settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_CosyVoice;
            processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_CosyVoice;
        }

        if (Widgets.ButtonText(indexRect, "Ustas.RimAI.Communication.Settings.TTS.ResetPrompt.IndexTTS".Translate()))
        {
            settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_IndexTTS;
            processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_IndexTTS;
        }

        // Reset buttons - Second row: AzureTTS, EdgeTTS, GeminiTTS
        listing.Gap(6f);
        Rect resetButtonsRect2 = listing.GetRect(30f);
        Rect azureRect = new Rect(resetButtonsRect2.x, resetButtonsRect2.y, btnW, resetButtonsRect2.height);
        Rect edgeRect = new Rect(resetButtonsRect2.x + btnW + gap, resetButtonsRect2.y, btnW, resetButtonsRect2.height);
        Rect geminiRect = new Rect(resetButtonsRect2.x + (btnW + gap) * 2f, resetButtonsRect2.y, btnW, resetButtonsRect2.height);

        if (Widgets.ButtonText(azureRect, "Ustas.RimAI.Communication.Settings.TTS.ResetPrompt.AzureTTS".Translate()))
        {
            settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_AzureTTS;
            processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_AzureTTS;
        }

        if (Widgets.ButtonText(edgeRect, "Ustas.RimAI.Communication.Settings.TTS.ResetPrompt.EdgeTTS".Translate()))
        {
            settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_EdgeTTS;
            processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_EdgeTTS;
        }

        if (Widgets.ButtonText(geminiRect, "Ustas.RimAI.Communication.Settings.TTS.ResetPrompt.GeminiTTS".Translate()))
        {
            settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_GeminiTTS;
            processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_GeminiTTS;
        }

        // Reset buttons - Third row: TTSWebUI, OpenAI
        listing.Gap(6f);
        Rect resetButtonsRect3 = listing.GetRect(30f);
        Rect ttswebuiRect = new Rect(resetButtonsRect3.x, resetButtonsRect3.y, btnW, resetButtonsRect3.height);
        Rect openAiRect = new Rect(resetButtonsRect3.x + btnW + gap, resetButtonsRect3.y, btnW, resetButtonsRect3.height);

        if (Widgets.ButtonText(ttswebuiRect, "Ustas.RimAI.Communication.Settings.TTS.ResetPrompt.TTSWebUI".Translate()))
        {
            settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_TTSWebUI;
            processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_TTSWebUI;
        }

        if (Widgets.ButtonText(openAiRect, "Ustas.RimAI.Communication.Settings.TTS.ResetPrompt.OpenAI".Translate()))
        {
            settings.CustomTTSProcessingPrompt = Data.TTSConstant.DefaultTTSProcessingPrompt_OpenAI;
            processingPromptBuffer = Data.TTSConstant.DefaultTTSProcessingPrompt_OpenAI;
        }
    }
    }
}
