using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using Ustas.RimAI.Communication.Voices.Data;
using Ustas.RimAI.Communication.Voices.Service;
using Ustas.RimAI.Communication.Voices.Patch;
using Ustas.RimAI.Core.Storage;

namespace Ustas.RimAI.Communication.Voices.UI
{
    using static SettingsUIState;

    public static class SettingsUI
    {
        internal static bool LocalUploadFileExists(string path) =>
            !string.IsNullOrWhiteSpace(path) && LocalStorage.Current.FileExists(path);

        public static void DrawTTSSettings(Rect inRect, TTSSettings settings)
        {
            while (pendingActions.TryDequeue(out var act))
            {
                act?.Invoke();
            }

            while (pendingMessages.TryDequeue(out var _m))
            {
                Messages.Message(_m.text, _m.type, false);
            }

            float baseHeight = 2000f;
            float voiceModelRowHeight = 40f;
            var supplierVoiceModels = settings.GetSupplierVoiceModels(settings.Supplier);
            int voiceModelCount = supplierVoiceModels?.Count ?? 0;
            float uploadSectionHeight = (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS) ? 280f : 0f;
            float resetButtonHeight = (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS || settings.Supplier == TTSSettings.TTSSupplier.AzureTTS || settings.Supplier == TTSSettings.TTSSupplier.EdgeTTS) ? 40f : 0f;
            float openAiSectionHeight = settings.Supplier == TTSSettings.TTSSupplier.OpenAI ? 400f : 0f;
            float rulesSectionHeight = showRulesList ? 60f : 0f;
            float contentHeight = baseHeight + (voiceModelCount * voiceModelRowHeight) + uploadSectionHeight + resetButtonHeight + openAiSectionHeight + rulesSectionHeight;
            bool isOn = settings.EnableTTS;

            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);

            Widgets.BeginScrollView(inRect, ref mainScrollPosition, viewRect);

            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            listing.CheckboxLabeled("Ustas.RimAI.Communication.Settings.TTS.Enable".Translate(), ref settings.EnableTTS, "Ustas.RimAI.Communication.Settings.TTS.EnableTooltip".Translate());

            if (isOn != settings.EnableTTS)
            {
                if (!settings.EnableTTS)
                {
                    AudioPlaybackService.StopAndClear();
                    Log.Message("[RimAI.Voices] TTS disabled via settings");
                    listing.End();
                    Widgets.EndScrollView();
                    return;
                }
                else
                {
                    if (Find.CurrentMap != null)
                    {
                        TTSService.ReloadMap(Find.CurrentMap);
                        Log.Message("[RimAI.Voices] TTS enabled via settings, reloading map pawns");
                    }
                }
            }

            listing.Gap();

            listing.CheckboxLabeled("Ustas.RimAI.Communication.Settings.TTS.ButtonEnable".Translate(), ref settings.ButtonDisplay, "Ustas.RimAI.Communication.Settings.TTS.ButtonEnableTooltip".Translate());

            listing.Gap();

            DrawAutomaticVoicesToggle(listing, settings);

            listing.Gap();

            DrawApiConfigSection(listing, settings);

            listing.Gap();

            SettingsUIProcessingPromptPanel.DrawProcessingPromptSection(listing, settings);

            listing.Gap();

            Text.Font = GameFont.Medium;
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.TTSConfig".Translate());
            Text.Font = GameFont.Small;

            listing.Label("Ustas.RimAI.Communication.Settings.TTS.TTSSupplier".Translate());
            Rect supplierRect = listing.GetRect(Text.LineHeight);
            string supplierDisplay = SupplierString(settings.Supplier);

            if (Widgets.ButtonText(supplierRect, supplierDisplay))
            {
                var supplierOrder = new[]
                {
                    TTSSettings.TTSSupplier.OpenAI,
                    TTSSettings.TTSSupplier.FishAudio,
                    TTSSettings.TTSSupplier.CosyVoice,
                    TTSSettings.TTSSupplier.IndexTTS,
                    TTSSettings.TTSSupplier.AzureTTS,
                    TTSSettings.TTSSupplier.EdgeTTS,
                    TTSSettings.TTSSupplier.GeminiTTS,
                    TTSSettings.TTSSupplier.TTSWebUI,
                    TTSSettings.TTSSupplier.None
                };

                var options = new System.Collections.Generic.List<FloatMenuOption>();
                foreach (var candidate in supplierOrder)
                {
                    var selected = candidate;
                    options.Add(new FloatMenuOption(SupplierString(selected), delegate
                    {
                        SelectSupplier(settings, selected);
                    }));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap();

            SettingsUISupplierPanel.DrawSupplierRuntimeSection(listing, settings, viewRect.width);

            listing.End();
            Widgets.EndScrollView();
        }

        internal static bool IsAutomaticActive(TTSSettings settings) =>
            settings.AutomaticPawnVoices && Voice.PawnVoiceRenderer.SupportsAutomaticVoices(settings.Supplier);

    internal static void DrawAutomaticVoicesToggle(Listing_Standard listing, TTSSettings settings)
    {
        listing.CheckboxLabeled(
            "Ustas.RimAI.Communication.Settings.TTS.AutomaticVoices".Translate(),
            ref settings.AutomaticPawnVoices,
            "Ustas.RimAI.Communication.Settings.TTS.AutomaticVoicesTooltip".Translate());

        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;
        listing.Label(IsAutomaticActive(settings)
            ? "Ustas.RimAI.Communication.Settings.TTS.AutomaticVoicesActive".Translate()
            : "Ustas.RimAI.Communication.Settings.TTS.AutomaticVoicesUnsupported".Translate(SupplierString(settings.Supplier)));
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
    }

    internal static void SelectSupplier(TTSSettings settings, TTSSettings.TTSSupplier supplier)
    {
        settings.Supplier = supplier;

        if (supplier == TTSSettings.TTSSupplier.OpenAI)
        {
            SettingsUIOpenAiPanel.EnsureOpenAiDefaults(settings);
        }

        TTSService.SetProvider(settings.Supplier, settings);
    }
    internal static void DrawApiConfigSection(Listing_Standard listing, TTSSettings settings)
    {
        Text.Font = GameFont.Medium;
        listing.Label("Ustas.RimAI.Communication.Settings.TTS.LLMApiConfig".Translate());
        Text.Font = GameFont.Small;

        listing.Gap(6f);

        listing.CheckboxLabeled(
            "Ustas.RimAI.Communication.Settings.TTS.UseLlmPreprocess".Translate(),
            ref settings.EnableTextPreprocessing);

        listing.Gap(6f);

        string model = VoiceSharedAiText.EffectiveModel;
        if (string.IsNullOrWhiteSpace(model))
            model = "Ustas.RimAI.Communication.Settings.TTS.NotSet".Translate();
        listing.Label("Ustas.RimAI.Communication.Settings.TTS.SharedModel".Translate(model));
        listing.Label("Ustas.RimAI.Communication.Settings.TTS.SharedLanguage".Translate(VoiceSharedAiText.Language));
        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;
        listing.Label("Ustas.RimAI.Communication.Settings.TTS.SharedLanguageHint".Translate());
        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        listing.Gap(6f);

        listing.CheckboxLabeled(
            "Ustas.RimAI.Communication.Settings.TTS.RemoveBracketsInPreProcess".Translate(),
            ref settings.RemoveBracketsInPreProcess,
            "Ustas.RimAI.Communication.Settings.TTS.RemoveBracketsInPreProcessTooltip".Translate());
    }

    internal static string SupplierString(TTSSettings.TTSSupplier supplier)
    {
        return supplier switch
        {
            TTSSettings.TTSSupplier.FishAudio => "Ustas.RimAI.Communication.Settings.TTS.TTSSupplier.FishAudio".Translate(),
            TTSSettings.TTSSupplier.CosyVoice => "Ustas.RimAI.Communication.Settings.TTS.TTSSupplier.CosyVoice".Translate(),
            TTSSettings.TTSSupplier.IndexTTS => "Ustas.RimAI.Communication.Settings.TTS.TTSSupplier.IndexTTS".Translate(),
            TTSSettings.TTSSupplier.AzureTTS => "Ustas.RimAI.Communication.Settings.TTS.TTSSupplier.AzureTTS".Translate(),
            TTSSettings.TTSSupplier.EdgeTTS => "Ustas.RimAI.Communication.Settings.TTS.TTSSupplier.EdgeTTS".Translate(),
            TTSSettings.TTSSupplier.GeminiTTS => "Ustas.RimAI.Communication.Settings.TTS.TTSSupplier.GeminiTTS".Translate(),
            TTSSettings.TTSSupplier.TTSWebUI => "Ustas.RimAI.Communication.Settings.TTS.TTSSupplier.TTSWebUI".Translate(),
            TTSSettings.TTSSupplier.OpenAI => "Ustas.RimAI.Communication.Settings.TTS.TTSSupplier.OpenAI".Translate(),
            TTSSettings.TTSSupplier.None => "Ustas.RimAI.Communication.Settings.TTS.None".Translate(),
            _ => supplier.ToString(),
        };
    }
    }
}
