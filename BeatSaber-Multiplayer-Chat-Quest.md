# Beat Saber Multiplayer Chat - Quest (QMod) port notes

This file is a **planning anchor** for a future **Quest standalone** build (`qmod`). The PC mod (`MultiplayerChat`, BSIPA + Zenject) will **not** share the same host: expect a **separate project/solution**.

## Quest repository location

**Suggested checkout:** `c:\Users\goria\BeatSaber-Multiplayer-Chat-Quest` (sibling of this PC repo; **avoid spaces in the folder path** so build scripts behave).

Clone your GitHub Quest repo there, then follow **`BeatSaber-Multiplayer-Chat-Quest/docs/NEXT_STEPS.md`** inside that repo (`README` links it).

## High-impact differences to expect

- **Bootstrap**: QMod entry instead of IPA `Plugin`; no Zenject scene installers. Use manual service location, scene hooks, or a tiny DI container.
- **XR / input**: No desktop `KeyCode.Space` PTT assumption; Quest uses controller / system UI patterns. `VrPttInput` paths need Quest-valid usages.
- **File paths**: `Assembly.Location` + `Plugins/Sounds` layout differs; keep a single `SoundPathResolver` (PC already searches multiple roots).
- **Networking**: Confirm MultiplayerCore / BeatTogether equivalents on Quest; packet IDs and encryption must stay compatible if cross-play matters.
- **UI**: BSML / HMUI availability on Quest builds; fallback layouts or reduced UI may be required.
- **Microphone**: Unity `Microphone` API behavior and permissions on Android; background policy and sample rates.
- **Harmony / patches**: Patch targets may differ per platform assembly.

## Suggested migration order

1. Blank QMod plugin that loads and logs in multiplayer.
2. Port **network + crypto + packet types** with integration tests against PC.
3. Port **ChatManager** core without full UI; in-game console or minimal text.
4. Port **UI** incrementally (lobby tab last).
5. Parity pass: hot mic, voice messages, presence, talk-to.

Update this document as Quest-specific decisions are made (folder name, repo layout, shared code via submodule, etc.).
