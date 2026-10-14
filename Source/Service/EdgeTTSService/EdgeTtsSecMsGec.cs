using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Ustas.RimAI.Communication.Voices.Service.EdgeTTSService
{
    /// <summary>
    /// The Sec-MS-GEC token the Edge read-aloud service asks every connection
    /// for, in its second form.
    ///
    /// The first form was the SHA-256 of the five-minute Windows file time and
    /// the trusted client token, sent with Sec-MS-GEC-Version 1-&lt;Chromium&gt;.
    /// The service now answers that with 403 on the WebSocket handshake. The
    /// second form, sent with version 2-&lt;Chromium&gt;, hashes the same string
    /// with a SHA-256 whose first block starts from a different state and
    /// constants, and prefixes a 64-bit CS64 digest keyed by it:
    /// <c>&lt;16 hex&gt;-&lt;64 hex&gt;</c>. The constants and the shape are
    /// the ones the edge-tts project (rany2/edge-tts, drm.py, release 7.3.1)
    /// reads from Edge; this is written from that description, and checked
    /// against its output for fixed timestamps in the Voices tests.
    /// </summary>
    internal static class EdgeTtsSecMsGec
    {
        private const long WinEpochSeconds = 11644473600;

        private static readonly uint[] Sha256Initial =
        {
            0x6A09E667, 0xBB67AE85, 0x3C6EF372, 0xA54FF53A, 0x510E527F, 0x9B05688C, 0x1F83D9AB, 0x5BE0CD19,
        };

        private static readonly uint[] Sha256State =
        {
            0xFD9FBEA2, 0x91243CE7, 0xBE499094, 0xCD2E1E0A, 0x6C006F49, 0x04BD35AB, 0xFEB0A7E4, 0x03EFFBD2,
        };

        private static readonly uint[] Sha256Schedule =
        {
            0xEB24BF9E, 0x1558691F, 0xA0C09EE2, 0x0CFA32BA, 0xA622EE8C, 0xD1DD9491, 0xC0DD25A0,
            0xF9E70AB6, 0xF0C31573, 0x7058CE4E, 0x7188E606, 0x00E5AB33, 0xD258B060, 0x72766564,
        };

        private static readonly uint[] RoundConstants =
        {
            0x428A2F98, 0x71374491, 0xB5C0FBCF, 0xE9B5DBA5, 0x3956C25B, 0x59F111F1, 0x923F82A4, 0xAB1C5ED5,
            0xD807AA98, 0x12835B01, 0x243185BE, 0x550C7DC3, 0x72BE5D74, 0x80DEB1FE, 0x9BDC06A7, 0xC19BF174,
            0xE49B69C1, 0xEFBE4786, 0x0FC19DC6, 0x240CA1CC, 0x2DE92C6F, 0x4A7484AA, 0x5CB0A9DC, 0x76F988DA,
            0x983E5152, 0xA831C66D, 0xB00327C8, 0xBF597FC7, 0xC6E00BF3, 0xD5A79147, 0x06CA6351, 0x14292967,
            0x27B70A85, 0x2E1B2138, 0x4D2C6DFC, 0x53380D13, 0x650A7354, 0x766A0ABB, 0x81C2C92E, 0x92722C85,
            0xA2BFE8A1, 0xA81A664B, 0xC24B8B70, 0xC76C51A3, 0xD192E819, 0xD6990624, 0xF40E3585, 0x106AA070,
            0x19A4C116, 0x1E376C08, 0x2748774C, 0x34B0BCB5, 0x391C0CB3, 0x4ED8AA4A, 0x5B9CCA4F, 0x682E6FF3,
            0x748F82EE, 0x78A5636F, 0x84C87814, 0x8CC70208, 0x90BEFFFA, 0xA4506CEB, 0xBEF9A3F7, 0xC67178F2,
        };

        private const uint Sha256Adjustment = 0xC9DC06A7;
        private const uint Sigma0Adjustment = 0x059C0B80;
        private const uint Sigma1Adjustment = 0x000B92C0;

        // The first block's round 14 uses the adjustment in place of the standard constant.
        private static readonly uint[] FirstBlockConstants = BuildFirstBlockConstants();

        private static readonly uint[,] Cs64Coefficients =
        {
            { 0xE82E91B1, 0xB32700D0 }, { 0x44DE24C6, 0x88DFF51D }, { 0x3BEB65BC, 0x29514038 },
            { 0xC89C5933, 0xB328CBDF }, { 0x8D1EC211, 0xD405B4D5 }, { 0x314EF5D5, 0x26E55F3C },
            { 0xE92491B1, 0x607801C6 }, { 0xF0A5B9D5, 0x82B2C1FB }, { 0x06064CF3, 0xFA51A3DA },
            { 0xDA79FA60, 0x207CC464 }, { 0xBED20560, 0x3C3C74F3 }, { 0x89F9C9F8, 0xD2D14B61 },
            { 0x35FA38C6, 0xBB04A2C1 }, { 0xCB49E3C6, 0xC5626167 },
        };

        private const uint Cs64Factor = 0x82893933;
        private const uint Cs64Adjustment0 = 0xF2F90B1A;
        private const uint Cs64Adjustment1 = 0x0B1A0000;

        /// <summary>The token for <paramref name="unixSeconds"/>, Unix time as the service's clock reads it.</summary>
        internal static string Generate(double unixSeconds, string trustedClientToken)
        {
            // Windows file time, rounded down to five minutes, in 100 ns units.
            long seconds = (long)Math.Floor(unixSeconds) + WinEpochSeconds;
            seconds -= seconds % 300;
            long ticks = seconds * 10_000_000L;
            byte[] data = Encoding.ASCII.GetBytes(ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) + trustedClientToken);

            byte[] digest = Sha256Digest(data);
            (uint first, uint second) = Cs64Digest(data, digest);
            byte[] cs64 = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(cs64.AsSpan(0), first);
            BinaryPrimitives.WriteUInt32LittleEndian(cs64.AsSpan(4), second);
            return Convert.ToHexString(cs64) + "-" + Convert.ToHexString(digest);
        }

        private static uint[] BuildFirstBlockConstants()
        {
            uint[] constants = (uint[])RoundConstants.Clone();
            constants[14] = Sha256Adjustment;
            return constants;
        }

        private static uint SmallSigma0(uint x) => BitOperations.RotateRight(x, 7) ^ BitOperations.RotateRight(x, 18) ^ (x >> 3);

        private static uint SmallSigma1(uint x) => BitOperations.RotateRight(x, 17) ^ BitOperations.RotateRight(x, 19) ^ (x >> 10);

        private static uint Rol16(uint x) => BitOperations.RotateLeft(x, 16);

        private static void ExpandSchedule(uint[] words, int firstWord)
        {
            for (int i = firstWord; i < 64; i++)
            {
                words[i] = unchecked(SmallSigma1(words[i - 2]) + words[i - 7] + SmallSigma0(words[i - 15]) + words[i - 16]);
            }
        }

        private static uint[] Rounds(uint[] state, uint[] words, int firstRound)
        {
            uint[] constants = firstRound == 14 ? FirstBlockConstants : RoundConstants;
            uint a = state[0], b = state[1], c = state[2], d = state[3];
            uint e = state[4], f = state[5], g = state[6], h = state[7];
            for (int i = firstRound; i < 64; i++)
            {
                unchecked
                {
                    uint t1 = h + (BitOperations.RotateRight(e, 6) ^ BitOperations.RotateRight(e, 11) ^ BitOperations.RotateRight(e, 25))
                        + (g ^ (e & (f ^ g))) + constants[i] + words[i];
                    uint t2 = (BitOperations.RotateRight(a, 2) ^ BitOperations.RotateRight(a, 13) ^ BitOperations.RotateRight(a, 22))
                        + ((a & b) | (c & (a | b)));
                    h = g;
                    g = f;
                    f = e;
                    e = d + t1;
                    d = c;
                    c = b;
                    b = a;
                    a = t1 + t2;
                }
            }

            return new[] { a, b, c, d, e, f, g, h };
        }

        private static uint[] InitialState(byte[] data)
        {
            uint[] words = new uint[64];
            words[14] = ((uint)data[0] << 16) | ((uint)data[1] << 8) | data[2];
            words[15] = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(3, 4));
            for (int i = 16; i < 30; i++)
            {
                unchecked
                {
                    uint value = Sha256Schedule[i - 16];
                    if (i - 2 >= 14)
                    {
                        value += i == 16 ? SmallSigma1(words[i - 2]) ^ Sigma1Adjustment : SmallSigma1(words[i - 2]);
                    }
                    if (i - 7 >= 14)
                    {
                        value += words[i - 7];
                    }
                    if (i - 15 >= 14)
                    {
                        value += i == 29 ? SmallSigma0(words[i - 15]) ^ Sigma0Adjustment : SmallSigma0(words[i - 15]);
                    }
                    words[i] = value;
                }
            }

            words[30] = unchecked(SmallSigma1(words[28]) + words[23] + SmallSigma0(words[15]) + words[14]
                + Sha256Adjustment - RoundConstants[14]);
            ExpandSchedule(words, 31);
            uint[] working = Rounds(Sha256State, words, 14);
            uint[] state = new uint[8];
            for (int i = 0; i < 8; i++)
            {
                state[i] = unchecked(Sha256Initial[i] + working[i]);
            }
            return state;
        }

        private static byte[] Sha256Digest(byte[] data)
        {
            if (data.Length < 7)
            {
                throw new ArgumentException("The token string must be at least seven bytes long.", nameof(data));
            }

            long length = 57 + data.Length;
            int zeros = (int)(((55 - length) % 64 + 64) % 64);
            byte[] padded = new byte[data.Length - 7 + 1 + zeros + 8];
            Array.Copy(data, 7, padded, 0, data.Length - 7);
            padded[data.Length - 7] = 0x80;
            BinaryPrimitives.WriteUInt64BigEndian(padded.AsSpan(padded.Length - 8), (ulong)(length * 8));

            uint[] state = InitialState(data);
            uint[] words = new uint[64];
            for (int offset = 0; offset < padded.Length; offset += 64)
            {
                for (int i = 0; i < 16; i++)
                {
                    words[i] = BinaryPrimitives.ReadUInt32BigEndian(padded.AsSpan(offset + i * 4, 4));
                }
                ExpandSchedule(words, 16);
                uint[] working = Rounds(state, words, 0);
                for (int i = 0; i < 8; i++)
                {
                    state[i] = unchecked(state[i] + working[i]);
                }
            }

            byte[] digest = new byte[32];
            for (int i = 0; i < 8; i++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(digest.AsSpan(i * 4), state[i]);
            }
            return digest;
        }

        private static (uint, uint) Cs64Digest(byte[] data, byte[] digest)
        {
            unchecked
            {
                uint keyA = BinaryPrimitives.ReadUInt32LittleEndian(digest.AsSpan(0)) | 1;
                uint keyB = BinaryPrimitives.ReadUInt32LittleEndian(digest.AsSpan(4)) | 1;
                uint[] factors = { keyA * Cs64Factor, keyB * Cs64Factor };

                int coefficientCount = Cs64Coefficients.GetLength(0);
                uint[] words = LittleEndianWordsEven(data);
                uint[] terms = new uint[coefficientCount + words.Length];
                uint[] offsets = new uint[coefficientCount + words.Length];
                for (int i = 0; i < coefficientCount; i++)
                {
                    terms[i] = Cs64Coefficients[i, 0] * factors[i % 2];
                    offsets[i] = Cs64Coefficients[i, 1];
                }
                for (int i = 0; i < words.Length; i++)
                {
                    uint word = words[i];
                    uint term = word * (i % 2 == 0 ? keyA : keyB);
                    uint offset = Rol16(word) * (i % 2 == 0 ? 0xEF0569FB : 0xC31713DB);
                    if (i == 0)
                    {
                        term += Cs64Adjustment0 * factors[0];
                        offset += Cs64Adjustment1;
                    }
                    terms[coefficientCount + i] = term;
                    offsets[coefficientCount + i] = offset;
                }

                uint swapAccumulator = 0, swapLast = 0, state = 0, accumulator = 0;
                for (int i = 0; i < terms.Length; i += 2)
                {
                    uint x = terms[i], y = terms[i + 1];
                    uint t = Rol16(offsets[i] + x);
                    uint q = t * 0x76F50F34;
                    swapAccumulator += Rol16(q) * 0xC3EFEA97 + q;
                    t = Rol16(offsets[i + 1] + y);
                    q = t * 0x1881CE3C;
                    swapLast = Rol16(q) * 0x35BD1EC9 + q;
                    swapAccumulator += swapLast;

                    state = state * keyA + x;
                    state = Rol16(state) * 0xCF98B111;
                    state = Rol16(state) * 0x87085B9F;
                    state = Rol16(state) * 0x12CEB96D;
                    state = Rol16(state) * 0x257E1D83;
                    accumulator += state;
                    t = state * keyB + y;
                    t = Rol16(t) * 0xA27416F5;
                    t = Rol16(t) * 0xD38396FF;
                    state = Rol16(Rol16(t) * 0x7C932B89) * 0xBFA49F69;
                    accumulator += state;
                }

                return (swapLast ^ state, swapAccumulator ^ accumulator);
            }
        }

        // The data behind a zero byte, as little-endian words, zero-padded to an even count.
        private static uint[] LittleEndianWordsEven(byte[] data)
        {
            int length = data.Length + 1;
            int padded = (length + 3) / 4 * 4;
            byte[] bytes = new byte[padded];
            Array.Copy(data, 0, bytes, 1, data.Length);
            int count = padded / 4;
            uint[] words = new uint[count + count % 2];
            for (int i = 0; i < count; i++)
            {
                words[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i * 4, 4));
            }
            return words;
        }
    }
}
