# Azure Custom Neural Voice

Product: `RimAI.Communication.Voices`.

## Overview

Azure TTS supports standard neural voices and Custom Neural Voice (Professional Voice) deployments.

## Configuration

1. Mod settings → Voices / TTS settings
2. Select Azure TTS supplier
3. Enter Azure subscription key and region
4. For custom voices, set Deployment ID from Azure Speech Studio
5. Configure voice model IDs (standard names like `en-US-JennyNeural`, or custom deployment voices)

## Notes

- Provider terminology (Azure, Speech Studio, Deployment ID) is external contract language and may remain as-is
- Do not treat Azure product names as RimAI donor residue
- SSML / runtime request strings are runtime contracts — not comment-cleanup targets
