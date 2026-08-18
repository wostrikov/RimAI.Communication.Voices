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

    internal static class SettingsUIVoiceRulesPanel
    {
    internal static void DrawSimpleDefaultVoiceSelector(Listing_Standard listing, TTSSettings settings, System.Collections.Generic.List<VoiceModel> voiceModels)
    {
        // Default model selector (shows names from current voice model list)
        string defaultModelId = settings.GetSupplierDefaultVoiceModelId(settings.Supplier);

        string currentDefaultName = "Ustas.RimAI.Communication.Settings.TTS.NotSet".Translate();
        if (!string.IsNullOrEmpty(defaultModelId))
        {
            if (defaultModelId == VoiceModel.NONE_MODEL_ID)
            {
                currentDefaultName = "Ustas.RimAI.Communication.Settings.TTS.NoneModel".Translate();
            }
            else if (defaultModelId == VoiceModel.RULE_BASED_MODEL_ID)
            {
                currentDefaultName = "Ustas.RimAI.Communication.Settings.TTS.RuleBased".Translate();
            }
            else if (voiceModels != null)
            {
                var m = voiceModels.FirstOrDefault(x => x.ModelId == defaultModelId);
                if (m != null)
                    currentDefaultName = m.GetDisplayName();
            }
        }

        if (listing.ButtonText("Ustas.RimAI.Communication.Settings.TTS.DefaultModel".Translate(currentDefaultName)))
        {
            var options = new System.Collections.Generic.List<FloatMenuOption>();
            options.Add(new FloatMenuOption("Ustas.RimAI.Communication.Settings.TTS.ClearDefault".Translate(), delegate
            {
                settings.SetSupplierDefaultVoiceModelId(settings.Supplier, null);
            }));

            // Add NONE pseudo-model option
            options.Add(new FloatMenuOption("Ustas.RimAI.Communication.Settings.TTS.NoneModel".Translate(), delegate
            {
                settings.SetSupplierDefaultVoiceModelId(settings.Supplier, VoiceModel.NONE_MODEL_ID);
            }));

            // Add RULE_BASED option
            options.Add(new FloatMenuOption("Ustas.RimAI.Communication.Settings.TTS.RuleBased".Translate(), delegate
            {
                settings.SetSupplierDefaultVoiceModelId(settings.Supplier, VoiceModel.RULE_BASED_MODEL_ID);
            }));

            if (voiceModels != null)
            {
                foreach (var vm in voiceModels)
                {
                    var display = vm.GetDisplayName();
                    options.Add(new FloatMenuOption(display, delegate
                    {
                        settings.SetSupplierDefaultVoiceModelId(settings.Supplier, vm.ModelId);
                    }));
                }
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    internal static void DrawVoiceRulesList(Listing_Standard listing, TTSSettings settings, float width, System.Collections.Generic.List<VoiceModel> voiceModels)
    {
        var rules = settings.GetSupplierVoiceRules(settings.Supplier);
        
        // Rules list title
        listing.Label("Ustas.RimAI.Communication.Settings.TTS.AdvancedMode.RulesList".Translate());
        
        // Container box for rules
        float ruleListHeight = Mathf.Max(200f, rules.Count * 35f + 10f);
        Rect ruleListOuterRect = listing.GetRect(ruleListHeight);
        
        Widgets.DrawBoxSolid(ruleListOuterRect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
        Widgets.DrawBox(ruleListOuterRect);
        
        Rect ruleListInnerRect = ruleListOuterRect.ContractedBy(5f);
        Rect ruleListViewRect = new Rect(0f, 0f, ruleListInnerRect.width - 20f, rules.Count * 35f);
        
        Vector2 ruleScrollPos = Vector2.zero;
        Widgets.BeginScrollView(ruleListInnerRect, ref ruleScrollPos, ruleListViewRect);
        
        float y = 0f;
        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            Rect ruleRect = new Rect(0f, y, ruleListViewRect.width, 30f);
            
            // Highlight selected rule
            if (i == selectedRuleIndex)
            {
                Widgets.DrawHighlight(ruleRect);
            }
            
            // Rule display text (truncated if needed)
            string displayText = rule.GetDisplayString(ruleRect.width - 10f);
            
            // Double click detection: check if same item clicked within time window
            if (Widgets.ButtonInvisible(ruleRect))
            {
                float currentTime = Time.realtimeSinceStartup;
                bool isDoubleClick = (i == lastClickedRuleIndex) && 
                                    (currentTime - lastClickTime < DOUBLE_CLICK_TIME);
                
                if (isDoubleClick)
                {
                    // Double click - open editor
                    Find.WindowStack.Add(new VoiceRuleEditorWindow(rule, settings, () =>
                    {
                        settings.SetSupplierVoiceRules(settings.Supplier, rules);
                    }));
                    lastClickedRuleIndex = -1; // Reset to prevent triple-click
                }
                else
                {
                    // Single click - select and record click
                    selectedRuleIndex = i;
                    lastClickedRuleIndex = i;
                    lastClickTime = currentTime;
                }
            }
            
            Rect labelRect = new Rect(ruleRect.x + 5f, ruleRect.y, ruleRect.width - 10f, ruleRect.height);
            Widgets.Label(labelRect, displayText);
            
            y += 35f;
        }
        
        Widgets.EndScrollView();
        
        listing.Gap(6f);
        
        // Control buttons: ↑ ↓ + ×
        Rect buttonRowRect = listing.GetRect(30f);
        float buttonWidth = 40f;
        float buttonGap = 5f;
        
        Rect upButtonRect = new Rect(buttonRowRect.x, buttonRowRect.y, buttonWidth, 30f);
        if (Widgets.ButtonText(upButtonRect, "↑"))
        {
            if (selectedRuleIndex > 0)
            {
                var temp = rules[selectedRuleIndex];
                rules[selectedRuleIndex] = rules[selectedRuleIndex - 1];
                rules[selectedRuleIndex - 1] = temp;
                selectedRuleIndex--;
                settings.SetSupplierVoiceRules(settings.Supplier, rules);
            }
        }
        
        Rect downButtonRect = new Rect(upButtonRect.xMax + buttonGap, buttonRowRect.y, buttonWidth, 30f);
        if (Widgets.ButtonText(downButtonRect, "↓"))
        {
            if (selectedRuleIndex >= 0 && selectedRuleIndex < rules.Count - 1)
            {
                var temp = rules[selectedRuleIndex];
                rules[selectedRuleIndex] = rules[selectedRuleIndex + 1];
                rules[selectedRuleIndex + 1] = temp;
                selectedRuleIndex++;
                settings.SetSupplierVoiceRules(settings.Supplier, rules);
            }
        }
        
        Rect addButtonRect = new Rect(downButtonRect.xMax + buttonGap, buttonRowRect.y, buttonWidth, 30f);
        if (Widgets.ButtonText(addButtonRect, "+"))
        {
            var newRule = new VoiceAssignmentRule();
            Find.WindowStack.Add(new VoiceRuleEditorWindow(newRule, settings, () =>
            {
                rules.Add(newRule);
                settings.SetSupplierVoiceRules(settings.Supplier, rules);
                selectedRuleIndex = rules.Count - 1;
            }));
        }
        
        Rect deleteButtonRect = new Rect(addButtonRect.xMax + buttonGap, buttonRowRect.y, buttonWidth, 30f);
        if (Widgets.ButtonText(deleteButtonRect, "×"))
        {
            if (selectedRuleIndex >= 0 && selectedRuleIndex < rules.Count)
            {
                rules.RemoveAt(selectedRuleIndex);
                settings.SetSupplierVoiceRules(settings.Supplier, rules);
                selectedRuleIndex = -1;
            }
        }

        listing.Gap();
    }

    internal static void DrawPlayerVoiceSelector(Listing_Standard listing, TTSSettings settings)
    {
        // Player reference voice selection (single-line dropdown using supplier voice models)
        listing.Gap(6f);
        listing.Label("Ustas.RimAI.Communication.Settings.TTS.PlayerVoiceModel".Translate());
        Rect playerRect = listing.GetRect(Text.LineHeight);

        string currentPlayerSelectionName;
        var playerModelId = settings.PlayerReferenceVoiceModelId;
        if (playerModelId == VoiceModel.NONE_MODEL_ID)
        {
            currentPlayerSelectionName = "Ustas.RimAI.Communication.Settings.TTS.NoneModel".Translate();
        }
        else
        {
            var vm = settings.GetSupplierVoiceModels(settings.Supplier)?.FirstOrDefault(x => x.ModelId == playerModelId);
            currentPlayerSelectionName = vm?.GetDisplayName() ?? playerModelId;
        }

        if (Widgets.ButtonText(playerRect, currentPlayerSelectionName))
        {
            var options = new System.Collections.Generic.List<FloatMenuOption>();

            // None
            options.Add(new FloatMenuOption("Ustas.RimAI.Communication.Settings.TTS.NoneModel".Translate(), delegate
            {
                settings.PlayerReferenceVoiceModelId = VoiceModel.NONE_MODEL_ID;
                RimTalkPatches.UpdatePlayerPawnVoice();
            }));

            var list = settings.GetSupplierVoiceModels(settings.Supplier);
            if (list != null)
            {
                foreach (var vm in list)
                {
                    var display = vm.GetDisplayName();
                    var id = vm.ModelId ?? "";
                    options.Add(new FloatMenuOption(display, delegate
                    {
                        settings.PlayerReferenceVoiceModelId = id;
                        RimTalkPatches.UpdatePlayerPawnVoice();
                    }));
                }
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        listing.Gap();
    }
    }
}
