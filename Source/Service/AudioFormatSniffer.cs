namespace Ustas.RimAI.Communication.Voices;

/// <summary>
/// Tells what a speech provider sent back by looking at it.
///
/// A provider's Content-Type is a hint and not always a true one, so the
/// first bytes are read as well: an ID3 tag, or the eleven set bits every
/// MPEG audio frame starts with.
/// </summary>
internal static class AudioFormatSniffer
{
    public static bool LooksLikeMp3(byte[] data, string mediaType)
    {
        if (!string.IsNullOrEmpty(mediaType) && (mediaType.Contains("mpeg") || mediaType.Contains("mp3")))
            return true;
        if (data == null)
            return false;
        if (data.Length >= 3 && data[0] == (byte)'I' && data[1] == (byte)'D' && data[2] == (byte)'3')
            return true;
        return data.Length >= 2 && data[0] == 0xFF && (data[1] & 0xE0) == 0xE0;
    }
}
