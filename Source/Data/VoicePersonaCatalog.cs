using System.Collections.Generic;
using Verse;

namespace Ustas.RimAI.Communication.Voices.Data
{
    /// <summary>
    /// Perceived age band of a voice. Used only to spread voices across pawn age groups
    /// when generating assignment rules; providers do not expose this themselves.
    /// </summary>
    public enum VoiceAgeBand
    {
        Young,
        Adult,
        Mature
    }

    public sealed class VoicePersona
    {
        public VoicePersona(string voiceId, string displayName, Gender gender, VoiceAgeBand ageBand)
        {
            VoiceId = voiceId;
            DisplayName = displayName;
            Gender = gender;
            AgeBand = ageBand;
        }

        public string VoiceId { get; }
        public string DisplayName { get; }
        public Gender Gender { get; }
        public VoiceAgeBand AgeBand { get; }
    }

    /// <summary>
    /// Voice character metadata used to build default assignment rules.
    /// Gender and age band are perceptual descriptions, not provider guarantees.
    /// </summary>
    public static class VoicePersonaCatalog
    {
        public static readonly IReadOnlyList<VoicePersona> OpenAI = new List<VoicePersona>
        {
            new VoicePersona("alloy", "Alloy (neutral, balanced)", Gender.Male, VoiceAgeBand.Adult),
            new VoicePersona("ash", "Ash (male, firm)", Gender.Male, VoiceAgeBand.Mature),
            new VoicePersona("ballad", "Ballad (male, expressive)", Gender.Male, VoiceAgeBand.Adult),
            new VoicePersona("cedar", "Cedar (male, deep)", Gender.Male, VoiceAgeBand.Mature),
            new VoicePersona("echo", "Echo (male, calm)", Gender.Male, VoiceAgeBand.Adult),
            new VoicePersona("fable", "Fable (male, storyteller)", Gender.Male, VoiceAgeBand.Young),
            new VoicePersona("onyx", "Onyx (male, low)", Gender.Male, VoiceAgeBand.Mature),
            new VoicePersona("verse", "Verse (male, youthful)", Gender.Male, VoiceAgeBand.Young),
            new VoicePersona("coral", "Coral (female, warm)", Gender.Female, VoiceAgeBand.Adult),
            new VoicePersona("marin", "Marin (female, clear)", Gender.Female, VoiceAgeBand.Adult),
            new VoicePersona("nova", "Nova (female, bright)", Gender.Female, VoiceAgeBand.Young),
            new VoicePersona("sage", "Sage (female, measured)", Gender.Female, VoiceAgeBand.Mature),
            new VoicePersona("shimmer", "Shimmer (female, light)", Gender.Female, VoiceAgeBand.Young)
        };

        public static VoicePersona Find(TTSSettings.TTSSupplier supplier, string voiceId)
        {
            if (string.IsNullOrWhiteSpace(voiceId))
                return null;
            if (supplier != TTSSettings.TTSSupplier.OpenAI)
                return null;

            foreach (var persona in OpenAI)
            {
                if (persona.VoiceId == voiceId)
                    return persona;
            }

            return null;
        }

        /// <summary>
        /// Best-effort gender for a configured voice model. Falls back to reading the
        /// gender marker that provider presets embed in the display name.
        /// </summary>
        public static Gender ResolveGender(TTSSettings.TTSSupplier supplier, VoiceModel model)
        {
            if (model == null)
                return Gender.None;

            var persona = Find(supplier, model.ModelId);
            if (persona != null)
                return persona.Gender;

            return GenderFromLabel(model.GetDisplayName());
        }

        public static VoiceAgeBand ResolveAgeBand(TTSSettings.TTSSupplier supplier, VoiceModel model)
        {
            var persona = model == null ? null : Find(supplier, model.ModelId);
            return persona?.AgeBand ?? VoiceAgeBand.Adult;
        }

        static Gender GenderFromLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return Gender.None;

            // Provider presets label voices as "Jenny (US, Female)"; the bundled Chinese
            // library uses 女 / 男 instead.
            if (label.IndexOf("female", System.StringComparison.OrdinalIgnoreCase) >= 0
                || label.Contains("女"))
            {
                return Gender.Female;
            }

            if (label.IndexOf("male", System.StringComparison.OrdinalIgnoreCase) >= 0
                || label.Contains("男"))
            {
                return Gender.Male;
            }

            return Gender.None;
        }
    }
}
