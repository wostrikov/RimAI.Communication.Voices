using System;

namespace Ustas.RimAI.Communication.Voices.Policy
{
    /// <summary>
    /// The voice ids of the local Ukrainian server (tools/voices/local_tts_server.py),
    /// reached as the TTS-WebUI supplier.
    ///
    /// Its random voice picks an engine - Piper, tts_uk or StyleTTS2 - and a voice of it
    /// for every line, which is the point: a session played on it hears all three. A pawn
    /// should still keep its gender from line to line, so the random id is narrowed to the
    /// pawn's here; the server cannot see the pawn.
    /// </summary>
    public static class LocalTtsVoicePolicy
    {
        public const string Random = "uk-random";
        public const string RandomMale = "uk-random-male";
        public const string RandomFemale = "uk-random-female";

        /// <summary>
        /// <paramref name="voiceId"/> narrowed to the pawn's gender when it is the random
        /// voice, and unchanged otherwise. The server's "default" is the same random voice,
        /// and so is RULE_BASED: with no voice rule written for this supplier, or none that
        /// matches the pawn, the rule lookup hands back the default id, which is RULE_BASED
        /// itself - and the random voice by gender is the rule this server would have.
        /// <paramref name="male"/> is null for a pawn with no gender, or no pawn at all.
        /// </summary>
        public static string ForPawn(string voiceId, bool? male)
        {
            bool random = string.Equals(voiceId, Random, StringComparison.OrdinalIgnoreCase)
                          || string.Equals(voiceId, "default", StringComparison.OrdinalIgnoreCase)
                          || voiceId == "RULE_BASED";
            if (random && male == null)
                return Random;
            if (!random)
                return voiceId;
            return male.Value ? RandomMale : RandomFemale;
        }

        /// <summary>The fixed voices the server offers, as id and display name.</summary>
        public static readonly (string Id, string Name)[] FixedVoices =
        {
            ("piper:lada", "Piper - Лада"),
            ("piper:mykyta", "Piper - Микита"),
            ("piper:tetiana", "Piper - Тетяна"),
            ("tts_uk:lada", "tts_uk - Лада"),
            ("tts_uk:mykyta", "tts_uk - Микита"),
            ("tts_uk:tetiana", "tts_uk - Тетяна"),
            ("styletts2:single", "StyleTTS2 - Філатов"),
            ("styletts2:Олександр Ролдугін", "StyleTTS2 - Олександр Ролдугін"),
            ("styletts2:Вероніка Дорош", "StyleTTS2 - Вероніка Дорош"),
        };
    }
}
