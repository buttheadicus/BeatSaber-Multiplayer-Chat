# Multiplayer Chat

Encrypted text/voice chat for BeatTogether (or other supported server (in theory, any server))

## Already implemented features

- **Encrypted text/voice chat**: fully encrypted packets, server cant read what you say or type.
- **Voice chat & messages**: speak to other players with hot mic, or give them a nice voice message!
- **DMs**: self explanitory, have only one player read/hear your text/voice message.
- **Mute / deafen**: self explanitory
- **Talk-to / listen filters**: listen to one player, or talk to one player! (very useful in groups)
- **Custom avatars**: (THIS IS AN OPTIONAL ADDON, GO TO THE SETTINGS TO ENABLE THIS FEATURE) use custom unity avatars in multiplayer rather then the default Beat Saber avatars!

## Fused mods
- [AvatarExtras](https://github.com/roydejong/BeatSaberAvatarExtras)

## Addons [GO TO SETTINGS TO ENABLE THESE FEATURES]
- **Custom Multiplayer Avatars**
- **Avatar Coloring Extentions**

## Plans

- **Friend service**: ChatID based friends beyond the current lobby list.
- **Join friends**: Connect to a friend's session from the menu or a friends list.
- **Private calls**: Out-of-lobby or dedicated voice sessions (design is TBD).
- **DM from menu**: Start or continue DMs without being in a multiplayer lobby first, basically just discord but ingame.

## Multiplayer Extensions (Required (but also auto installs, see below))

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

_BSIPA `dependsOn` omits Multiplayer Extensions on purpose - use the **Multiplayer Extensions** section above for auto or manual install._

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
