# Route Manager (Overhauled)

A mod for [Derail Valley](https://store.steampowered.com/app/588030/Derail_Valley/) that adds route planning, automatic junction switching, cruise control, and locomotive AI driving.

> **Forked from [WallyCZ/DVRouteManager](https://github.com/WallyCZ/DVRouteManager)** — original mod by Wally.
> This fork is in **active development**, updating the mod to work with the latest version of Derail Valley.

---

## Features

- **Route planning** — calculate a path between any two tracks using A* pathfinding
- **Automatic junction switching** — switches are set as you drive along the planned route
- **Comms Radio integration** — set routes, check status, and control the AI via the in-cab radio
- **Cruise control** — PID-based speed controller; supports DM3 automatic gear shifting and overheating protection
- **Locomotive AI** — fully automatic driving along a planned route, including reversing at junctions; drive to any destination with or without a job
- **Freight haul AI** — fully automated multi-phase freight operations (route to cars → couple → release handbrakes → drive to destination → uncouple → apply handbrakes)
- **DM3 speed cap** — AI limits the DM3 to 70 km/h to protect the drivetrain
- **Map markers** — route is drawn on the in-game map

---

## Requirements

- [Unity Mod Manager](https://www.nexusmods.com/site/mods/21)
- [CommsRadioAPI](https://www.nexusmods.com/derailvalley/mods/740)

---

## Installation

1. Install Unity Mod Manager and CommsRadioAPI first
2. Drop `DVRouteManager.dll` and `Priority Queue.dll` into `Derail Valley/Mods/DVRouteManager/`
3. Launch the game — the mod will appear in the UMM overlay (F10)

---

## Usage

Open the Comms Radio in-cab and cycle to **Route Manager**. From there you can:

- **New route** — set a route from your locomotive to a job destination or a specific track
- **Active route** — view current route info, distance remaining, and clear it
- **Loco AI** — start the locomotive AI driver or launch a full freight haul
  - *Drive to destination*: pick any track from the list — AI drives there with no job required; sets active route and adjusts junctions
  - *Freight haul*: if you have multiple jobs or multiple destinations, you will be prompted to pick which to do first; the AI then handles all phases automatically
  - *Stop AI*: shown while AI is running — cancels the current operation cleanly
- **Settings** — configure mod behaviour via UMM (F10)

### Comms Radio: Freight Haul Flow

1. Select **Loco AI → Freight haul**
2. Pick a job (if more than one) and a destination (if more than one)
3. AI drives loco to the freight cars, couples up, releases handbrakes, drives to the destination, uncouples, and applies handbrakes
4. Use **Stop AI** at any point to abort

---

## Status

| Area | Status |
|------|--------|
| Build | ✅ Compiles |
| Comms Radio UI | ✅ Working |
| Route pathfinding | ✅ Working |
| Junction switching | ✅ Working |
| Route tracking | ✅ Working |
| Turntable routing | 🧪 Testing |
| Sign-based speed limits | 🧪 Testing |
| Cruise control | 🧪 Testing |
| Locomotive AI | 🧪 Testing |
| Freight haul AI | 🧪 Testing |
| Map markers | ✅ Working |
| Audio cues | ❌ Not working — clips load but do not play in-game |

---

## Changelog

### 0.5.0 (current — in development)

**Turntable support**
- Pathfinder now routes through turntables — the A* graph includes turntable tracks as nodes connecting all spur tracks on their rim; tracks reachable via turntable rotation are treated as direct (no reverse penalty)
- `Route.AdjustSwitches()` automatically rotates a turntable to the correct entry angle when the route passes through it
- Locomotive AI waits (`TargetSpeed = 0`) while an upcoming turntable is still rotating
- Player-controlled routing: `RouteTracker` detects an upcoming rotating turntable 1–2 tracks ahead, plays a stop-train audio cue, and shows "WAIT: turntable rotating" on the Comms Radio display; plays a go cue when rotation finishes

**Speed limits from in-game signs**
- Replaced geometry-based speed estimation with direct sign reading
- At startup, all `SignGeneratorData` MonoBehaviours are scanned; each speed-limit sign is mapped to the nearest `RailTrack` within 30 m
- AI now drives at exactly the speed the in-game signs display — 40 where it says 40, 120 on open main line
- Falls back to 120 km/h (no restriction) on tracks with no nearby sign

**Comms Radio: Active Route improvements**
- Added **Flip direction** button — regenerates the route in the opposite heading (even if longer); uses `FindOppositeRoute()` which bans the current first→second track transition to force the pathfinder the other way
- Shows "WAIT: turntable rotating" in route info when the tracker detects an upcoming rotating turntable

**Other**
- Routing to your current location now gives a clear "You're already there" error instead of crashing

---

### 0.4.0

Full overhaul to support the latest Derail Valley release, plus new AI features.

**Porting fixes**
- Updated all API calls for the latest DV version (`RailTrack.LogicTrack()` extension, `RailTrackRegistryBase.RailTracks`, `TrainCarRegistry.logicCarToTrainCar`, etc.)
- Replaced old reflection-based Comms Radio with the new `CommsRadioAPI` (`AStateBehaviour` pattern)
- Fixed `WorldMap` / `MapMarkersController` access via reflection for the new private API
- Added `WorldStreamer.dll` reference for `WorldMover`
- Fixed `LCDArrowState.Both` → `LCDArrowState.Right`
- Fixed `Utils.CycleEnum<T>` → `Utils.NextEnumItem(value)`

**Pathfinder**
- Fixed critical bug: `bannedTransitions.All()` → `.Any()` (transitions were never actually banned)
- Named yard sidings (storage, loading, in/out, parking tracks) now receive a large cost penalty when not the destination — the pathfinder strongly prefers unnamed connector tracks when routing through yards like OR and CMS, avoiding cars parked on those tracks

**Cruise control**
- PID controller refactored into `LocoCruiseControl` base class
- DM3 locomotive: automatic RPM-based gear shifting (shift up at >800 RPM, shift down at <600 RPM and dropping); throttle is zeroed and the mod waits for RPM < 750 before moving the gear lever, with a 3-second cooldown between shifts
- All locomotives: temperature monitoring — at Warning the throttle is capped at current level; at Critical throttle is forced down

**Locomotive AI (`LocoAI`)**
- Competing mod compatibility: on AI start, disables DriverAssist cruise control and SteamCruiseControl via reflection so they don't fight the AI
- New `StartFreightHaul` for fully automated freight operations
- New "Drive to destination" mode — AI drives to any selected track without a job booklet; sets active route and adjusts junctions
- DM3: AI enforces a 70 km/h speed cap to stay within the locomotive's safe operating range
- `StopAll()` cleanly aborts both AI driving and any active freight haul

**Freight haul AI (new)**
- Phase 1: pathfind and drive loco to freight cars
- Phase 2: auto-couple to cars; release handbrakes on all non-loco cars in the consist
- Phase 3: pathfind and drive consist to the job destination track
- Phase 4: uncouple loco from consist; apply handbrakes on freight cars
- Plays completion sound on success
- Abortable at any phase via Comms Radio "Stop AI"

**Bug fixes**
- Fixed `ArgumentNullException: Value cannot be null. Parameter name: key` crash when routing with no job or with passenger jobs (CS-A2LP etc.) — null branch tracks and null Car entries in task data are now guarded
- Fixed pathfinder null crash: branch track lists are filtered for null entries; `LogicTrack()` return is null-checked before use in A*

**Comms Radio UI**
- Main menu: New route / Active route / Loco AI / Settings / Back
- Loco AI menu: Freight haul / Drive to destination / Stop AI (when running) / Back
- Job picker state (when player has multiple active jobs)
- Destination picker state (when a job has multiple delivery tracks)
- Running state: shows distance to go, STOP button

---

### 0.3.1

- Fixed freezing on cruise control page

### 0.3.0

- Cruise control fixes
- Option to change route direction
- Settings page via UMM

### 0.2.0

- Route tracker
- Locomotive AI (initial)
- Comms Radio refactoring

### 0.1.0

- Initial release (original WallyCZ mod)

---

## Original Mod

Original mod by **WallyCZ**: https://github.com/WallyCZ/DVRouteManager
Nexus Mods page: https://www.nexusmods.com/derailvalley/mods/157
