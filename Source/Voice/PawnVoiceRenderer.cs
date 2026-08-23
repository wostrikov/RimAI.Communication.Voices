using System;
using Ustas.RimAI.Communication.Voices.Data;
using Ustas.RimAI.Communication.Voices.Policy;
using Ustas.RimAI.Communication.Voices.Service;
using Ustas.RimAI.Core.Voices;
using Verse;

namespace Ustas.RimAI.Communication.Voices.Voice
{
    /// <summary>Provider-ready values for one pawn's next line, plus the cache signature that produced them.</summary>
    public sealed class ResolvedPawnVoice
    {
        public bool Silent { get; set; }
        public string VoiceId { get; set; }
        public string Model { get; set; }
        public float Speed { get; set; } = 1f;
        public string Instructions { get; set; }

        /// <summary>SSML pitch such as +8Hz, or null when the provider has no pitch control.</summary>
        public string Pitch { get; set; }

        /// <summary>Locale of the chosen voice, or null when the provider does not take one.</summary>
        public string Locale { get; set; }

        public VoiceRenderSignature Signature { get; set; } = new VoiceRenderSignature();

        public static ResolvedPawnVoice SilentVoice() => new ResolvedPawnVoice { Silent = true };
    }

    /// <summary>
    /// The single place where a persisted identity becomes provider parameters.
    ///
    /// Resolution happens on the caller's thread before any async work starts, so the
    /// pawn is only inspected while it is safe to do so, and the background pipeline
    /// works from plain values.
    /// </summary>
    public static class PawnVoiceRenderer
    {
        public static VoiceProviderKind KindOf(TTSSettings.TTSSupplier supplier)
        {
            switch (supplier)
            {
                case TTSSettings.TTSSupplier.OpenAI: return VoiceProviderKind.OpenAI;
                case TTSSettings.TTSSupplier.EdgeTTS: return VoiceProviderKind.EdgeTTS;
                case TTSSettings.TTSSupplier.AzureTTS: return VoiceProviderKind.AzureTTS;
                case TTSSettings.TTSSupplier.FishAudio: return VoiceProviderKind.FishAudio;
                case TTSSettings.TTSSupplier.CosyVoice: return VoiceProviderKind.CosyVoice;
                case TTSSettings.TTSSupplier.IndexTTS: return VoiceProviderKind.IndexTTS;
                case TTSSettings.TTSSupplier.GeminiTTS: return VoiceProviderKind.GeminiTTS;
                case TTSSettings.TTSSupplier.TTSWebUI: return VoiceProviderKind.TTSWebUI;
                default: return VoiceProviderKind.None;
            }
        }

        /// <summary>True when the automatic identity path can drive this supplier end to end.</summary>
        public static bool SupportsAutomaticVoices(TTSSettings.TTSSupplier supplier)
        {
            var kind = KindOf(supplier);
            return kind == VoiceProviderKind.OpenAI || kind == VoiceProviderKind.EdgeTTS;
        }

        public static ResolvedPawnVoice Resolve(Pawn pawn, TTSSettings settings)
        {
            if (settings == null)
                return ResolvedPawnVoice.SilentVoice();

            string raw = pawn == null ? null : PawnVoiceManager.GetRawVoiceModel(pawn);
            bool automaticEnabled = settings.AutomaticPawnVoices
                                    && SupportsAutomaticVoices(settings.Supplier);
            var decision = PawnVoiceBindingPolicy.ForDialogue(raw, automaticEnabled);
            if (decision.Silent)
                return ResolvedPawnVoice.SilentVoice();

            if (decision.UseAutomatic)
            {
                var identity = PawnVoiceIdentityStore.GetOrCreate(pawn);
                if (identity != null)
                    return RenderIdentity(identity, settings);
            }

            string manualChoice = string.IsNullOrEmpty(decision.ExplicitVoiceId)
                ? null
                : decision.ExplicitVoiceId;
            return RenderConfigured(pawn, settings, manualChoice);
        }

        /// <summary>Renders any identity, including one that is only being previewed.</summary>
        public static ResolvedPawnVoice RenderIdentity(PawnVoiceIdentity identity, TTSSettings settings)
        {
            string language = LanguageOf();

            if (KindOf(settings.Supplier) == VoiceProviderKind.EdgeTTS)
            {
                var edge = EdgeVoiceRenderer.Render(identity, language);
                return new ResolvedPawnVoice
                {
                    VoiceId = edge.VoiceName,
                    Model = settings.GetSupplierModel(settings.Supplier),
                    Speed = 1f + (edge.RatePercent / 100f),
                    Instructions = null,
                    Pitch = edge.Pitch,
                    Locale = edge.Locale,
                    Signature = VoiceRenderSignature.FromEdge(edge)
                };
            }

            string configuredModel = settings.GetSupplierModel(settings.Supplier);
            var openAi = OpenAiVoiceRenderer.Render(identity, language, configuredModel);

            return new ResolvedPawnVoice
            {
                VoiceId = openAi.Voice,
                Model = openAi.Model,
                Speed = openAi.Speed,
                Instructions = openAi.Instructions,
                Pitch = null,
                Locale = null,
                Signature = VoiceRenderSignature.FromOpenAi(openAi, language)
            };
        }

        /// <summary>
        /// Legacy and manual-override path: an explicitly chosen voice, or a provider
        /// the automatic identity path cannot render yet.
        /// </summary>
        static ResolvedPawnVoice RenderConfigured(Pawn pawn, TTSSettings settings, string manualChoice)
        {
            string voiceId = manualChoice ?? PawnVoiceManager.GetVoiceModel(pawn);
            if (string.IsNullOrEmpty(voiceId) || voiceId == VoiceModel.NONE_MODEL_ID)
                return ResolvedPawnVoice.SilentVoice();

            string model = settings.GetSupplierModel(settings.Supplier);
            string instructions = settings.GetSupplierInstructions(settings.Supplier);
            float speed = settings.GetSupplierSpeed(settings.Supplier);
            string locale = KindOf(settings.Supplier) == VoiceProviderKind.EdgeTTS
                ? EdgeVoiceCatalog.LocaleOf(voiceId)
                : null;

            return new ResolvedPawnVoice
            {
                VoiceId = voiceId,
                Model = model,
                Speed = speed,
                Instructions = instructions,
                Pitch = null,
                Locale = locale,
                Signature = new VoiceRenderSignature
                {
                    Provider = KindOf(settings.Supplier),
                    Model = model ?? string.Empty,
                    VoiceId = voiceId,
                    Style = instructions ?? string.Empty,
                    Speed = speed,
                    PitchHz = 0,
                    Language = locale ?? LanguageOf()
                }
            };
        }

        static string LanguageOf()
        {
            try
            {
                return VoiceSharedAiText.Language;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
