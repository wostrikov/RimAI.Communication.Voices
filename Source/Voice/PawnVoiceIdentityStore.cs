using System;
using System.Collections.Generic;
using Ustas.RimAI.Core.Voices;
using Verse;

namespace Ustas.RimAI.Communication.Voices.Voice
{
    /// <summary>
    /// Owns pawn voice identities inside the save.
    ///
    /// An identity is generated once, on whichever comes first: an explicit creation
    /// path or the first time the pawn needs to speak. After that it is reused for the
    /// life of the pawn, so ageing, reloading or switching TTS provider never changes
    /// who a colonist sounds like. Core stays free of Verse: the identity travels
    /// through <see cref="PawnVoiceIdentityCodec"/> as a compact string.
    /// </summary>
    public static class PawnVoiceIdentityStore
    {
        static Dictionary<string, string> _encodedIdentities = new Dictionary<string, string>();
        static readonly Dictionary<string, PawnVoiceIdentity> _decodedCache = new Dictionary<string, PawnVoiceIdentity>();
        static readonly object _lock = new object();

        public static int Count
        {
            get { lock (_lock) { return _encodedIdentities.Count; } }
        }

        /// <summary>Existing identity, or null. Never generates.</summary>
        public static PawnVoiceIdentity Peek(Pawn pawn)
        {
            string key = PawnVoiceFeatureExtractor.StableKeyFor(pawn);
            if (string.IsNullOrEmpty(key))
                return null;

            lock (_lock)
            {
                return Lookup(key);
            }
        }

        /// <summary>
        /// The lazy path that guarantees a pawn can always speak: look up, otherwise
        /// extract features, generate against the current colony and persist.
        /// </summary>
        public static PawnVoiceIdentity GetOrCreate(Pawn pawn)
        {
            if (pawn == null)
                return null;

            string key = PawnVoiceFeatureExtractor.StableKeyFor(pawn);
            if (string.IsNullOrEmpty(key))
                return null;

            VoiceFeatureSet features;
            int currentAge;

            lock (_lock)
            {
                var existing = Lookup(key);
                if (existing != null)
                {
                    currentAge = CurrentAge(pawn, existing.CreatedFromBiologicalAge);
                    if (!PawnVoiceIdentityGenerator.ShouldMatureToAdult(existing, currentAge))
                        return existing;

                    var matured = PawnVoiceIdentityGenerator.MatureToAdult(existing, currentAge);
                    Store(key, matured);
                    return matured;
                }
            }

            // Feature extraction touches the pawn, so it happens outside the lock.
            try
            {
                features = PawnVoiceFeatureExtractor.Extract(pawn);
                currentAge = features.BiologicalAge;
            }
            catch (Exception ex)
            {
                Log.Warning("[RimAI.Voices] Voice feature extraction failed for " + key + ": " + ex.Message);
                features = new VoiceFeatureSet { StableKey = key };
                currentAge = features.BiologicalAge;
            }

            lock (_lock)
            {
                // Another path may have generated while features were being read.
                var raced = Lookup(key);
                if (raced != null)
                    return raced;

                var identity = PawnVoiceIdentityGenerator.Generate(features, SnapshotLocked());
                Store(key, identity);

                if (Prefs.DevMode)
                    Log.Message("[RimAI.Voices] Generated voice identity for " + key + ": " + identity);

                return identity;
            }
        }

        /// <summary>Player-authored identity. Marked as an override so it is never matured or regenerated.</summary>
        public static void SetManualOverride(Pawn pawn, PawnVoiceIdentity identity)
        {
            string key = PawnVoiceFeatureExtractor.StableKeyFor(pawn);
            if (string.IsNullOrEmpty(key) || identity == null)
                return;

            var copy = identity.Copy();
            copy.IsManualOverride = true;

            lock (_lock)
            {
                Store(key, copy.Normalize());
            }
        }

        /// <summary>Drops the stored identity so the next request generates a fresh one.</summary>
        public static void Regenerate(Pawn pawn)
        {
            Remove(pawn);
            GetOrCreate(pawn);
        }

        public static void Remove(Pawn pawn)
        {
            string key = PawnVoiceFeatureExtractor.StableKeyFor(pawn);
            if (string.IsNullOrEmpty(key))
                return;

            lock (_lock)
            {
                _encodedIdentities.Remove(key);
                _decodedCache.Remove(key);
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _encodedIdentities.Clear();
                _decodedCache.Clear();
            }
        }

        /// <summary>Identities already assigned in this save, used as the diversity reference set.</summary>
        public static IReadOnlyList<PawnVoiceIdentity> Snapshot()
        {
            lock (_lock)
            {
                return SnapshotLocked();
            }
        }

        public static void ExposeData()
        {
            Scribe_Collections.Look(ref _encodedIdentities, "pawnVoiceIdentities", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (_encodedIdentities == null)
                    _encodedIdentities = new Dictionary<string, string>();

                lock (_lock)
                {
                    _decodedCache.Clear();
                }
            }
        }

        static List<PawnVoiceIdentity> SnapshotLocked()
        {
            var result = new List<PawnVoiceIdentity>(_encodedIdentities.Count);
            foreach (var pair in _encodedIdentities)
            {
                var identity = Lookup(pair.Key);
                if (identity != null)
                    result.Add(identity);
            }

            return result;
        }

        static PawnVoiceIdentity Lookup(string key)
        {
            if (_decodedCache.TryGetValue(key, out var cached))
                return cached;

            if (!_encodedIdentities.TryGetValue(key, out string encoded))
                return null;

            var decoded = PawnVoiceIdentityCodec.Decode(encoded);
            if (decoded == null)
            {
                // A row we cannot read is treated as missing so it regenerates cleanly.
                _encodedIdentities.Remove(key);
                return null;
            }

            _decodedCache[key] = decoded;
            return decoded;
        }

        static void Store(string key, PawnVoiceIdentity identity)
        {
            _encodedIdentities[key] = PawnVoiceIdentityCodec.Encode(identity);
            _decodedCache[key] = identity;
        }

        static int CurrentAge(Pawn pawn, int fallback)
        {
            try
            {
                int age = pawn?.ageTracker?.AgeBiologicalYears ?? fallback;
                return age <= 0 ? fallback : age;
            }
            catch (Exception)
            {
                return fallback;
            }
        }
    }
}
