using System;
using System.Collections.Generic;
using RimWorld;
using Ustas.RimAI.Core.Voices;
using Verse;

namespace Ustas.RimAI.Communication.Voices.Voice
{
    /// <summary>
    /// Translates a live pawn into the engine-free <see cref="VoiceFeatureSet"/> the
    /// Core generator understands.
    ///
    /// Everything here is semantic on purpose: there is no xenotype table, no race
    /// table and no trait-to-voice table. Unknown traits, unknown genes and unknown
    /// modded xenotypes simply contribute nothing and still produce a usable voice.
    /// </summary>
    public static class PawnVoiceFeatureExtractor
    {
        /// <summary>Stable per-pawn key that survives save/load and map changes.</summary>
        public static string StableKeyFor(Pawn pawn)
        {
            if (pawn == null) return string.Empty;

            try
            {
                if (!string.IsNullOrEmpty(pawn.ThingID))
                    return pawn.ThingID;
            }
            catch (Exception)
            {
                // Fall through to the numeric id below.
            }

            return "pawn:" + pawn.thingIDNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static VoiceFeatureSet Extract(Pawn pawn)
        {
            var features = new VoiceFeatureSet
            {
                StableKey = StableKeyFor(pawn),
                SexClass = SexClassOf(pawn),
                BiologicalAge = BiologicalAgeOf(pawn),
                IsHumanlike = IsHumanlike(pawn),
                BodySize = BodySizeOf(pawn),
                XenotypeKey = XenotypeKeyOf(pawn),
                IsNonBaseline = IsNonBaseline(pawn),
                CultureKey = CultureKeyOf(pawn)
            };

            ApplyBodyType(pawn, features);
            ApplyTraits(pawn, features);
            ApplyGenes(pawn, features);
            ApplyBackstory(pawn, features);

            Clamp(features);
            return features;
        }

        static VoiceSexClass SexClassOf(Pawn pawn)
        {
            try
            {
                switch (pawn?.gender)
                {
                    case Gender.Male: return VoiceSexClass.Masculine;
                    case Gender.Female: return VoiceSexClass.Feminine;
                    default: return VoiceSexClass.Unspecified;
                }
            }
            catch (Exception)
            {
                return VoiceSexClass.Unspecified;
            }
        }

        static int BiologicalAgeOf(Pawn pawn)
        {
            try
            {
                int age = pawn?.ageTracker?.AgeBiologicalYears ?? 25;
                return age <= 0 ? 25 : age;
            }
            catch (Exception)
            {
                return 25;
            }
        }

        static bool IsHumanlike(Pawn pawn)
        {
            try
            {
                return pawn?.RaceProps?.Humanlike ?? false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static float BodySizeOf(Pawn pawn)
        {
            try
            {
                float size = pawn?.BodySize ?? 1f;
                if (size <= 0f) return 1f;
                return size > 3f ? 3f : size;
            }
            catch (Exception)
            {
                return 1f;
            }
        }

        static string XenotypeKeyOf(Pawn pawn)
        {
            try
            {
                var xenotype = pawn?.genes?.Xenotype;
                if (xenotype != null && !string.IsNullOrEmpty(xenotype.defName))
                    return xenotype.defName;

                return pawn?.def?.defName ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        static bool IsNonBaseline(Pawn pawn)
        {
            try
            {
                var genes = pawn?.genes;
                if (genes == null) return false;
                if (genes.UniqueXenotype) return true;

                string defName = genes.Xenotype?.defName;
                return !string.IsNullOrEmpty(defName)
                       && !string.Equals(defName, "Baseliner", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        static string CultureKeyOf(Pawn pawn)
        {
            try
            {
                string faction = pawn?.Faction?.def?.defName ?? string.Empty;
                string ideo = pawn?.Ideo?.name ?? string.Empty;
                return faction + "/" + ideo;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        static void ApplyBodyType(Pawn pawn, VoiceFeatureSet features)
        {
            try
            {
                string bodyType = pawn?.story?.bodyType?.defName;
                if (string.IsNullOrEmpty(bodyType))
                    return;

                switch (bodyType)
                {
                    case "Hulk":
                        features.BodySize += 0.25f;
                        features.Toughness += 0.25f;
                        break;
                    case "Fat":
                        features.BodySize += 0.15f;
                        break;
                    case "Thin":
                        features.BodySize -= 0.15f;
                        break;
                }
            }
            catch (Exception)
            {
                // Body type is optional colour, never a failure.
            }
        }

        /// <summary>
        /// Traits nudge continuous dimensions. A trait never selects a voice, and a
        /// trait this table does not know about simply has no effect.
        /// </summary>
        static void ApplyTraits(Pawn pawn, VoiceFeatureSet features)
        {
            List<Trait> traits;
            try
            {
                traits = pawn?.story?.traits?.allTraits;
            }
            catch (Exception)
            {
                return;
            }

            if (traits == null)
                return;

            foreach (var trait in traits)
            {
                string defName = trait?.def?.defName;
                if (string.IsNullOrEmpty(defName))
                    continue;

                int degree = 0;
                try
                {
                    degree = trait.Degree;
                }
                catch (Exception)
                {
                    // Degree is optional for many traits.
                }

                switch (defName)
                {
                    case "Kind":
                        features.Kindness += 0.5f;
                        features.Sociability += 0.2f;
                        break;
                    case "Abrasive":
                        features.Kindness -= 0.45f;
                        features.Toughness += 0.2f;
                        break;
                    case "Psychopath":
                        features.Kindness -= 0.4f;
                        features.Theatricality -= 0.4f;
                        break;
                    case "Bloodlust":
                        features.Aggression += 0.45f;
                        break;
                    case "Brawler":
                        features.Aggression += 0.3f;
                        features.Toughness += 0.3f;
                        break;
                    case "Cannibal":
                        features.Kindness -= 0.2f;
                        break;
                    case "TooSmart":
                        features.Intellect += 0.5f;
                        break;
                    case "GreatMemory":
                        features.Intellect += 0.2f;
                        break;
                    case "SlowLearner":
                        features.Intellect -= 0.3f;
                        break;
                    case "Nerves":
                        // Negative degrees are volatile nerves, positive are iron-willed.
                        features.Nervousness += degree < 0 ? 0.4f : -0.25f;
                        break;
                    case "Neurotic":
                        features.Nervousness += degree >= 2 ? 0.5f : 0.3f;
                        break;
                    case "NaturalMood":
                        features.Sociability += degree > 0 ? 0.25f : -0.2f;
                        features.Theatricality += degree > 0 ? 0.25f : -0.25f;
                        break;
                    case "Industriousness":
                        features.Nervousness += degree > 0 ? 0.1f : -0.1f;
                        break;
                    case "Beauty":
                        features.Sociability += degree * 0.1f;
                        break;
                    case "AnnoyingVoice":
                        features.Toughness += 0.35f;
                        features.Intellect -= 0.2f;
                        break;
                    case "CreepyBreathing":
                        features.Otherworldliness += 0.3f;
                        break;
                    case "PsychicSensitivity":
                        features.Otherworldliness += degree * 0.25f;
                        break;
                    case "Nimble":
                        features.Nervousness += 0.1f;
                        break;
                    case "Tough":
                        features.Toughness += 0.35f;
                        break;
                    case "Wimp":
                        features.Toughness -= 0.3f;
                        break;
                    case "Gourmand":
                        features.BodySize += 0.05f;
                        break;
                    case "Transhumanist":
                        features.Otherworldliness += 0.15f;
                        break;
                }
            }
        }

        /// <summary>
        /// Genes are read semantically by what their names imply. This keeps unknown
        /// modded genes and xenotypes working without a per-mod table, and it never
        /// derives a real-world ethnicity from a xenotype.
        /// </summary>
        static void ApplyGenes(Pawn pawn, VoiceFeatureSet features)
        {
            List<Gene> genes;
            try
            {
                genes = pawn?.genes?.GenesListForReading;
            }
            catch (Exception)
            {
                return;
            }

            if (genes == null)
                return;

            foreach (var gene in genes)
            {
                string defName = gene?.def?.defName;
                if (string.IsNullOrEmpty(defName))
                    continue;

                string lowered = defName.ToLowerInvariant();

                if (lowered.Contains("aggress") || lowered.Contains("berserk"))
                    features.Aggression += 0.2f;
                if (lowered.Contains("robust") || lowered.Contains("tough") || lowered.Contains("armor"))
                    features.Toughness += 0.2f;
                if (lowered.Contains("frail") || lowered.Contains("delicate"))
                    features.Toughness -= 0.2f;
                if (lowered.Contains("psychic") || lowered.Contains("psylink"))
                    features.Otherworldliness += 0.2f;
                if (lowered.Contains("voice") || lowered.Contains("vocal"))
                    features.Theatricality += 0.2f;
                if (lowered.Contains("beauty") || lowered.Contains("pretty"))
                    features.Sociability += 0.1f;
                if (lowered.Contains("intelligence") || lowered.Contains("learning"))
                    features.Intellect += 0.15f;
                if (lowered.Contains("body_") || lowered.Contains("bodysize"))
                {
                    if (lowered.Contains("large") || lowered.Contains("hulk"))
                        features.BodySize += 0.2f;
                    if (lowered.Contains("small") || lowered.Contains("thin"))
                        features.BodySize -= 0.2f;
                }
            }
        }

        static void ApplyBackstory(Pawn pawn, VoiceFeatureSet features)
        {
            try
            {
                string childhood = pawn?.story?.Childhood?.identifier ?? string.Empty;
                string adulthood = pawn?.story?.Adulthood?.identifier ?? string.Empty;
                string combined = (childhood + " " + adulthood).ToLowerInvariant();

                if (combined.Length == 0)
                    return;

                if (combined.Contains("noble") || combined.Contains("aristocrat") || combined.Contains("diplomat"))
                {
                    features.Intellect += 0.2f;
                    features.Sociability += 0.15f;
                }

                if (combined.Contains("soldier") || combined.Contains("mercenary") || combined.Contains("pirate"))
                    features.Toughness += 0.2f;

                if (combined.Contains("scientist") || combined.Contains("researcher") || combined.Contains("scholar"))
                    features.Intellect += 0.2f;

                if (combined.Contains("preacher") || combined.Contains("actor") || combined.Contains("singer"))
                    features.Theatricality += 0.25f;

                if (combined.Contains("hermit") || combined.Contains("recluse"))
                    features.Sociability -= 0.25f;
            }
            catch (Exception)
            {
                // Backstory is the softest signal; losing it changes nothing important.
            }
        }

        static void Clamp(VoiceFeatureSet features)
        {
            features.Aggression = VoiceMath.ClampSigned(features.Aggression);
            features.Sociability = VoiceMath.ClampSigned(features.Sociability);
            features.Nervousness = VoiceMath.ClampUnit(features.Nervousness);
            features.Intellect = VoiceMath.ClampSigned(features.Intellect);
            features.Kindness = VoiceMath.ClampSigned(features.Kindness);
            features.Toughness = VoiceMath.ClampUnit(features.Toughness);
            features.Theatricality = VoiceMath.ClampSigned(features.Theatricality);
            features.Otherworldliness = VoiceMath.ClampUnit(features.Otherworldliness);
            features.BodySize = VoiceMath.Clamp(features.BodySize, 0.4f, 3f);
        }
    }
}
