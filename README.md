# Multiplayer Chat

End-to-end encrypted text and voice for BeatTogether multiplayer. Chat and voice payloads are encrypted on your device; only players in your session can decrypt them. The relay server sees opaque bytes, not your messages or voice.

## Features (implemented)

- **Encrypted text chat**: AES-256-CBC + HMAC. Session key from connected player IDs; only lobby members derive the key.
- **Voice messages**: Record, send, and play short voice clips over the same encrypted channel.
- **Hot mic (continuous voice)**: Low-latency streamed voice chat.
- **DMs**: Direct messages to a chosen player in the lobby.
- **Mute / unmute**: Per-player mute for incoming voice.
- **Deafen**: Local deafen.
- **Talk-to / listen filters**: Restrict who you send voice to and whose voice you hear when those modes are active.
- **Receive gain**: Per-sender playback level applied in PCM before playback (consistent with voice message path).
- **Chat activity**: Typing / recording presence hints for other players (where supported by UI).
- **Mod tab**: "Multiplayer chat" in mod tabs (lobby-side).
- **Keyboard UI**: Letters, numbers, and symbols for composing messages.

## Plans

- **Custom avatars**: Permit custom avatars to replace the current Beat Saber multiplayer avatarr, able to be turned off at the flick of a switch. 
- **Friend service**: ChatID based friends beyond the current lobby list.
- **Join friends**: Connect to a friend's session from the menu or a friends list.
- **Private calls**: Out-of-lobby or dedicated voice sessions (design is TBD).
- **DM from menu**: Start or continue DMs without being in a multiplayer lobby first.

## Multiplayer Extensions (Required)

Lobby features (solo-style environment, lobby visuals, gameplay tweaks) come from **[Multiplayer Extensions](https://github.com/EnderdracheLP/MultiplayerExtensions)** ([v1.1.0 for BS 1.37.5–1.40.8](https://github.com/EnderdracheLP/MultiplayerExtensions/releases/tag/v1.1.0)). It is **not** a hard BSIPA dependency of Multiplayer Chat so the mod can start without it.

- **Auto-install:** If `MultiplayerExtensions.dll` is not next to `MultiplayerChat.dll` in `Plugins`, Multiplayer Chat downloads the official release zip, extracts `MultiplayerExtensions.dll` into that folder, then closes Beat Saber. Launch the game again so BSIPA loads Multiplayer Extensions.
- **Manual:** Download the same release and place `MultiplayerExtensions.dll` beside `MultiplayerChat.dll`.

## Requirements

- Beat Saber 1.40.X (all 1.40 versions)
- [MultiplayerExtensions](https://github.com/EnderdracheLP/MultiplayerExtensions) 1.1.0+ (does get automatically installed if missing)
- [BSIPA](https://beatmods.com) 4.2.0+
- [BeatSaberMarkupLanguage](https://beatmods.com) 1.6.0+
- [SiraUtil](https://beatmods.com) 3.0.0+
- [MultiplayerCore](https://github.com/Goobwabber/MultiplayerCore) 1.6.0+
- [BeatTogether](https://github.com/BeatTogether/BeatTogether) (or compatible server)

_BSIPA `dependsOn` omits Multiplayer Extensions on purpose — use the **Multiplayer Extensions** section above for auto or manual install._

## SLZ companion mode (optional)

Multiplayer Chat can treat your install as an **SLZ companion** client when a marker file **`SLZ.dat`** exists **in the same folder as `MultiplayerChat.dll`** — usually **`Beat Saber\Plugins`**. The file can be empty; presence is what matters.

- **Create:** Run `Tools\SlzMarkerTool.exe` from the release zip with your Plugins path, for example:  
  `SlzMarkerTool.exe "D:\Steam\steamapps\common\Beat Saber\Plugins"`  
  Or create `SLZ.dat` manually in that folder.
- **Remove:** `SlzMarkerTool.exe --remove "...\Plugins"`
- **Build from source:** `dotnet build SlzMarkerTool\SlzMarkerTool.csproj -c Release` → `SlzMarkerTool\bin\Release\SlzMarkerTool.exe`

Restart the game after adding or removing the marker.

## Installation

1. Install the dependencies above (Mod Assistant recommended). Multiplayer Extensions is handled automatically when missing (bootstrap then quit; relaunch once) or installed manually beside `MultiplayerChat.dll`.
2. Download the latest release or build from source.
3. Place `MultiplayerChat.dll` in your Beat Saber `Plugins` folder. [YOUR BEATSABER INSTALL FOLDER]\Plugins

## Building

1. Install [.NET SDK](https://dotnet.microsoft.com/download) 6.0 or later.
2. Edit `MultiplayerChat.csproj` and set `BeatSaberDir` to your Beat Saber install path.
3. Run: `dotnet build -c Release`
4. Copy `bin/Release/MultiplayerChat.dll` to `Beat Saber/Plugins/`

## How It Works

**Text:** Messages are encrypted before sending; the server forwards ciphertext only.

**Voice:** Voice messages and hot-mic chunks use the same session key material as chat where applicable; clips are built and played through Unity `AudioSource` scheduling for hot mic to avoid gaps between chunks.

**UI:** Chat appears in the mod’s lobby tab with stacked bubbles; settings and player actions live alongside the multiplayer flow.

## License

MIT
