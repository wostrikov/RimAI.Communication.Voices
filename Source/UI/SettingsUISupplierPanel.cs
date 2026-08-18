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

    internal static class SettingsUISupplierPanel
    {
        internal static void DrawSupplierRuntimeSection(Listing_Standard listing, TTSSettings settings, float viewWidth)
        {
        // Per-supplier API key and model configuration
        if (settings.Supplier != TTSSettings.TTSSupplier.None)
        {
            // OpenAI voicing takes its credential from the environment, not from a text field
            if (settings.Supplier == TTSSettings.TTSSupplier.OpenAI)
            {
                SettingsUIOpenAiPanel.DrawOpenAiCredential(listing);
                listing.Gap();
            }
            // EdgeTTS doesn't need API key - skip it
            else if (settings.Supplier != TTSSettings.TTSSupplier.EdgeTTS)
            {
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.ApiKey".Translate());
                string currentApiKey = settings.GetSupplierApiKey(settings.Supplier);
                string newApiKey = GUI.PasswordField(listing.GetRect(30f), currentApiKey ?? "", '•');
                if (newApiKey != currentApiKey)
                {
                    settings.SetSupplierApiKey(settings.Supplier, newApiKey);
                }

                listing.Gap();
            }

            // TTS Model Selection (example: FishAudio choices)
            if (settings.Supplier == TTSSettings.TTSSupplier.FishAudio)
            {
                string currentModel = settings.GetSupplierModel(settings.Supplier);
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.ModelLabel".Translate(currentModel));
                if (listing.RadioButton("Ustas.RimAI.Communication.Settings.TTS.ModelHighQuality".Translate(), currentModel == "fishaudio-1"))
                {
                    settings.SetSupplierModel(settings.Supplier, "fishaudio-1");
                }
                if (listing.RadioButton("Ustas.RimAI.Communication.Settings.TTS.ModelFaster".Translate(), currentModel == "s1"))
                {
                    settings.SetSupplierModel(settings.Supplier, "s1");
                }
            }

            // CosyVoice model selection
            if (settings.Supplier == TTSSettings.TTSSupplier.CosyVoice)
            {
                string currentModel = settings.GetSupplierModel(settings.Supplier);
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.ModelLabel.CosyVoice".Translate(currentModel ?? "(not set)"));
                if (listing.RadioButton("FunAudioLLM/CosyVoice2-0.5B", currentModel == "FunAudioLLM/CosyVoice2-0.5B"))
                {
                    settings.SetSupplierModel(settings.Supplier, "FunAudioLLM/CosyVoice2-0.5B");
                }
                listing.Gap(6f);
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.CustomModelIdLabel".Translate());
                string customModelCosy = listing.TextEntry(currentModel ?? "");
                if (customModelCosy != currentModel)
                {
                    settings.SetSupplierModel(settings.Supplier, customModelCosy);
                }
            }

            // IndexTTS model selection
            if (settings.Supplier == TTSSettings.TTSSupplier.IndexTTS)
            {
                string currentModel = settings.GetSupplierModel(settings.Supplier);
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.ModelLabel.IndexTTS".Translate(currentModel ?? "(not set)"));
                if (listing.RadioButton("IndexTeam/IndexTTS-2", currentModel == "IndexTeam/IndexTTS-2"))
                {
                    settings.SetSupplierModel(settings.Supplier, "IndexTeam/IndexTTS-2");
                }
                listing.Gap(6f);
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.CustomModelIdLabel".Translate());
                string customModelIndex = listing.TextEntry(currentModel ?? "");
                if (customModelIndex != currentModel)
                {
                    settings.SetSupplierModel(settings.Supplier, customModelIndex);
                }
            }

            // AzureTTS region configuration
            if (settings.Supplier == TTSSettings.TTSSupplier.AzureTTS)
            {
                string currentRegion = settings.GetSupplierRegion(settings.Supplier);
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.AzureRegionLabel".Translate(currentRegion ?? "eastus"));
                listing.Gap(6f);
                
                // Common Azure regions for TTS
                var regionOptions = new[] { "eastus", "westus", "westus2", "eastus2", "westeurope", "northeurope", 
                                           "southeastasia", "eastasia", "australiaeast", "japaneast", "canadacentral" };
                
                Rect regionRect = listing.GetRect(30f);
                string regionDisplay = currentRegion ?? "eastus";
                if (Widgets.ButtonText(regionRect, regionDisplay))
                {
                    var options = new System.Collections.Generic.List<FloatMenuOption>();
                    foreach (var region in regionOptions)
                    {
                        options.Add(new FloatMenuOption(region, delegate
                        {
                            settings.SetSupplierRegion(settings.Supplier, region);
                            // Update provider with new region
                            TTSService.SetProvider(settings.Supplier, settings);
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                
                listing.Gap(6f);
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.CustomRegionLabel".Translate());
                string customRegion = listing.TextEntry(currentRegion ?? "eastus");
                if (customRegion != currentRegion)
                {
                    settings.SetSupplierRegion(settings.Supplier, customRegion);
                    // Update provider with new region
                    TTSService.SetProvider(settings.Supplier, settings);
                }
            }

            if (settings.Supplier == TTSSettings.TTSSupplier.OpenAI)
            {
                SettingsUIOpenAiPanel.DrawOpenAiSection(listing, settings);
            }

            // TTSWebUI base URL configuration
            if (settings.Supplier == TTSSettings.TTSSupplier.TTSWebUI)
            {
                string currentBaseUrl = settings.GetSupplierRegion(settings.Supplier);
                if (string.IsNullOrWhiteSpace(currentBaseUrl))
                {
                    currentBaseUrl = "http://localhost:7778/v1";
                }
                
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.TTSWebUIBaseUrlLabel".Translate(currentBaseUrl));
                listing.Gap(6f);
                
                // Common TTSWebUI base URLs
                var urlOptions = new[] { 
                    "http://localhost:7778/v1",  // Default OpenAI API endpoint
                    "http://localhost:7770/v1",  // Gradio default port
                    "http://127.0.0.1:7778/v1"
                };
                
                Rect urlRect = listing.GetRect(30f);
                if (Widgets.ButtonText(urlRect, currentBaseUrl))
                {
                    var options = new System.Collections.Generic.List<FloatMenuOption>();
                    foreach (var url in urlOptions)
                    {
                        options.Add(new FloatMenuOption(url, delegate
                        {
                            settings.SetSupplierRegion(settings.Supplier, url);
                            TTSService.SetProvider(settings.Supplier, settings);
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                
                listing.Gap(6f);
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.TTSWebUICustomUrlLabel".Translate());
                string customUrl = listing.TextEntry(currentBaseUrl);
                if (customUrl != currentBaseUrl)
                {
                    settings.SetSupplierRegion(settings.Supplier, customUrl);
                    TTSService.SetProvider(settings.Supplier, settings);
                }
                
                listing.Gap(6f);
                
                // Model selection for TTSWebUI (user can specify model name)
                string currentModel = settings.GetSupplierModel(settings.Supplier);
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.TTSWebUIModelLabel".Translate(currentModel ?? "(default)"));
                listing.Gap(6f);
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.TTSWebUIModelHint".Translate());
                string customModel = listing.TextEntry(currentModel ?? "");
                if (customModel != currentModel)
                {
                    settings.SetSupplierModel(settings.Supplier, customModel);
                }
            }

            listing.Gap();
            
            int currentCooldown = settings.GetSupplierGenerateCooldown(settings.Supplier);
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.GenerateCooldownMiliSecondsLabel".Translate(currentCooldown.ToString()));
            int newCooldown = (int)listing.Slider(currentCooldown, 0, 20000);
            if (newCooldown != currentCooldown)
                settings.SetSupplierGenerateCooldown(settings.Supplier, newCooldown);

            listing.Gap();

            float currentVolume = settings.GetSupplierVolume(settings.Supplier);
            listing.Label("Ustas.RimAI.Communication.Settings.TTS.VolumeLabel".Translate(currentVolume.ToStringPercent()));
            float newVolume = listing.Slider(currentVolume, 0f, 1f);
            if (newVolume != currentVolume)
                settings.SetSupplierVolume(settings.Supplier, newVolume);

            listing.Gap();

            // Sampling knobs only exist on the Fish Audio style backends
            if (TTSSettings.SupportsSampling(settings.Supplier))
            {
                float currentTemp = settings.GetSupplierTemperature(settings.Supplier);
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.TemperatureLabel".Translate(currentTemp.ToString("F2")));
                float newTemp = listing.Slider(currentTemp, 0.7f, 1.0f);
                if (newTemp != currentTemp)
                    settings.SetSupplierTemperature(settings.Supplier, newTemp);

                // Top P
                float currentTopP = settings.GetSupplierTopP(settings.Supplier);
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.TopPLabel".Translate(currentTopP.ToString("F2")));
                float newTopP = listing.Slider(currentTopP, 0.7f, 1.0f);
                if (newTopP != currentTopP)
                    settings.SetSupplierTopP(settings.Supplier, newTopP);

                listing.Gap();
            }

            bool automaticActive = SettingsUI.IsAutomaticActive(settings);

            // In automatic mode pace comes from each pawn's identity, so a global
            // speed slider would silently fight the generator.
            if (!automaticActive)
            {
                float currentSpeed = settings.GetSupplierSpeed(settings.Supplier);
                listing.Label("Ustas.RimAI.Communication.Settings.TTS.SpeedLabel".Translate(currentSpeed.ToString("F2")));
                float newSpeed = listing.Slider(currentSpeed, 0.25f, 4.0f);
                if (newSpeed != currentSpeed)
                    settings.SetSupplierSpeed(settings.Supplier, newSpeed);

                listing.Gap();
            }

            // Voice Models Section (per-supplier when a supplier is selected).
            System.Collections.Generic.List<VoiceModel> currentVoiceModels = settings.GetSupplierVoiceModels(settings.Supplier);

            if (automaticActive)
            {
                if (listing.ButtonText(showManualVoiceSection
                        ? "Ustas.RimAI.Communication.Settings.TTS.Manual.Hide".Translate()
                        : "Ustas.RimAI.Communication.Settings.TTS.Manual.Show".Translate()))
                {
                    showManualVoiceSection = !showManualVoiceSection;
                }

                if (showManualVoiceSection)
                {
                    listing.Gap(6f);
                    SettingsUIVoiceModelsPanel.DrawVoiceModelsSection(listing, settings, viewWidth, currentVoiceModels);
                }
            }
            else
            {
                SettingsUIVoiceModelsPanel.DrawVoiceModelsSection(listing, settings, viewWidth, currentVoiceModels);
            }
        }
        }
    }
}
