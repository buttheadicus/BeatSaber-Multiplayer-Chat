# Multiplayer Chat

End-to-end encrypted text chat for BeatTogether multiplayer. Chat messages are encrypted on your device and can only be decrypted by other players in your lobby. No server, including BeatTogether, can read your messages.

## Features

- End-to-end encrypted – AES-256-CBC + HMAC. Only lobby members can read messages.
- Mod tab – "Multiplayer chat" tab in your mod tabs. (On your left).
- QWERTY keyboard – Full keyboard UI with letters, numbers, and symbols (`:;"'<,>.?/\|]}[{\`~!@#$%^&*()_+-=`).
- Stacked chat bubbles – All messages appear in one area above the lobby header (HOST SETUP / CLIENT SETUP / QUICK PLAY LOBBY), stacking vertically with newest at bottom.

## Plans

- Voice Chat

## Requirements

- Beat Saber 1.40.8
- [BSIPA](https://beatmods.com) 4.2.0+
- [BeatSaberMarkupLanguage](https://beatmods.com) 1.6.0+
- [SiraUtil](https://beatmods.com) 3.0.0+
- [MultiplayerCore](https://github.com/Goobwabber/MultiplayerCore) 1.6.0+
- [BeatTogether](https://github.com/BeatTogether/BeatTogether) (or compatible server)

## Installation

1. Install the dependencies above (Mod Assistant recommended).
2. Download the latest release or build from source.
3. Place `MultiplayerChat.dll` in your Beat Saber `Plugins` folder. [YOUR BEATSABER INSTALL FOLDER]\Plugins

## Building

1. Install [.NET SDK](https://dotnet.microsoft.com/download) 6.0 or later.
2. Edit `MultiplayerChat.csproj` and set `BeatSaberDir` to your Beat Saber install path.
3. Run: `dotnet build -c Release`
4. Copy `bin/Release/MultiplayerChat.dll` to `Beat Saber/Plugins/`

## How It Works

Encryption: A session key is derived from the sorted list of connected player IDs. Only players in the lobby can compute this key.
Packets: Messages are encrypted before sending. The BeatTogether server relays encrypted bytes and cannot decrypt them.
Chat bubbles: All messages appear stacked above the lobby header (HOST SETUP / QUICK PLAY LOBBY, etc.). New messages appear at the bottom of the stack; up to 8 messages are shown at once. Bubbles fade in/out over ~15 seconds.

## License

MIT
