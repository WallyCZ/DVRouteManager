# Route Manager (Overhauled)

A mod for [Derail Valley](https://store.steampowered.com/app/588030/Derail_Valley/) that adds route planning, automatic junction switching, cruise control, and locomotive AI driving.

> **Forked from [WallyCZ/DVRouteManager](https://github.com/WallyCZ/DVRouteManager)** — original mod by Wally.
> This fork is in **active development**, updating the mod to work with the latest version of Derail Valley.

---

## Features

- **Route planning** — calculate a path between any two tracks using A* pathfinding
- **Automatic junction switching** — switches are set as you drive along the planned route
- **Comms Radio integration** — set routes, check status, and control cruise control via the in-cab radio
- **Cruise control** — set a target speed and let the mod hold it
- **Locomotive AI** — fully automatic driving along a planned route
- **Map markers** — route is drawn on the in-game map

## Requirements

- [Unity Mod Manager](https://www.nexusmods.com/site/mods/21)
- [CommsRadioAPI](https://www.nexusmods.com/derailvalley/mods/740)

## Installation

1. Install Unity Mod Manager and CommsRadioAPI first
2. Drop `DVRouteManager.dll` and `Priority Queue.dll` into `Derail Valley/Mods/DVRouteManager/`
3. Launch the game — the mod will appear in the UMM overlay (F10)

## Usage

Open the Comms Radio in-cab and cycle to **Route Manager**. From there you can:
- Set a route from your locomotive to a job destination or a specific track
- View active route info and clear it
- Toggle cruise control and adjust speed
- Start the locomotive AI

## Status

| Area | Status |
|------|--------|
| Build | ✅ Compiles |
| Comms Radio UI | ✅ Working |
| Route pathfinding | 🧪 Testing |
| Junction switching | 🧪 Testing |
| Cruise control | 🧪 Testing |
| Locomotive AI | 🧪 Testing |
| Map markers | 🧪 Testing |

## Original Mod

Original mod by **WallyCZ**: https://github.com/WallyCZ/DVRouteManager
Nexus Mods page: https://www.nexusmods.com/derailvalley/mods/157
