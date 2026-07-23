# DVRouteManager

A [Derail Valley](https://store.steampowered.com/app/588030/Derail_Valley/) mod that adds route management, automatic junction switching, and cruise control via the Comms Radio.

> **Forked from [WallyCZ/DVRouteManager](https://github.com/WallyCZ/DVRouteManager)** — original mod by Wally.

> **Note:** The autonomous AI driver is under active development on the [`Debug` branch](https://github.com/odwales20/DVRouteManager/tree/Debug) and is not part of this stable release.

---

## Features

### Route Planning & Navigation
- **A\* pathfinding** over the full RailTrack graph — finds the shortest driveable path between any two tracks
- **Automatic junction switching** — all switches along the route are set as you pass each track segment
- **Turntable routing** — routes through turntables, auto-rotates them to the correct heading before you arrive
- **Yard track avoidance** — penalises occupied or reserved yard sidings so the pathfinder prefers main-line paths
- **Flip direction** — reverse the active route in-place without re-planning from scratch
- **Map markers** — route is drawn on the in-game map

### Cruise Control
- **PID speed controller** — smooth throttle/brake to hold a target speed
- **DM3 automatic gear shifting** — shifts up at >800 RPM, down at <600 RPM, with a hard 70 km/h cap
- **Steam loco support (S060 / S282)** — pressure-aware cutoff, pulse braking, smooth regulator/cutoff lerp
- **Overheat protection** — throttle backed off at Warning temperature, forced down at Critical

### Comms Radio UI
Full menu tree accessible from the in-game Comms Radio:
- **New Route** — pick destination, plan route
- **Active Route** — view info, flip direction, or clear
- **Settings** — adjust behaviour

---

## Requirements

- [Derail Valley](https://store.steampowered.com/app/588030/Derail_Valley/) (current build)
- [Unity Mod Manager (UMM)](https://www.nexusmods.com/site/mods/21)
- [CommsRadioAPI](https://www.nexusmods.com/derailvalley/mods/813) — place `CommsRadioAPI.dll` in `Mods\CommsRadioAPI\`

---

## Installation

1. Install Unity Mod Manager and point it at your Derail Valley folder.
2. Install CommsRadioAPI via UMM or manually.
3. Copy the `DVRouteManager` folder (containing `DVRouteManager.dll` and `info.json`) into your `Mods\` directory:
   ```
   Derail Valley\Mods\DVRouteManager\
   ```
4. Launch the game — the mod loads automatically via UMM.

---

## Building from Source

**Requirements:** Visual Studio 2022 (or MSBuild), .NET Framework 4.7.2

1. Clone the repo.
2. Set `DVInstallPath` in `DVDRouteManager.csproj` to your Derail Valley install.
3. Build:
   ```
   MSBuild DVRouteManager\DVDRouteManager.csproj /p:Configuration=Debug
   ```
   The DLL is output to `bin\Debug\DVRouteManager.dll` and copied to your Mods folder automatically.

> **Note:** The post-build copy step may report `MSB3073` (SolutionName undefined) when building outside the full solution — this is harmless; the DLL is still built successfully.

---

## How It Works

| Component | Role |
|-----------|------|
| `PathFinder.cs` | A\* search over `RailTrack` graph with turntable and yard-penalty support |
| `Route.cs` | Wraps the path list; manages junction/turntable switching and direction reversal |
| `RouteTracker.cs` | Tracks loco position along the active route; triggers switch updates as segments are passed |
| `LocoCruiseControl.cs` | PID controller with DM3 gear logic and steam loco support |
| `RouteManagerStates.cs` | All Comms Radio UI states and menu tree |

---

## Known Issues

- **Audio cues** — clips load and `AudioSource` is found, but cues do not reliably play in-game

---

## Branches

| Branch | Purpose |
|--------|---------|
| `master` | Stable release — route planning, junction switching, cruise control |
| `Debug` | Active development — autonomous AI driver (speed limit system in progress) |

---

## Credits

- [WallyCZ/DVRouteManager](https://github.com/WallyCZ/DVRouteManager) — original mod
- [RouteSetter](https://github.com/zelmer69/RouteSetter) by zelmer69 — reference for modern CommsRadioAPI usage
- Derail Valley by [Altfuture](https://altfuture.gg/)
