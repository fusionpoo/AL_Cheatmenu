# AL_Cheatmenu — in-game cheat window for Amanatsu Location

BepInEx 6 IL2CPP plugin for **Amanatsu Location** (ILLGAMES). Press **F7** in game.

**[Download the latest release](https://github.com/fusionpoo/AL_Cheatmenu/releases/latest)** ·
**[Documentation](https://fusionpoo.github.io/AL_Cheatmenu/)**

## Features
- **Heroines** — per heroine in the loaded save: Favorability / Inclusiveness / Proactivity / Curiosity
  level −/+, Max, add points, reset (levels and points come from the game's own tables); late points,
  energy, H and massage counts, H gauge stages. "Max everything" and "refill energy" for all.
- **Time & flags** — set the time of day or advance one zone; tutorial done/reset; help-seen and unlock flags.
- Built on the game's own UI system (uGUI + TextMeshPro); unlocks the mouse while open; refreshes live.

Everything edits the live save in memory — **save the game afterwards** to keep changes.

## Install
1. Install **BepInEx 6 IL2CPP** (tested with 6.0.0-be.788) and run the game once.
2. Merge the release zip's `BepInEx` folder into the game folder (`BepInEx\plugins\AL_Cheatmenu\AL_Cheatmenu.dll`).
3. Press F7 in game. Key and window scale: `BepInEx\config\fusionpoo.al.cheatmenu.cfg`.

Uninstall: delete `BepInEx\plugins\AL_Cheatmenu`.

## Building
`dotnet build -c Release -p:GameDir="D:\Games\AmanatsuLocation" src/AL_Cheatmenu.csproj`, or `GAME_DIR=... ./build_plugin.sh`.
The technical notes (why IMGUI can't be used in this game, which save classes are edited) are on the Pages site.

## License
MIT.
