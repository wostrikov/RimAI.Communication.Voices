using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Voices.Service
{
    public class TTSRequest
    {
        // API key (placed here so callers only need to pass one object)
        public string ApiKey { get; set; }

        public string Model { get; set; }
        public string Input { get; set; }
        public string InstructText { get; set; }

        // voice URI or preset (can be empty string for dynamic references)
        public string Voice { get; set; }

        // Free-form delivery guidance (OpenAI gpt-4o-mini-tts "instructions")
        public string Instructions { get; set; }

        // Requested audio container, e.g. mp3 or wav
        public string ResponseFormat { get; set; } = "mp3";

        // optional synthesis parameters
        public float Speed { get; set; } = 1.0f;
        public float Volume { get; set; } = 1.0f;

        // SSML prosody pitch such as +8Hz. Null leaves the base voice untouched.
        public string Pitch { get; set; }

        // Locale of the selected voice, e.g. uk-UA. Used for SSML instead of a fixed language.
        public string Locale { get; set; }

        public float Temperature { get; set; } = 0f;
        public float TopP { get; set; } = 0f;

        // Only used when caller wants to supply reference audio for dynamic voice
        public List<ReferenceAudio> References { get; set; }

        public class ReferenceAudio
        {
            public string Audio { get; set; }
            public string Text { get; set; }
        }
    }
}
