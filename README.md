# Blueprints

![version](https://img.shields.io/badge/version-0.1.0-blue) ![valheim](https://img.shields.io/badge/Valheim-1.0.21-orange) ![client](https://img.shields.io/badge/client--side-only-green)

A BepInEx plugin for Valheim.

Build something once, then place copies of it in one go. Mark the pieces of a build with the hammer, save them as a named blueprint, and pick that blueprint from the hammer menu to place the whole thing.

## How to use
1. Take out the **Hammer** and open the build menu. There's a new **Blueprints** tab.
2. Choose **Blueprint selection** (it works like the Repair tool).
   - **Click** a built piece to mark or unmark it. Marked pieces are tinted blue.
   - **Shift + click** marks every piece connected to the one you click.
   - **Backspace** clears all marks.
3. Press **B** and type a name to save the marked pieces as a blueprint.
4. Your blueprints appear in the **Blueprints** tab. Choose one to see a ghost of the whole build, rotate it as normal, and click to place it.
   - With a blueprint chosen, **B** renames it and **Delete** (twice) deletes it.

Placing a blueprint costs the full materials of every piece in it, and needs every crafting station those pieces need (for example a workbench and a stonecutter) in range, just like building them one by one. No-cost mode is respected.

The blueprint's facing comes from the first piece you marked, and it sits on the ground by its lowest point.

## Limits
- Only the pieces are copied, not what's in or on them (chest contents, sign text, item stands).
- Pieces are placed all at once, so a blueprint placed in mid-air will lose support and collapse, as it would in vanilla.

## Configuration (`BepInEx/config/jordan.valheim.blueprints.cfg`)
| Section | Key | Default | What it does |
|---|---|---|---|
| Keys | SaveOrRename | B | Save marked pieces (selection tool) / rename (blueprint chosen). |
| Keys | ClearSelection | Backspace | Unmark every piece. |
| Keys | DeleteBlueprint | Delete | Press twice to delete the chosen blueprint. |
| Keys | SelectConnectedModifier | LeftShift | Hold while clicking to mark all connected pieces. |
| Selection | MaxConnected | 500 | Most pieces one connected click adds. |
| Selection | HighlightColor | light blue | Tint for marked pieces. |

Blueprints are saved as JSON files in `BepInEx/config/Blueprints/`, so you can back them up or share them.

## Install

### Easiest: Valheim Mod Installer
Download the [**Valheim Mod Installer**](https://github.com/Glutoblop/ValheimPlugins) and run it (`Start-Installer.bat` on Windows, `start-installer.sh` on Linux/macOS), point it at your Valheim folder, tick **Blueprints** and click **Install / Update**. It installs BepInEx for you if needed.

### Manual
Download from the [latest release](https://github.com/Glutoblop/Blueprints/releases/latest) ([`Blueprints.zip`](https://github.com/Glutoblop/Blueprints/releases/latest/download/Blueprints.zip): unzip into your Valheim folder), or:

1. Install [BepInExPack_Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) (5.4.2360 or newer) into your Valheim folder.
2. Download [`dist/Blueprints.dll`](https://github.com/Glutoblop/Blueprints/raw/main/dist/Blueprints.dll).
3. Copy it to `<Valheim>/BepInEx/plugins/Blueprints/Blueprints.dll`.
4. Launch the game once; settings are written to `BepInEx/config/jordan.valheim.blueprints.cfg`.

## Compatibility

| | |
|---|---|
| Plugin version | **0.1.0** |
| Last tested on Valheim | **1.0.21** (network version 41, Steam build 26249040) on 2026-11-07 |
| BepInExPack_Valheim | 5.4.2360 |
| Multiplayer | Client-side only: works on vanilla servers, other players don't need it. Placed pieces are ordinary vanilla pieces. |

Machine-readable versions live in [`plugin.json`](plugin.json). The installer reads it straight from `main` to tell users when an update is available, or when the mod hasn't been tested against their game version.

## Building

Requires the .NET SDK (8+) and a Valheim install with BepInEx.

```
dotnet build -c Release -p:GameDir="C:\Path\To\Valheim"
```

The build copies the DLL into `<GameDir>/BepInEx/plugins/Blueprints` for testing, and into `dist/` (which is committed, and is what the installer downloads).

## Releasing an update

Run `release\Release.bat` (or `powershell -File release\release.ps1 [-Bump patch|minor|major] [-Changes "text"]`). It checks the repo is pushed, bumps the version in `plugin.json` and `src/Plugin.cs`, rebuilds, commits, pushes and publishes the GitHub release with the zip. Use `-BuildOnly` to just build the package.
