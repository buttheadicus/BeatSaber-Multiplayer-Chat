# Multiplayer Chat

Encrypted text/voice chat for BeatTogether (or other supported server (in theory, any server. doesnt include multiplayer+ currently))

## Already implemented features

<details>
  <summary>Voice chat / text chat</summary><br/>
	chat with players with a text chat, or use voice chat-you do you. (note, packets are encrypted, beattogether or whatever server you use can NOT read what you say/type.
</details>

<details>
  <summary>DMs</summary><br/>
	self explanitory, have only one player read/hear your text/voice message. to be expanded in 0.4.0's major server update.
</details>

<details>
  <summary>Mute / deafen</summary><br/>
	self explanitory
</details>

<details>
  <summary>Talk-to / listen filters</summary><br/>
	listen to one player, or talk to one player! (very useful in groups)
</details>

## Fused mods
- [AvatarExtras](https://github.com/roydejong/BeatSaberAvatarExtras) 

## Addons [GO TO SETTINGS TO ENABLE THESE FEATURES]

<details>
  <summary>Custom Multiplayer Avatars</summary><br/>
	This addon allows custom avatars to be used in multiplayer-players with this addon enabled can see your avatar and vice versa. 
	**does not require the Custom Avatars mod as of 0.3.8**
</details>

<details>
  <summary>Avatar Coloring Extentions</summary><br/>
	This addon gives you extra color settings that allow you to create exotic colors and effects that **ANYBODY** can see in multiplayer.
</details>

<details>
  <summary>Quick Binds</summary><br/>
	This addon gives you the ability to bind game events to your controllers
	current bindable events; 
</details>


## Plans

- **Friend service**: ChatID based friends beyond the current lobby list.
- **Join friends**: Connect to a friend's session from the menu or a friends list.
- **Private calls**: Out-of-lobby or dedicated voice sessions (design is TBD).
- **DM from menu**: Start or continue DMs without being in a multiplayer lobby first, basically just discord but ingame.

## Dependency auto installs

yeah, it does. soo it will close beatsaber a few times after the first install, please know that its normal. its not crashing, its installing everything you need.

## Requirements

- Beat Saber 1.40.X (all 1.40 versions)
- [MultiplayerExtensions](https://github.com/EnderdracheLP/MultiplayerExtensions) 1.1.0+ (does get automatically installed if missing)
- [BSIPA](https://beatmods.com) 4.2.0+
- [BeatSaberMarkupLanguage](https://beatmods.com) 1.6.0+
- [SiraUtil](https://beatmods.com) 3.0.0+
- [MultiplayerCore](https://github.com/Goobwabber/MultiplayerCore) 1.6.0+
- [BeatTogether](https://github.com/BeatTogether/BeatTogether) (or compatible server)

## Installation

1. Install the dependencies above (or, just install the DLL to your game and it will install the dependencies).
2. Download the latest release or build from source (why would you build from source, you do you).
3. Place `MultiplayerChat.dll` in your Beat Saber `Plugins` folder. [YOUR BEATSABER INSTALL FOLDER]\Plugins

## Building

1. Install [.NET SDK](https://dotnet.microsoft.com/download) 6.0 or later.
2. Edit `MultiplayerChat.csproj` and set `BeatSaberDir` to your Beat Saber install path.
3. Run: `dotnet build -c Release`
4. Copy `bin/Release/MultiplayerChat.dll` to `Beat Saber/Plugins/`
5. Piss yourself when build fails
6. Restart step 1

## License

MultiplayerChat is dedicated to the public domain under [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/). See [LICENSE](LICENSE).
