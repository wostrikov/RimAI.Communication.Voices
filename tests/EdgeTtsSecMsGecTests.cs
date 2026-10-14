using System;
using Ustas.RimAI.Communication.Voices.Service.EdgeTTSService;

/// <summary>
/// The Edge read-aloud service's handshake token, second form.
///
/// The service began answering the first form - a plain SHA-256 sent as
/// Sec-MS-GEC-Version 1 - with 403, and every Edge voice went silent; the
/// game logged three refused attempts and "All attempts failed" per line.
/// The expected values below are what edge-tts 7.3.1 (drm.py,
/// generate_sec_ms_gec_v2) returns for the same timestamps, so a change to the
/// port that alters a single bit fails here rather than as a 403 in play.
/// </summary>
internal static class EdgeTtsSecMsGecTests
{
    private const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";

    public static int Run()
    {
        int n = 0;
        void T(bool x, string s)
        {
            if (!x)
                throw new Exception("FAILED " + s);
            n++;
        }

        T(EdgeTtsSecMsGec.Generate(1791990000.0, TrustedClientToken)
            == "87887CB3E43131CC-73C4E36992FB0F4122386678621E0ECFAE0722C8DED7D4E7602455CF4DB2884F",
            "edge-tts-vector-1791990000");
        T(EdgeTtsSecMsGec.Generate(1700000000.0, TrustedClientToken)
            == "2CD23A2CA0B2595A-F3C846C370E05F9CC83ACC5F995C5F5D87A4D0F14CE8936A0F902E64099E38A2",
            "edge-tts-vector-1700000000");
        // The token names a five-minute window: any second inside it gives the same value.
        T(EdgeTtsSecMsGec.Generate(1791990000.0 + 299.9, TrustedClientToken)
            == EdgeTtsSecMsGec.Generate(1791990000.0, TrustedClientToken),
            "same-five-minute-window");
        return n;
    }
}
