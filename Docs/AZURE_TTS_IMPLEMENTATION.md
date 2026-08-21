# Azure TTS — current integration notes

Product: `RimAI.Communication.Voices`.

## Ownership

- Client: `Source/Service/AzureTTSClient.cs`
- Provider: `Source/Provider/AzureTTSProvider.cs`
- Settings UI surfaces live under `Source/UI/`

## Behavior (current)

- REST call to Azure Speech TTS
- SSML request construction (including prosody rate)
- WAV response handling (24 kHz 16-bit mono PCM typical path)
- Voice list retrieval via provider APIs
- Region + subscription key validation in settings/provider path

## Localization

Player-facing labels use RimWorld keyed resources. Do not hardcode new UI copy in C#.

Legacy keyed prefixes may still mention older product names in localization XML; those are localization/product debt, not comment cleanup.

## Related

- Custom Neural Voice: [AZURE_CUSTOM_VOICE.md](AZURE_CUSTOM_VOICE.md)
