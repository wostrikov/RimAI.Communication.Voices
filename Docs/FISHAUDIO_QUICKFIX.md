# Fish Audio — quick troubleshooting

Product: `RimAI.Communication.Voices`.

Fish Audio uses a local Python HTTP helper (typically `127.0.0.1:5678`) started from the C# bootstrap path under `Source/Service/FishAudioService/`.

## Common failures

| Symptom | Likely cause | Action |
| --- | --- | --- |
| `ModuleNotFoundError: No module named 'fishaudio'` | Missing `fish-audio-sdk` | `pip install fish-audio-sdk` |
| Server failed to start within timeout | Slow Python start / missing deps | Run dependency check script in FishAudioService |
| Python process exited during startup | Broken Python env | Inspect Player.log for import/traceback |
| Invalid API key | Bad credentials in settings | Fix API key in mod settings |
| Reference voice ID not found | Bad model/voice id | Verify ID on Fish Audio site |
| Connection error | Network / firewall / proxy | Confirm access to `https://api.fish.audio` |

## Logs

Search Player.log for `FishAudio TTS` around startup and request failures.

## Note

External service names and package names are provider contracts, not donor-prose debt.
