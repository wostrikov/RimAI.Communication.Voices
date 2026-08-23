using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Voices.Policy
{
    public enum PawnVoiceBindingKind
    {
        Default,
        RuleBased,
        Silent,
        Explicit
    }

    public readonly struct PawnVoiceDialogueDecision
    {
        public PawnVoiceDialogueDecision(bool silent, bool useAutomatic, string explicitVoiceId)
        {
            Silent = silent;
            UseAutomatic = useAutomatic;
            ExplicitVoiceId = explicitVoiceId ?? string.Empty;
        }

        public bool Silent { get; }
        public bool UseAutomatic { get; }
        public string ExplicitVoiceId { get; }
    }

    /// <summary>
    /// Authoritative per-pawn voice assignment and dialogue resolve.
    /// Different pawn ids stay isolated. NONE is silent. DEFAULT/RULE_BASED
    /// are not a manual override.
    /// </summary>
    public static class PawnVoiceBindingPolicy
    {
        public const string NoneModelId = "NONE";
        public const string RuleBasedModelId = "RULE_BASED";
        public const string DefaultModelId = "DEFAULT";

        public static string NormalizeAssignment(string voiceModelId) =>
            string.IsNullOrEmpty(voiceModelId) ? DefaultModelId : voiceModelId;

        public static string RawOrDefault(IDictionary<int, string> map, int pawnId)
        {
            if (map != null
                && map.TryGetValue(pawnId, out string voiceId)
                && !string.IsNullOrEmpty(voiceId))
                return voiceId;
            return DefaultModelId;
        }

        public static void Assign(IDictionary<int, string> map, int pawnId, string voiceModelId)
        {
            if (map == null)
                return;
            map[pawnId] = NormalizeAssignment(voiceModelId);
        }

        public static PawnVoiceBindingKind Classify(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw == DefaultModelId)
                return PawnVoiceBindingKind.Default;
            if (raw == RuleBasedModelId)
                return PawnVoiceBindingKind.RuleBased;
            if (raw == NoneModelId)
                return PawnVoiceBindingKind.Silent;
            return PawnVoiceBindingKind.Explicit;
        }

        public static string ManualChoice(string raw)
        {
            var kind = Classify(raw);
            if (kind == PawnVoiceBindingKind.Explicit || kind == PawnVoiceBindingKind.Silent)
                return raw;
            return null;
        }

        public static PawnVoiceDialogueDecision ForDialogue(string raw, bool automaticEnabled)
        {
            string manual = ManualChoice(raw);
            if (manual == NoneModelId)
                return new PawnVoiceDialogueDecision(true, false, string.Empty);
            bool automatic = automaticEnabled && manual == null;
            if (automatic)
                return new PawnVoiceDialogueDecision(false, true, string.Empty);
            return new PawnVoiceDialogueDecision(false, false, manual ?? string.Empty);
        }
    }
}
