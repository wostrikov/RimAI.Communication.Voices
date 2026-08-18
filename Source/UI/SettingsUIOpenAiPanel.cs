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

    internal static class SettingsUIOpenAiPanel
    {
    /// <summary>
    /// Fill in the endpoint, model and voice presets so OpenAI is usable straight after
    /// being selected instead of requiring manual entry.
    /// </summary>
    internal static void EnsureOpenAiDefaults(TTSSettings settings)
    {
        string baseUrl = settings.GetSupplierRegion(TTSSettings.TTSSupplier.OpenAI);
        if (string.IsNullOrWhiteSpace(baseUrl) || !baseUrl.TrimStart().StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
        {
            settings.SetSupplierRegion(TTSSettings.TTSSupplier.OpenAI, Service.OpenAITTSClient.DefaultBaseUrl);
        }

        if (string.IsNullOrWhiteSpace(settings.GetSupplierModel(TTSSettings.TTSSupplier.OpenAI)))
        {
            settings.SetSupplierModel(TTSSettings.TTSSupplier.OpenAI, Service.OpenAITTSClient.DefaultModel);
        }

        var voiceModels = settings.GetSupplierVoiceModels(TTSSettings.TTSSupplier.OpenAI);
        if (voiceModels == null || voiceModels.Count == 0)
        {
            settings.SetSupplierVoiceModels(
                TTSSettings.TTSSupplier.OpenAI,
                TTSSettings.GetDefaultVoiceModels(TTSSettings.TTSSupplier.OpenAI));
        }
    }

    internal static void DrawOpenAiCredential(Listing_Standard listing)
    {
        bool present = Data.OpenAITtsCredential.Present;
        GUI.color = present ? Color.green : Color.red;
        listing.Label("Ustas.RimAI.Communication.Settings.TTS.OpenAI.Credential".Translate(Data.OpenAITtsCredential.Display));
        GUI.color = Color.white;

        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;
        listing.Label("Ustas.RimAI.Communication.Settings.TTS.OpenAI.CredentialHint".Translate(Data.OpenAITtsCredential.Variable));
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
    }

    internal static void DrawOpenAiSection(Listing_Standard listing, TTSSettings settings)
    {
        var supplier = TTSSettings.TTSSupplier.OpenAI;
        EnsureOpenAiDefaults(settings);

        string baseUrl = settings.GetSupplierRegion(supplier);
        listing.Label("Ustas.RimAI.Communication.Settings.TTS.OpenAI.BaseUrlLabel".Translate(baseUrl));
        listing.Gap(6f);

        listing.Label("Ustas.RimAI.Communication.Settings.TTS.OpenAI.CustomUrlLabel".Translate());
        string editedUrl = listing.TextEntry(baseUrl);
        if (editedUrl != baseUrl)
        {
            settings.SetSupplierRegion(supplier, editedUrl);
            TTSService.SetProvider(supplier, settings);
        }

        listing.Gap(6f);

        // Model selection from a list instead of free text
        string currentModel = settings.GetSupplierModel(supplier);
        if (string.IsNullOrWhiteSpace(currentModel))
            currentModel = Service.OpenAITTSClient.DefaultModel;

        listing.Label("Ustas.RimAI.Communication.Settings.TTS.OpenAI.ModelLabel".Translate(currentModel));
        Rect modelRect = listing.GetRect(30f);
        if (Widgets.ButtonText(modelRect, currentModel))
        {
            var options = new System.Collections.Generic.List<FloatMenuOption>();
            foreach (var candidate in BuildOpenAiModelChoices())
            {
                var picked = candidate;
                options.Add(new FloatMenuOption(picked, delegate
                {
                    settings.SetSupplierModel(supplier, picked);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        listing.Gap(6f);

        GUI.enabled = !openAiModelsLoading;
        Rect refreshRect = listing.GetRect(30f);
        if (Widgets.ButtonText(refreshRect, openAiModelsLoading
                ? "Ustas.RimAI.Communication.Settings.TTS.OpenAI.ModelsLoading".Translate()
                : "Ustas.RimAI.Communication.Settings.TTS.OpenAI.RefreshModels".Translate()))
        {
            RefreshOpenAiModels(settings);
        }
        GUI.enabled = true;

        listing.Gap(6f);

        // Audio container
        string currentFormat = settings.GetSupplierResponseFormat(supplier);
        listing.Label("Ustas.RimAI.Communication.Settings.TTS.OpenAI.FormatLabel".Translate(currentFormat));
        Rect formatRect = listing.GetRect(30f);
        if (Widgets.ButtonText(formatRect, currentFormat))
        {
            var options = new System.Collections.Generic.List<FloatMenuOption>();
            foreach (var format in Service.OpenAITTSClient.ResponseFormats)
            {
                var picked = format;
                options.Add(new FloatMenuOption(picked, delegate
                {
                    settings.SetSupplierResponseFormat(supplier, picked);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        listing.Gap(6f);

        // In automatic mode the renderer owns instructions, so a manual field here
        // would be written and then ignored.
        if (SettingsUI.IsAutomaticActive(settings))
        {
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.OpenAI.InstructionsAutomatic".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            return;
        }

        // Delivery instructions (style steering)
        listing.Label("Ustas.RimAI.Communication.Settings.TTS.OpenAI.InstructionsLabel".Translate());
        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;
        listing.Label("Ustas.RimAI.Communication.Settings.TTS.OpenAI.InstructionsHint".Translate());
        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        string instructions = settings.GetSupplierInstructions(supplier) ?? "";
        Rect instructionsRect = listing.GetRect(70f);
        string editedInstructions = Widgets.TextArea(instructionsRect, instructions);
        if (editedInstructions != instructions)
            settings.SetSupplierInstructions(supplier, editedInstructions);

        listing.Gap(6f);
        Rect exampleRect = listing.GetRect(30f);
        if (Widgets.ButtonText(exampleRect, "Ustas.RimAI.Communication.Settings.TTS.OpenAI.InstructionsExample".Translate()))
        {
            settings.SetSupplierInstructions(
                supplier,
                "Ustas.RimAI.Communication.Settings.TTS.OpenAI.InstructionsExampleText".Translate());
        }
    }

    internal static System.Collections.Generic.List<string> BuildOpenAiModelChoices()
    {
        var choices = new System.Collections.Generic.List<string>(Service.OpenAITTSClient.KnownModels);
        foreach (var fetched in openAiModelChoices)
        {
            if (!choices.Contains(fetched))
                choices.Add(fetched);
        }
        return choices;
    }

    internal static void RefreshOpenAiModels(TTSSettings settings)
    {
        string apiKey = Data.OpenAITtsCredential.Resolve();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Messages.Message(
                "Ustas.RimAI.Communication.Settings.TTS.OpenAI.CredentialMissing".Translate(Data.OpenAITtsCredential.Variable),
                MessageTypeDefOf.RejectInput,
                false);
            return;
        }

        Service.OpenAITTSClient.SetBaseUrl(settings.GetSupplierRegion(TTSSettings.TTSSupplier.OpenAI));
        openAiModelsLoading = true;

        System.Threading.Tasks.Task.Run(async () =>
        {
            var models = await Service.OpenAITTSClient.ListSpeechModelsAsync(apiKey);
            EnqueueMainThreadAction(() =>
            {
                openAiModelsLoading = false;
                if (models != null && models.Count > 0)
                {
                    openAiModelChoices = models;
                    Messages.Message(
                        "Ustas.RimAI.Communication.Settings.TTS.OpenAI.ModelsRefreshed".Translate(models.Count),
                        MessageTypeDefOf.TaskCompletion,
                        false);
                }
                else
                {
                    Messages.Message(
                        "Ustas.RimAI.Communication.Settings.TTS.OpenAI.ModelsRefreshFailed".Translate(),
                        MessageTypeDefOf.RejectInput,
                        false);
                }
            });
        });
    }
    }
}
