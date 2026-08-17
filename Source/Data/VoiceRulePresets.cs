using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Ustas.RimAI.Communication.Voices.Data
{
    /// <summary>
    /// Builds a ready-to-use set of voice assignment rules from the voices a supplier
    /// currently has configured, so a colony of twenty pawns does not share two voices.
    /// Each generated rule keeps several voices, and the assignment engine picks one at
    /// random per pawn and caches it.
    /// </summary>
    public static class VoiceRulePresets
    {
        const int YoungMaxAge = 17;
        const int AdultMinAge = 18;
        const int AdultMaxAge = 59;
        const int SeniorMinAge = 60;
        const int OpenEndedAge = 999999;

        public static List<VoiceAssignmentRule> Generate(TTSSettings.TTSSupplier supplier, List<VoiceModel> voiceModels)
        {
            var rules = new List<VoiceAssignmentRule>();
            var usable = (voiceModels ?? new List<VoiceModel>())
                .Where(m => m != null && m.IsValid())
                .ToList();

            if (usable.Count == 0)
                return rules;

            var byGender = usable
                .GroupBy(m => VoicePersonaCatalog.ResolveGender(supplier, m))
                .ToDictionary(g => g.Key, g => g.ToList());

            byGender.TryGetValue(Gender.Male, out var maleVoices);
            byGender.TryGetValue(Gender.Female, out var femaleVoices);

            AddGenderRules(rules, supplier, Gender.Male, maleVoices);
            AddGenderRules(rules, supplier, Gender.Female, femaleVoices);

            // Last rule matches everyone the gendered rules did not cover.
            rules.Add(BuildRule(usable, null, 0, OpenEndedAge));

            return rules;
        }

        static void AddGenderRules(
            List<VoiceAssignmentRule> rules,
            TTSSettings.TTSSupplier supplier,
            Gender gender,
            List<VoiceModel> voices)
        {
            if (voices == null || voices.Count == 0)
                return;

            var young = Pick(supplier, voices, VoiceAgeBand.Young, VoiceAgeBand.Adult);
            var adult = Pick(supplier, voices, VoiceAgeBand.Adult, VoiceAgeBand.Young);
            var senior = Pick(supplier, voices, VoiceAgeBand.Mature, VoiceAgeBand.Adult);

            rules.Add(BuildRule(young, gender, 0, YoungMaxAge));
            rules.Add(BuildRule(adult, gender, AdultMinAge, AdultMaxAge));
            rules.Add(BuildRule(senior, gender, SeniorMinAge, OpenEndedAge));
        }

        /// <summary>
        /// Take voices from the preferred age band, widening to the fallback band and then
        /// to the whole gender bucket rather than producing an empty rule.
        /// </summary>
        static List<VoiceModel> Pick(
            TTSSettings.TTSSupplier supplier,
            List<VoiceModel> voices,
            VoiceAgeBand preferred,
            VoiceAgeBand fallback)
        {
            var picked = voices
                .Where(m => VoicePersonaCatalog.ResolveAgeBand(supplier, m) == preferred)
                .ToList();

            if (picked.Count == 0)
            {
                picked = voices
                    .Where(m => VoicePersonaCatalog.ResolveAgeBand(supplier, m) == fallback)
                    .ToList();
            }

            return picked.Count == 0 ? voices : picked;
        }

        static VoiceAssignmentRule BuildRule(List<VoiceModel> voices, Gender? gender, int minAge, int maxAge)
        {
            var rule = new VoiceAssignmentRule();

            if (gender.HasValue)
                rule.Requirements.Add(new GenderRequirement(gender.Value));

            bool coversEveryAge = minAge == 0 && maxAge == OpenEndedAge;
            if (!coversEveryAge)
                rule.Requirements.Add(new AgeRequirement(minAge, maxAge));

            rule.VoiceModelIds = voices.Select(m => m.ModelId).Distinct().ToList();
            return rule;
        }
    }
}
