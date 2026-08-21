# RimAI.Communication.Voices

TTS / voice synthesis module for the RimAI suite (`ustas.rimai.communication.voices`).

## Ownership

- Product: `RimAI.Communication.Voices`
- Depends on RimAI Core / Communication host surfaces (not a standalone RimTalk addon)
- Credential domain for TTS: `OPENAI_RIMAI_TTS` (do not fall back across gameplay/translation domains)

## Providers

Supported provider families include Azure TTS, Gemini TTS, Edge TTS, Fish Audio, and related local/bootstrap helpers under `Source/Provider/` and `Source/Service/`.

Exact provider product names and API identifiers are contracts — keep them in code/config as-is.

## Docs

| Document | Purpose |
| --- | --- |
| [AZURE_TTS_IMPLEMENTATION.md](AZURE_TTS_IMPLEMENTATION.md) | Azure TTS ownership and integration notes |
| [AZURE_CUSTOM_VOICE.md](AZURE_CUSTOM_VOICE.md) | Azure Custom Neural Voice configuration |
| [FISHAUDIO_QUICKFIX.md](FISHAUDIO_QUICKFIX.md) | Fish Audio local bootstrap troubleshooting |

Historical donor product names (RimTalk TTS) in older commits are not current ownership.
