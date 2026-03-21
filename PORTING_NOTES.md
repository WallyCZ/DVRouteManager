# DVRouteManager - Porting Notes (Old DV → New DV)

## Status Legend
- [ ] TODO
- [~] IN PROGRESS
- [x] DONE

---

## Current Status: **LOADING IN-GAME** ✓
As of 2026-03-21:
- Mod loads successfully (no ReflectionTypeLoadException)
- CommsRadio mode appears and is navigable
- Route computing throws "promise-style task" error → FIXED (removed `.Start()` call)
- Next session: test full route set → switch → drive flow in-game

### Known remaining issues to test/fix:
- [ ] Route computing result — does pathfinding actually work end-to-end?
- [ ] Junction switching — does it correctly switch junctions along the route?
- [ ] RouteTracker — does it track progress and detect arrival correctly?
- [ ] LocoAI — untested in new DV
- [ ] PathMapMarkers — map dot positions may be wrong (coordinate space guessed)
- [ ] Audio clips — not tested (stoptrain, trainend, wrongway etc.)

---

## Deploy Steps (each session)
```
1. Build:
   "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "D:\Documents\DVRouteManager\DVRouteManager\DVDRouteManager.csproj"
   (PostBuildEvent fails — ignore, DLL is in bin\Debug\)

2. Copy:
   DVRouteManager\bin\Debug\DVRouteManager.dll  →  DV\Mods\DVRouteManager\
   DVRouteManager\bin\Debug\Priority Queue.dll  →  DV\Mods\DVRouteManager\   ← required!
```

---

## Phase 1: Project File & References ✓
- [x] Fix .csproj DLL paths (C:\Program Files (x86)\Steam → D:\SteamLibrary)
- [x] Add CommsRadioAPI.dll reference
- [x] Keep OptimizedPriorityQueue NuGet (downloaded 4.2.0 manually to packages/)
- [x] Remove DV.Teleporters reference (removed from game)
- [x] Add DV.RailTrack.dll reference
- [x] Add WorldStreamer.dll reference (contains WorldMover class)
- [x] `Priority Queue.dll` must be copied to Mods\DVRouteManager\ alongside the main DLL

## Phase 2: API Breaking Changes ✓
- [x] `RailTrack.logicTrack.xxx` → `RailTrack.LogicTrack().xxx` (all files)
- [x] `RailTrackRegistry.Instance.AllTracks` → `RailTrackRegistryBase.RailTracks`
- [x] `onTrackBogies` → `.BogiesOnTrack()` extension method
- [x] `SingletonBehaviour<IdGenerator>` → `TrainCarRegistry.Instance` (direct subclass access)
- [x] `AppUtil.IsPaused` → `AppUtil.Instance.IsTimePaused`
- [x] `Mathd.Clamp01` → `Mathf.Clamp01`
- [x] `WorldMap` removed — use `MapMarkersController` directly, `mapController.transform` as parent
- [x] `MapMarkersController.shopMarkerPrefab` private → FieldInfo reflection
- [x] `GetMapPosition(Vector3, Vector2)` → `GetMapPosition(Vector3, bool)` via reflection
- [x] `LocoCruiseControl.IsActive` → `LocoCruiseControl.IsSet`
- [x] `Utils.CycleEnum<T>()` → `(T)Utils.NextEnumItem(value)`
- [x] `using DV.Simulation.Cars` needed for SimController in Module.cs

## Phase 3: CommsRadio Rewrite ✓
- [x] Replaced 15 old page files with single `CommsRadio\RouteManagerStates.cs`
- [x] New pattern: `AStateBehaviour` subclasses, registered via `CommsRadioMode.Create()`
- [x] Registration waits for `CommsRadioController` to exist (coroutine, like RouteSetter)
- [x] `task.Start()` removed — `DoCommand` is async (hot task, already running)
- [x] `LCDArrowState.Both` → `LCDArrowState.Right`
- [x] `LocoAI.StartAI()` takes `RouteTracker` argument

### States in RouteManagerStates.cs:
- `RouteManagerInitialState` — entry point
- `RouteManagerMainMenuState` — New route / Active route / Cruise Control / Loco AI / Settings
- `RouteManagerNewRouteState` — Loco→job / Loco→track / Job cars→job
- `RouteManagerSelectJobState` — lists job booklets
- `RouteManagerSelectTrackState` — lists named tracks
- `RouteManagerComputingState` — async pathfinding, polls via OnUpdate()
- `RouteManagerMessageState` — result/error display
- `RouteManagerRouteInfoState` — active route info + clear
- `RouteManagerCruiseControlState` — toggle/adjust cruise
- `RouteManagerLocoAIState` — start auto-drive
- `RouteManagerSettingsState` — cycle ReversingStrategy

## Phase 4: Info.json
- [x] `Requirements: ["CommsRadioAPI"]` added (ensures correct load order)
- [x] `DisplayName: "Route Manager (Overhauled)"`
- [ ] Bump version to 0.4.0 when ready to release

---

## Key API Quick Reference

| Old | New |
|-----|-----|
| `RailTrack.logicTrack` | `RailTrack.LogicTrack()` |
| `RailTrackRegistry.Instance.AllTracks` | `RailTrackRegistryBase.RailTracks` |
| `RailTrack.onTrackBogies` | `RailTrack.BogiesOnTrack()` |
| `SingletonBehaviour<IdGenerator>.Instance` | `TrainCarRegistry.Instance` |
| `AppUtil.IsPaused` | `AppUtil.Instance.IsTimePaused` |
| `WorldMap` (removed) | use `MapMarkersController` |
| `LocoCruiseControl.IsActive` | `LocoCruiseControl.IsSet` |
| `Utils.CycleEnum<T>()` | `(T)Utils.NextEnumItem(value)` |
| `task.Start()` on async task | don't call Start() |

**WorldMover**: in `WorldStreamer.dll`, global namespace. `WorldMover.currentMove` still works.
**SingletonBehaviour**: in `DV.Utils.dll`. Access via subclass directly (e.g. `AppUtil.Instance`).
**DV.OriginShift.OriginShift.currentMove** also exists as a static equivalent.
