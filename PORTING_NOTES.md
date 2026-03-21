# DVRouteManager - Porting Notes (Old DV → New DV)

## Status Legend
- [ ] TODO
- [~] IN PROGRESS
- [x] DONE

---

## Current Status: **BUILD SUCCESSFUL** ✓
The project compiles with zero C# errors as of 2026-03-20.
DLL deployed to: `D:\SteamLibrary\steamapps\common\Derail Valley\Mods\DVRouteManager\DVRouteManager.dll`

---

## Phase 1: Project File & References
- [x] Fix .csproj DLL paths (C:\Program Files (x86)\Steam → D:\SteamLibrary)
- [x] Add CommsRadioAPI.dll reference
- [x] Keep OptimizedPriorityQueue NuGet (downloaded 4.2.0 manually to packages/)
- [x] Remove DV.Teleporters reference (removed from game)
- [x] Add DV.RailTrack.dll reference
- [x] Add WorldStreamer.dll reference (contains WorldMover class)

## Phase 2: API Breaking Changes (Source Files)

### RailTrack API
- [x] `RailTrack.logicTrack.xxx` → `RailTrack.LogicTrack().xxx` — ALL files
  - Fixed: PathFinder.cs, RailTrackExtension.cs, Route.cs, RouteTracker.cs, RouteCommand.cs, LocoAI.cs, PathMapMarkers.cs
- [x] `RailTrackRegistry.Instance.AllTracks` → `RailTrackRegistryBase.RailTracks`
  - Fixed: PathFinder.cs, RouteCommand.cs
- [x] `onTrackBogies` → `.BogiesOnTrack()` extension method
  - Fixed: RailTrackExtension.cs, RouteTracker.cs
- [x] `SingletonBehaviour<IdGenerator>.Instance.logicCarToTrainCar` → `SingletonBehaviour<TrainCarRegistry>.Instance.logicCarToTrainCar`
  - Fixed: RouteTask.cs

### Module.cs
- [x] Keep `www.isHttpError` / `www.isNetworkError` (DV's Unity version still uses old API)
- [x] `controlsOverrider.EngineOnReader` check removed — fails naturally if engine is off
- [x] Removed unused `using DV.Teleporters;`, `using DV.Simulation.Cars;`

### RouteTracker.cs
- [x] `Mathd.Clamp01` → `Mathf.Clamp01` (in DEBUG block, line 360)

### PathMapMarkers.cs
- [x] `WorldMap` removed from DV — removed usage, use `mapController.transform` as parent
- [x] `mapController.shopMarkerPrefab` now private — access via FieldInfo reflection
- [x] `GetMapPosition(Vector3, Vector2)` → `GetMapPosition(Vector3 absPosition, bool dynamic)` via reflection
- [x] `WorldMover` is in WorldStreamer.dll (global namespace) — added assembly reference
- [x] Coordinate fix: `pointPos + WorldMover.currentMove` = absolute position for new API

### LocoCruiseControl.cs
- [x] `LocoCruiseControl.IsActive` → `LocoCruiseControl.IsSet`
- [x] `TargetSpeed` (static) → `LocoCruiseControl.GetTargetSpeed()` (returns float?)

## Phase 3: CommsRadio Rewrite (MAJOR)
- [x] Removed all 15 old CommsRadio page files from .csproj
- [x] Created new `CommsRadio\RouteManagerStates.cs` using AStateBehaviour pattern
- [x] `LCDArrowState.Both` doesn't exist → replaced with `LCDArrowState.Right`
- [x] `Utils.CycleEnum` → `Utils.NextEnumItem()` (returns object, needs cast)
- [x] `LocoAI.StartAI()` takes `RouteTracker` argument

### States implemented in RouteManagerStates.cs:
- RouteManagerInitialState (entry, ButtonBehaviourType.Regular required)
- RouteManagerMainMenuState (scrollable: New route / Active route / Cruise Control / Loco AI / Settings)
- RouteManagerNewRouteState (Loco→job, Loco→track, job cars→job)
- RouteManagerSelectJobState (lists JobBooklet.allExistingJobBooklets)
- RouteManagerSelectTrackState (lists all named tracks from RailTrackRegistryBase.RailTracks)
- RouteManagerComputingState (async pathfinding via Module.StartCoroutine + OnUpdate polling)
- RouteManagerMessageState (display result/error)
- RouteManagerRouteInfoState (shows route info, clear option)
- RouteManagerCruiseControlState (toggle/adjust cruise control)
- RouteManagerLocoAIState (start LocoAI)
- RouteManagerSettingsState (cycle ReversingStrategy)

## Phase 4: Module.cs CommsRadio Registration
- [x] Replaced old reflection-based Harmony patch with CommsRadioAPI
- [x] `CommsRadioMode.Create(new RouteManagerInitialState(), new Color(0.5f, 0.5f, 0.5f))`
- [x] RemoveCommsRouteManager() is no-op (CommsRadioAPI doesn't support runtime removal)

## Phase 5: Info.json & Build
- [ ] Update Info.json version to 0.4.0
- [ ] Add CommsRadioAPI dependency to Info.json (if supported by UMM)
- [x] Build succeeded with zero C# errors

---

## Key API Reference

### DV API Changes (old → new)
- `RailTrack.logicTrack` → `RailTrack.LogicTrack()` extension method (DV.RailTrack.dll)
- `Track.RailTrack()` extension method to go the other way
- `RailTrackRegistry.Instance.AllTracks` → `RailTrackRegistryBase.RailTracks`
- `RailTrack.onTrackBogies` (private field) → `RailTrack.BogiesOnTrack()` extension method
- `IdGenerator.logicCarToTrainCar` → `TrainCarRegistry.logicCarToTrainCar`
- `WorldMap` → removed, use `MapMarkersController` directly
- `MapMarkersController.shopMarkerPrefab` → now private `[SerializeField]`, access via reflection
- `GetMapPosition(Vector3, Vector2)` → `GetMapPosition(Vector3 absPosition, bool dynamic)` (still private, still reflection)
- `WorldMover` class → in `WorldStreamer.dll`, global namespace, `currentMove` is a static Vector3
- `DV.OriginShift.OriginShift.currentMove` is the new static version (both exist)
- `LocoCruiseControl.IsActive` → `LocoCruiseControl.IsSet`
- `Utils.CycleEnum<T>()` → `Utils.NextEnumItem(value)` returns `object` (cast needed)

### CommsRadioAPI Pattern
```csharp
// Registration (Module.cs):
CommsRadioMode.Create(new RouteManagerInitialState(), new Color(0.5f, 0.5f, 0.5f));

// State class:
internal class MyState : AStateBehaviour {
    public MyState() : base(new CommsRadioState(
        titleText: "Title",
        contentText: "Content",
        actionText: "Action",
        lcd: new LCDData(LCDArrowState.Right, 0),
        buttonBehaviour: ButtonBehaviourType.Regular)) { }

    public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action) {
        switch (action) {
            case InputAction.Activate: return new NextState();
            case InputAction.Up: return new MyState(index - 1);
            case InputAction.Down: return new MyState(index + 1);
            default: throw new ArgumentException();
        }
    }

    // Optional: for auto-transitions (polling)
    public override AStateBehaviour OnUpdate(CommsRadioUtility utility) {
        if (someCondition) return new NextState();
        return null; // stay in current state
    }
}
```

### Build Command
```
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "D:\Documents\DVRouteManager\DVRouteManager\DVDRouteManager.csproj"
```
(PostBuildEvent will fail with $(SolutionName) undefined — ignore, DLL is still built to bin\Debug\)
