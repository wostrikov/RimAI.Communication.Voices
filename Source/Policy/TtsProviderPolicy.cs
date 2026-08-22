using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ustas.RimAI.Communication.Voices.Policy
{
    public enum TtsProviderKind
    {
        None,
        EdgeTts,
        OpenAi,
        Azure,
        Gemini,
        FishAudio,
        CosyVoice,
        IndexTts,
        TtsWebUi
    }

    public enum TtsFailureClass
    {
        Success,
        Transient,
        Auth,
        Configuration,
        Cancelled,
        Exhausted
    }

    public sealed class TtsProviderSlot
    {
        public TtsProviderKind Kind;
        public bool RequiresCredential;
        public bool CredentialPresent;
        public bool IsKeyless;
    }

    public sealed class TtsSlotResult
    {
        public TtsFailureClass Class;
        public byte[] Audio;
    }

    public sealed class TtsProviderOutcome
    {
        public TtsFailureClass Class;
        public TtsProviderKind UsedKind;
        public byte[] Audio;
        public int Attempts;
    }

    public static class TtsProviderChain
    {
        public static IReadOnlyList<TtsProviderSlot> Build(TtsProviderKind preferred, bool preferredCredentialPresent)
        {
            var slots = new List<TtsProviderSlot>();
            if (preferred == TtsProviderKind.None)
            {
                slots.Add(Slot(TtsProviderKind.None, false, true));
                return slots;
            }

            bool preferredKeyless = !RequiresCredential(preferred);
            if (preferredKeyless || preferredCredentialPresent)
                slots.Add(Slot(preferred, !preferredKeyless, preferredCredentialPresent));

            if (preferred != TtsProviderKind.EdgeTts)
                slots.Add(Slot(TtsProviderKind.EdgeTts, false, true));

            return slots;
        }

        public static bool RequiresCredential(TtsProviderKind kind)
        {
            return kind != TtsProviderKind.None && kind != TtsProviderKind.EdgeTts;
        }

        static TtsProviderSlot Slot(TtsProviderKind kind, bool requiresCredential, bool present)
        {
            return new TtsProviderSlot
            {
                Kind = kind,
                RequiresCredential = requiresCredential,
                CredentialPresent = present,
                IsKeyless = !requiresCredential
            };
        }
    }

    public static class TtsFailureClassifier
    {
        public static TtsFailureClass Classify(int? status, bool cancelled, bool emptyAudio)
        {
            if (cancelled)
                return TtsFailureClass.Cancelled;
            if (status == 401 || status == 403)
                return TtsFailureClass.Auth;
            if (status == 400 || status == 404)
                return TtsFailureClass.Configuration;
            if (emptyAudio)
                return TtsFailureClass.Transient;
            if (status == 429 || (status >= 500 && status <= 599) || status == null)
                return TtsFailureClass.Transient;
            return TtsFailureClass.Transient;
        }
    }

    public static class TtsProviderOrchestrator
    {
        public static TtsProviderOutcome Execute(
            IReadOnlyList<TtsProviderSlot> chain,
            Func<TtsProviderSlot, TtsSlotResult> attempt)
        {
            var outcome = new TtsProviderOutcome
            {
                Class = TtsFailureClass.Exhausted,
                UsedKind = TtsProviderKind.None
            };
            if (chain == null || attempt == null)
                return outcome;

            for (int i = 0; i < chain.Count; i++)
            {
                TtsProviderSlot slot = chain[i];
                if (slot == null || slot.Kind == TtsProviderKind.None)
                    continue;
                if (slot.RequiresCredential && !slot.CredentialPresent)
                    continue;

                outcome.Attempts++;
                TtsSlotResult result = attempt(slot) ?? new TtsSlotResult { Class = TtsFailureClass.Transient };
                if (result.Class == TtsFailureClass.Success && result.Audio != null && result.Audio.Length > 0)
                {
                    outcome.Class = TtsFailureClass.Success;
                    outcome.UsedKind = slot.Kind;
                    outcome.Audio = result.Audio;
                    return outcome;
                }

                if (result.Class == TtsFailureClass.Auth
                    || result.Class == TtsFailureClass.Configuration
                    || result.Class == TtsFailureClass.Cancelled)
                {
                    outcome.Class = result.Class;
                    outcome.UsedKind = slot.Kind;
                    return outcome;
                }
            }

            outcome.Class = TtsFailureClass.Exhausted;
            return outcome;
        }

        public static async Task<TtsProviderOutcome> ExecuteAsync(
            IReadOnlyList<TtsProviderSlot> chain,
            Func<TtsProviderSlot, Task<TtsSlotResult>> attempt)
        {
            var outcome = new TtsProviderOutcome
            {
                Class = TtsFailureClass.Exhausted,
                UsedKind = TtsProviderKind.None
            };
            if (chain == null || attempt == null)
                return outcome;

            for (int i = 0; i < chain.Count; i++)
            {
                TtsProviderSlot slot = chain[i];
                if (slot == null || slot.Kind == TtsProviderKind.None)
                    continue;
                if (slot.RequiresCredential && !slot.CredentialPresent)
                    continue;

                outcome.Attempts++;
                TtsSlotResult result = await attempt(slot).ConfigureAwait(false)
                    ?? new TtsSlotResult { Class = TtsFailureClass.Transient };
                if (result.Class == TtsFailureClass.Success && result.Audio != null && result.Audio.Length > 0)
                {
                    outcome.Class = TtsFailureClass.Success;
                    outcome.UsedKind = slot.Kind;
                    outcome.Audio = result.Audio;
                    return outcome;
                }

                if (result.Class == TtsFailureClass.Auth
                    || result.Class == TtsFailureClass.Configuration
                    || result.Class == TtsFailureClass.Cancelled)
                {
                    outcome.Class = result.Class;
                    outcome.UsedKind = slot.Kind;
                    return outcome;
                }
            }

            outcome.Class = TtsFailureClass.Exhausted;
            return outcome;
        }
    }
}
