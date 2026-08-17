using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Voices.Service
{
    /// <summary>
    /// Small bounded cache of generated audio, keyed by the full rendered voice state
    /// rather than by text alone. Repeated short lines are common in colony chatter, so
    /// this saves paid requests, but only when the same pawn voice would produce
    /// literally the same audio.
    /// </summary>
    public static class TtsAudioCache
    {
        public const int MaxEntries = 64;

        static readonly Dictionary<string, byte[]> _entries = new Dictionary<string, byte[]>();
        static readonly LinkedList<string> _order = new LinkedList<string>();
        static readonly object _lock = new object();

        public static int Count
        {
            get { lock (_lock) { return _entries.Count; } }
        }

        public static bool TryGet(string key, out byte[] audio)
        {
            audio = null;
            if (string.IsNullOrEmpty(key))
                return false;

            lock (_lock)
            {
                if (!_entries.TryGetValue(key, out audio))
                    return false;

                _order.Remove(key);
                _order.AddLast(key);
                return audio != null && audio.Length > 0;
            }
        }

        public static void Store(string key, byte[] audio)
        {
            if (string.IsNullOrEmpty(key) || audio == null || audio.Length == 0)
                return;

            lock (_lock)
            {
                if (_entries.ContainsKey(key))
                    _order.Remove(key);

                _entries[key] = audio;
                _order.AddLast(key);

                while (_order.Count > MaxEntries)
                {
                    var oldest = _order.First;
                    if (oldest == null)
                        break;

                    _order.RemoveFirst();
                    _entries.Remove(oldest.Value);
                }
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _entries.Clear();
                _order.Clear();
            }
        }
    }
}
