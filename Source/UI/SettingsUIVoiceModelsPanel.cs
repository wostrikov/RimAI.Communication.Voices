using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using Ustas.RimAI.Communication.Voices.Data;
using Ustas.RimAI.Communication.Voices.Service;
using Ustas.RimAI.Communication.Voices.Patch;
using RimAI.Core.Runtime;

namespace Ustas.RimAI.Communication.Voices.UI
{
    using static SettingsUIState;

    internal static class SettingsUIVoiceModelsPanel
    {
    internal static void DrawVoiceModelsSection(Listing_Standard listing, TTSSettings settings, float width, System.Collections.Generic.List<VoiceModel> voiceModels)
    {
        Text.Font = GameFont.Medium;
        listing.Label("Ustas.RimAI.Communication.Settings.TTS.VoiceModels".Translate());
        Text.Font = GameFont.Small;

        listing.Label("Ustas.RimAI.Communication.Settings.TTS.DefaultVoiceModel".Translate());

        // Show default model selector (now includes RULE_BASED as an option)
        SettingsUIVoiceRulesPanel.DrawSimpleDefaultVoiceSelector(listing, settings, voiceModels);

        listing.Gap(6f);

        // Rules button and list
        if (listing.ButtonText("Ustas.RimAI.Communication.Settings.TTS.Rules".Translate()))
        {
            // Toggle rules visibility (using a static variable)
            showRulesList = !showRulesList;
        }

        if (showRulesList)
        {
            listing.Gap(6f);
            SettingsUIVoiceRulesPanel.DrawVoiceRulesList(listing, settings, width, voiceModels);
        }

        // Player reference voice selection (always shown)
        SettingsUIVoiceRulesPanel.DrawPlayerVoiceSelector(listing, settings);

        // Voice model list (model configurations)
        DrawVoiceModelsList(listing, settings, width, voiceModels);
    }
    internal static void DrawVoiceModelsList(Listing_Standard listing, TTSSettings settings, float width, System.Collections.Generic.List<VoiceModel> voiceModels)
    {
        Rect headerRect = listing.GetRect(24f);
        Rect addButtonRect = new Rect(headerRect.x + headerRect.width - 65f, headerRect.y, 30f, 24f);
        Rect removeButtonRect = new Rect(headerRect.x + headerRect.width - 30f, headerRect.y, 30f, 24f);
        headerRect.width -= 70f;

        Widgets.Label(headerRect, "Ustas.RimAI.Communication.Settings.TTS.ModelConfigurations".Translate());

        listing.Gap(6f);

        // Upload user voice section (only shown when supplier supports SiliconFlow)
        if (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS)
        {
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.UploadUserVoiceLabel".Translate());
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.UploadFilePath".Translate());
            uploadPathBuffer = listing.TextEntry(uploadPathBuffer ?? "");
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.UploadName".Translate());
            uploadNameBuffer = listing.TextEntry(uploadNameBuffer ?? "");
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.UploadTextPreview".Translate());
            uploadTextBuffer = listing.TextEntry(uploadTextBuffer ?? "");
            Rect uploadRect = listing.GetRect(30f);
            if (Widgets.ButtonText(uploadRect, "Ustas.RimAI.Communication.Settings.TTS.UploadButton".Translate()))
            {
                // Validate local file
                if (string.IsNullOrWhiteSpace(uploadPathBuffer) || !SettingsUI.LocalUploadFileExists(uploadPathBuffer))
                {
                    Messages.Message("Ustas.RimAI.Communication.Voices.UploadFailed.LocalFileNotFound".Translate(), MessageTypeDefOf.RejectInput, false);
                }
                else if (string.IsNullOrWhiteSpace(uploadNameBuffer))
                {
                    Messages.Message("Ustas.RimAI.Communication.Voices.UploadFailed.NameEmpty".Translate(), MessageTypeDefOf.RejectInput, false);
                }
                else
                {
                    // Kick off upload in background
                    var apiKey = settings.GetSupplierApiKey(settings.Supplier);
                    var model = settings.GetSupplierModel(settings.Supplier);
                    RimAiBackground.Run(async () =>
                    {
                        var uri = await Service.SiliconFlowClient.UploadUserVoiceAsync(apiKey, model, uploadPathBuffer, uploadNameBuffer, uploadTextBuffer);
                        if (!string.IsNullOrWhiteSpace(uri))
                        {
                            // Defer the Refresh and message to run on the main thread
                            EnqueueMainThreadAction(() =>
                            {
                                Refresh();
                                Messages.Message("Ustas.RimAI.Communication.Voices.UploadComplete".Translate(), MessageTypeDefOf.TaskCompletion, false);
                            });
                        }
                        else
                        {
                            EnqueueMainThreadAction(() => Messages.Message("Ustas.RimAI.Communication.Voices.UploadFailed.ServerError".Translate(), MessageTypeDefOf.RejectInput, false));
                        }
                    });
                }
            }

            listing.Gap(6f);
        }

        if (Widgets.ButtonText(addButtonRect, "+"))
        {
            if (voiceModels == null)
                voiceModels = new System.Collections.Generic.List<VoiceModel>();
            voiceModels.Add(new VoiceModel { ModelName = "", ModelId = "" });
            settings.SetSupplierVoiceModels(settings.Supplier, voiceModels);
        }

        GUI.enabled = voiceModels != null && voiceModels.Count > 0;
        if (Widgets.ButtonText(removeButtonRect, "−"))
        {
            if (voiceModels != null && voiceModels.Count > 0)
            {
                voiceModels.RemoveAt(voiceModels.Count - 1);
                settings.SetSupplierVoiceModels(settings.Supplier, voiceModels);
            }
        }
        GUI.enabled = true;

        listing.Gap(6f);

        // Column descriptions
        listing.Label("Ustas.RimAI.Communication.Settings.TTS.ColumnDescription".Translate());
        listing.Gap(6f);

        // Draw table headers
        Rect tableHeaderRect = listing.GetRect(24f);
        float x = tableHeaderRect.x;
        float y = tableHeaderRect.y;
        float height = tableHeaderRect.height;

        x += 60f; // Space for reorder buttons

        float nameWidth = (width - 130f) * 0.4f;
        float idWidth = (width - 130f) * 0.4f;

        Rect nameHeaderRect = new Rect(x, y, nameWidth, height);
        Widgets.Label(nameHeaderRect, "Ustas.RimAI.Communication.Settings.TTS.ColumnModelName".Translate());
        x += nameWidth + 5f;

        Rect idHeaderRect = new Rect(x, y, idWidth, height);
        Widgets.Label(idHeaderRect, "Ustas.RimAI.Communication.Settings.TTS.ColumnModelID".Translate());

        // Draw each model config row
        if (voiceModels != null)
        {
            for (int i = 0; i < voiceModels.Count; i++)
            {
                DrawModelConfigRow(listing, voiceModels[i], i, voiceModels, width);
            }
        }

        if (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS || settings.Supplier == TTSSettings.TTSSupplier.AzureTTS || settings.Supplier == TTSSettings.TTSSupplier.EdgeTTS || settings.Supplier == TTSSettings.TTSSupplier.GeminiTTS || settings.Supplier == TTSSettings.TTSSupplier.OpenAI)
        {
            listing.Gap(6f);
            // Single Reset Models button placed after the full list
            Rect resetAllRect = listing.GetRect(30f);
            if (Widgets.ButtonText(resetAllRect, "Ustas.RimAI.Communication.Settings.TTS.ResetModelsButton".Translate()))
            {
                Refresh();
            }
        }

        // Voice library button for AzureTTS/EdgeTTS
        if (settings.Supplier == TTSSettings.TTSSupplier.AzureTTS || settings.Supplier == TTSSettings.TTSSupplier.EdgeTTS)
        {
            listing.Gap(6f);
            Rect voiceLibraryRect = listing.GetRect(30f);
            string buttonLabel = settings.Supplier == TTSSettings.TTSSupplier.AzureTTS 
                ? "Ustas.RimAI.Communication.Settings.TTS.AzureVoiceLibrary".Translate() 
                : "Ustas.RimAI.Communication.Settings.TTS.EdgeVoiceLibrary".Translate();
            if (Widgets.ButtonText(voiceLibraryRect, buttonLabel))
            {
                Find.WindowStack.Add(new VoiceLibraryWindow(settings.Supplier));
            }
        }
    }

    internal static void Refresh()
    {
        var settings = TTSConfig.Settings;
        var voiceModels = settings.GetSupplierVoiceModels(settings.Supplier);
        var presets = TTSSettings.GetDefaultVoiceModels(settings.Supplier);
        if (presets != null && presets.Count > 0)
        {
            // Merge presets with existing user models: keep presets first, then
            // append any custom/empty entries that aren't already in presets.
            var merged = new System.Collections.Generic.List<VoiceModel>();
            foreach (var p in presets)
            {
                if (p == null) continue;
                merged.Add(new VoiceModel { ModelId = p.ModelId, ModelName = p.ModelName });
            }

            if (voiceModels != null)
            {
                foreach (var vm in voiceModels)
                {
                    if (vm == null) continue;
                    // preserve blank/custom entries (no ModelId) and any models not present in presets
                    if (string.IsNullOrWhiteSpace(vm.ModelId) || !merged.Any(x => x.ModelId == vm.ModelId))
                    {
                        merged.Add(new VoiceModel { ModelId = vm.ModelId, ModelName = vm.ModelName });
                    }
                }
            }

            settings.SetSupplierVoiceModels(settings.Supplier, merged);
            voiceModels = settings.GetSupplierVoiceModels(settings.Supplier);
        }

    // Also: when ResetModels is pressed above we attempt to sync user-uploaded voices from SiliconFlow.
    // The network call is done asynchronously and will merge any returned user voices into the settings.
    // (This runs when the user pressed ResetModels; the above code already applied system presets.)
        if (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice || settings.Supplier == TTSSettings.TTSSupplier.IndexTTS)
        {
            var apiKey = settings.GetSupplierApiKey(settings.Supplier);
            var supplier = settings.Supplier;
            RimAiBackground.Run(async () =>
            {
                var list = await Service.SiliconFlowClient.ListUserVoicesAsync(apiKey);
                if (list != null && list.Count > 0)
                {
                    var current = settings.GetSupplierVoiceModels(supplier) ?? new System.Collections.Generic.List<Data.VoiceModel>();
                    bool changed = false;
                    foreach (var t in list)
                    {
                        if (!current.Exists(x => x.ModelId == t.Item1))
                        {
                            current.Add(new Data.VoiceModel(t.Item1, t.Item2));
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        settings.SetSupplierVoiceModels(supplier, current);
                    }
                    // Notify user that sync completed (enqueue to show on main thread)
                    EnqueueMessage("Ustas.RimAI.Communication.Voices.SyncComplete".Translate(), MessageTypeDefOf.TaskCompletion);
                }
            });
        }
    }

    internal static void DrawModelConfigRow(Listing_Standard listing, VoiceModel model, int index, System.Collections.Generic.List<VoiceModel> models, float width)
    {
        Rect rowRect = listing.GetRect(30f);
        float x = rowRect.x;
        float y = rowRect.y;
        float height = rowRect.height;

        // Reorder buttons
        Rect upButtonRect = new Rect(x, y, 24f, height);
        if (Widgets.ButtonText(upButtonRect, "▲") && index > 0)
        {
            (models[index], models[index - 1]) = (models[index - 1], models[index]);
        }
        x += 30f;

        Rect downButtonRect = new Rect(x, y, 24f, height);
        if (Widgets.ButtonText(downButtonRect, "▼") && index < models.Count - 1)
        {
            (models[index], models[index + 1]) = (models[index + 1], models[index]);
        }
        x += 30f;

        float nameWidth = (width - 130f) * 0.4f;
        float idWidth = (width - 130f) * 0.4f;

        // Model Name field
        Rect nameRect = new Rect(x, y, nameWidth, height);
        model.ModelName = Widgets.TextField(nameRect, model.ModelName ?? "");
        x += nameWidth + 5f;

        // Model ID field
        Rect idRect = new Rect(x, y, idWidth, height);
        model.ModelId = Widgets.TextField(idRect, model.ModelId ?? "");

        // Delete button for this row
        Rect delRect = new Rect(idRect.xMax + 5f, y, 24f, height);
        if (Widgets.ButtonText(delRect, "X"))
        {
            // If looks like a SiliconFlow user voice (speech:...), attempt deletion
            string toDeleteId = model.ModelId ?? "";
            if (!string.IsNullOrWhiteSpace(toDeleteId) && toDeleteId.StartsWith("speech:"))
            {
                var apiKey = LoadedModManager.GetMod(typeof(TTSMod)) is TTSMod mod ? (mod.GetSettings<TTSSettings>()?.GetSupplierApiKey(mod.GetSettings<TTSSettings>().Supplier) ?? "") : "";
                var supplier = LoadedModManager.GetMod(typeof(TTSMod)) is TTSMod _m2 ? _m2.GetSettings<TTSSettings>().Supplier : TTSSettings.TTSSupplier.None;
                // Use background task to delete
                RimAiBackground.Run(async () =>
                {
                    bool ok = await Service.SiliconFlowClient.DeleteUserVoiceAsync(apiKey, toDeleteId);
                    if (ok)
                        EnqueueMessage("Ustas.RimAI.Communication.Voices.DeleteComplete".Translate(), MessageTypeDefOf.TaskCompletion);
                    else
                        EnqueueMessage("Ustas.RimAI.Communication.Voices.DeleteFailed".Translate(), MessageTypeDefOf.RejectInput);
                });
            }

            // Remove locally regardless (server deletion attempted above)
            if (models != null && index >= 0 && index < models.Count)
            {
                models.RemoveAt(index);
            }
        }
    }
    }
}
