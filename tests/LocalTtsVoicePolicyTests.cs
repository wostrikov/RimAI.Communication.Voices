using System;
using Ustas.RimAI.Communication.Voices.Policy;

/// <summary>
/// The local server's random voice changes engine every line; the pawn's gender is what
/// keeps it one person. The server cannot see the pawn, so the renderer narrows the id.
/// </summary>
internal static class LocalTtsVoicePolicyTests
{
    public static int Run()
    {
        int n = 0;
        void T(bool x, string s)
        {
            if (!x)
                throw new Exception("FAILED " + s);
            n++;
        }

        T(LocalTtsVoicePolicy.ForPawn("uk-random", true) == "uk-random-male", "random-male-pawn");
        T(LocalTtsVoicePolicy.ForPawn("uk-random", false) == "uk-random-female", "random-female-pawn");
        T(LocalTtsVoicePolicy.ForPawn("default", true) == "uk-random-male", "server-default-is-random");
        T(LocalTtsVoicePolicy.ForPawn("uk-random", null) == "uk-random", "no-gender-stays-random");
        T(LocalTtsVoicePolicy.ForPawn("RULE_BASED", false) == "uk-random-female", "unmatched-rule-is-random");
        T(LocalTtsVoicePolicy.ForPawn("RULE_BASED", null) == "uk-random", "unmatched-rule-no-gender");
        // A voice the player fixed, or the random voice already narrowed, is left as it is.
        T(LocalTtsVoicePolicy.ForPawn("piper:lada", true) == "piper:lada", "fixed-voice-untouched");
        T(LocalTtsVoicePolicy.ForPawn("uk-random-female", true) == "uk-random-female", "narrowed-voice-untouched");
        return n;
    }
}
