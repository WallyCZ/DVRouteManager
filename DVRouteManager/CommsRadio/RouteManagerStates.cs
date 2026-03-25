using CommandTerminal;
using CommsRadioAPI;
using DV;
using DV.Logic.Job;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DVRouteManager.CommsRadio
{
    // ─────────────────────────────────────────────────────────────
    //  Initial / splash state  (must use ButtonBehaviourType.Regular)
    // ─────────────────────────────────────────────────────────────
    public class RouteManagerInitialState : AStateBehaviour
    {
        public RouteManagerInitialState()
            : base(new CommsRadioState(
                "ROUTE MANAGER",
                "v" + typeof(RouteManagerInitialState).Assembly.GetName().Version.ToString(3),
                "MENU"))
        { }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            if (action == InputAction.Activate)
                return new RouteManagerMainMenuState();
            return this;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Main menu
    // ─────────────────────────────────────────────────────────────
    public class RouteManagerMainMenuState : AStateBehaviour
    {
        private static readonly string[] MenuItems = new string[]
        {
            "New route",
            "Active route",
            "Loco AI",
            "Settings",
            "< Back"
        };

        private readonly int _index;

        public RouteManagerMainMenuState(int index = 0)
            : base(BuildState(index))
        {
            _index = index;
        }

        private static CommsRadioState BuildState(int index)
        {
            string item = MenuItems[Mathf.Clamp(index, 0, MenuItems.Length - 1)];
            // Dim "Active route" when no route is set
            if (item == "Active route" && !Module.ActiveRoute.IsSet)
                item = "(no active route)";
            return new CommsRadioState("ROUTE MANAGER", item, "SELECT",
                LCDArrowState.Right, LEDState.Off, ButtonBehaviourType.Override);
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            switch (action)
            {
                case InputAction.Up:
                    return new RouteManagerMainMenuState((_index + 1) % MenuItems.Length);
                case InputAction.Down:
                    return new RouteManagerMainMenuState((_index - 1 + MenuItems.Length) % MenuItems.Length);
                case InputAction.Activate:
                    switch (_index)
                    {
                        case 0: return new RouteManagerNewRouteState();
                        case 1:
                            if (Module.ActiveRoute.IsSet)
                                return new RouteManagerRouteInfoState();
                            return new RouteManagerMessageState("No active route", new RouteManagerMainMenuState());
                        case 2: return new RouteManagerLocoAIMenuState();
                        case 3: return new RouteManagerSettingsState();
                        case 4: return new RouteManagerInitialState();
                        default: return new RouteManagerInitialState();
                    }
                default:
                    return this;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  New route options
    // ─────────────────────────────────────────────────────────────
    public class RouteManagerNewRouteState : AStateBehaviour
    {
        private static readonly string[] Items = new string[]
        {
            "Loco → job destination",
            "Loco → specific track",
            "Job cars → job destination",
            "< Back"
        };

        private readonly int _index;

        public RouteManagerNewRouteState(int index = 0)
            : base(BuildState(index))
        {
            _index = Mathf.Clamp(index, 0, Items.Length - 1);
        }

        private static CommsRadioState BuildState(int index)
            => new CommsRadioState("NEW ROUTE", Items[Mathf.Clamp(index, 0, Items.Length - 1)], "SELECT",
                LCDArrowState.Right, LEDState.Off, ButtonBehaviourType.Override);

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            switch (action)
            {
                case InputAction.Up:
                    return new RouteManagerNewRouteState((_index + 1) % Items.Length);
                case InputAction.Down:
                    return new RouteManagerNewRouteState((_index - 1 + Items.Length) % Items.Length);
                case InputAction.Activate:
                    switch (_index)
                    {
                        case 0: return BuildJobRoute(fromLoco: true);
                        case 1: return new RouteManagerSelectTrackState(new RouteManagerNewRouteState());
                        case 2: return BuildJobRoute(fromLoco: false);
                        case 3: return new RouteManagerMainMenuState();
                        default: return new RouteManagerMainMenuState();
                    }
                default:
                    return this;
            }
        }

        private static AStateBehaviour BuildJobRoute(bool fromLoco)
        {
            var booklets = new List<JobBooklet>(JobBooklet.allExistingJobBooklets);
            if (booklets.Count == 0)
                return new RouteManagerMessageState("No job booklets found", new RouteManagerNewRouteState());

            if (booklets.Count == 1)
            {
                string jobId = booklets[0].job.ID;
                CommandArg[] args = fromLoco
                    ? new[] { new CommandArg { String = "loco" }, new CommandArg { String = jobId } }
                    : new[] { new CommandArg { String = "job" }, new CommandArg { String = jobId } };
                return new RouteManagerComputingState(args, new RouteManagerMainMenuState());
            }

            return new RouteManagerSelectJobState(fromLoco, booklets);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Job selection
    // ─────────────────────────────────────────────────────────────
    public class RouteManagerSelectJobState : AStateBehaviour
    {
        private readonly bool _fromLoco;
        private readonly List<JobBooklet> _booklets;
        private readonly int _index;

        public RouteManagerSelectJobState(bool fromLoco, List<JobBooklet> booklets, int index = 0)
            : base(BuildState(booklets, index))
        {
            _fromLoco = fromLoco;
            _booklets = booklets;
            _index = Mathf.Clamp(index, 0, booklets.Count);
        }

        private static CommsRadioState BuildState(List<JobBooklet> booklets, int index)
        {
            int i = Mathf.Clamp(index, 0, booklets.Count);
            string content = i < booklets.Count ? booklets[i].job.ID : "< Back";
            return new CommsRadioState("SELECT JOB", content, "SELECT",
                LCDArrowState.Right, LEDState.Off, ButtonBehaviourType.Override);
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            int total = _booklets.Count + 1; // +1 for back
            switch (action)
            {
                case InputAction.Up:
                    return new RouteManagerSelectJobState(_fromLoco, _booklets, (_index + 1) % total);
                case InputAction.Down:
                    return new RouteManagerSelectJobState(_fromLoco, _booklets, (_index - 1 + total) % total);
                case InputAction.Activate:
                    if (_index >= _booklets.Count)
                        return new RouteManagerNewRouteState();
                    string jobId = _booklets[_index].job.ID;
                    CommandArg[] args = _fromLoco
                        ? new[] { new CommandArg { String = "loco" }, new CommandArg { String = jobId } }
                        : new[] { new CommandArg { String = "job" }, new CommandArg { String = jobId } };
                    return new RouteManagerComputingState(args, new RouteManagerMainMenuState());
                default:
                    return this;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Track selection (from RailTrackRegistry - lists named tracks)
    // ─────────────────────────────────────────────────────────────
    public class RouteManagerSelectTrackState : AStateBehaviour
    {
        private readonly List<string> _trackIds;
        private readonly int _index;
        private readonly AStateBehaviour _cancelState;
        private readonly bool _aiDrive;

        public RouteManagerSelectTrackState(AStateBehaviour cancelState, int index = 0, List<string> trackIds = null, bool aiDrive = false)
            : base(BuildState(trackIds ?? LoadTrackIds(), index))
        {
            _cancelState = cancelState;
            _trackIds = trackIds ?? LoadTrackIds();
            _index = Mathf.Clamp(index, 0, Math.Max(0, _trackIds.Count));
            _aiDrive = aiDrive;
        }

        private static List<string> LoadTrackIds()
        {
            var list = new List<string>();
            var tracks = RailTrackRegistryBase.RailTracks;
            foreach (var t in tracks)
            {
                var lt = t.LogicTrack();
                if (lt == null) continue;
                string fullId = lt.ID.FullID;
                if (!string.IsNullOrEmpty(fullId) && !fullId.StartsWith("#"))
                    list.Add(fullId);
            }
            list.Sort();
            list.Add("< Back");
            return list;
        }

        private static CommsRadioState BuildState(List<string> ids, int index)
        {
            int i = Mathf.Clamp(index, 0, Math.Max(0, ids.Count - 1));
            string content = ids.Count > 0 ? ids[i] : "No tracks found";
            return new CommsRadioState("SELECT TRACK", content, "SELECT",
                LCDArrowState.Right, LEDState.Off, ButtonBehaviourType.Override);
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            if (_trackIds.Count == 0) return _cancelState;
            switch (action)
            {
                case InputAction.Up:
                    return new RouteManagerSelectTrackState(_cancelState, (_index + 1) % _trackIds.Count, _trackIds, _aiDrive);
                case InputAction.Down:
                    return new RouteManagerSelectTrackState(_cancelState, (_index - 1 + _trackIds.Count) % _trackIds.Count, _trackIds, _aiDrive);
                case InputAction.Activate:
                    string selected = _trackIds[_index];
                    if (selected == "< Back") return _cancelState;
                    CommandArg[] args = _aiDrive
                        ? new[]
                        {
                            new CommandArg { String = "auto" },
                            new CommandArg { String = selected }
                        }
                        : new[]
                        {
                            new CommandArg { String = "from" },
                            new CommandArg { String = "loco" },
                            new CommandArg { String = "to" },
                            new CommandArg { String = selected }
                        };
                    AStateBehaviour returnState = _aiDrive
                        ? (AStateBehaviour)new RouteManagerLocoAIMenuState()
                        : new RouteManagerMainMenuState();
                    return new RouteManagerComputingState(args, returnState);
                default:
                    return this;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Route computing state  (uses OnUpdate to poll completion)
    // ─────────────────────────────────────────────────────────────
    public class RouteManagerComputingState : AStateBehaviour
    {
        private enum ComputeStatus { Pending, Done, Error }

        private ComputeStatus _status = ComputeStatus.Pending;
        private string _resultMessage = "";
        private readonly AStateBehaviour _returnState;

        public RouteManagerComputingState(CommandArg[] args, AStateBehaviour returnState)
            : base(new CommsRadioState("ROUTE MANAGER", "Computing route...", "WAIT"))
        {
            _returnState = returnState;
            Module.StartCoroutine(Compute(args));
        }

        private IEnumerator Compute(CommandArg[] args)
        {
            yield return null; // wait one frame

            System.Threading.Tasks.Task task = null;
            try
            {
                task = RouteCommand.DoCommand(args);
            }
            catch (Exception e)
            {
                _resultMessage = e.Message;
                _status = ComputeStatus.Error;
                yield break;
            }

            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted)
            {
                _resultMessage = task.Exception?.InnerException?.Message ?? "Route failed";
                _status = ComputeStatus.Error;
                yield break;
            }

            if (Module.ActiveRoute.IsSet)
            {
                var via = Module.ActiveRoute.Route.Path
                    .Select(p => p.LogicTrack()?.ID?.FullID ?? "")
                    .Where(s => !string.IsNullOrEmpty(s) && !s.StartsWith("#"))
                    .Select(s => {
                        int idx = s.IndexOf('-');
                        return idx > 0 ? s.Substring(0, idx) : s;
                    })
                    .Distinct();

                _resultMessage = $"Route {(Module.ActiveRoute.Route.Length / 1000.0):0.#}km\n"
                    + $"Heading: {Module.ActiveRoute.Route.StartHeading}\n"
                    + $"via: {string.Join(", ", via)}";

                if (Module.ActiveRoute.Route.Reverses.Count > 0)
                    _resultMessage += $"\nReverses: {Module.ActiveRoute.Route.Reverses.Count}";
            }
            else
            {
                _resultMessage = "Route not found";
                _status = ComputeStatus.Error;
                yield break;
            }

            _status = ComputeStatus.Done;
        }

        public override AStateBehaviour OnUpdate(CommsRadioUtility utility)
        {
            if (_status != ComputeStatus.Pending)
                return new RouteManagerMessageState(_resultMessage, _returnState);
            return this;
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            // Allow cancelling with Down
            if (action == InputAction.Down)
            {
                Module.ActiveRoute.ClearRoute();
                return _returnState;
            }
            return this;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Message / result display state
    // ─────────────────────────────────────────────────────────────
    public class RouteManagerMessageState : AStateBehaviour
    {
        private readonly AStateBehaviour _nextState;

        public RouteManagerMessageState(string message, AStateBehaviour nextState)
            : base(new CommsRadioState("ROUTE MANAGER", message, "OK"))
        {
            _nextState = nextState;
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            if (action == InputAction.Activate || action == InputAction.Down)
                return _nextState;
            return this;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Route info state  (cycles: Info/CLEAR → Flip/FLIP → Back)
    // ─────────────────────────────────────────────────────────────
    public class RouteManagerRouteInfoState : AStateBehaviour
    {
        private readonly int _item; // 0 = info/clear, 1 = flip, 2 = back

        public RouteManagerRouteInfoState(int item = 0)
            : base(BuildState(item))
        {
            _item = Mathf.Clamp(item, 0, 2);
        }

        private static CommsRadioState BuildState(int item)
        {
            if (!Module.ActiveRoute.IsSet)
                return new CommsRadioState("ROUTE INFO", "No active route", "BACK");

            string content, action;
            switch (item)
            {
                case 1:
                    content = "Flip direction";
                    action = "FLIP";
                    break;
                case 2:
                    content = "< Back";
                    action = "BACK";
                    break;
                default:
                    var route = Module.ActiveRoute.Route;
                    var tracker = Module.ActiveRoute.RouteTracker;
                    if (tracker != null && tracker.IsWaitingForTurntable)
                    {
                        content = "WAIT: turntable rotating";
                        action = "CLEAR";
                    }
                    else
                    {
                        content = $"Length: {(route.Length / 1000.0):0.#}km\n"
                            + $"Heading: {route.StartHeading}\n"
                            + $"Reverses: {route.Reverses.Count}";
                        if (tracker != null)
                            content += $"\nProgress: {(tracker.DistanceTraveled / 1000.0):0.#}km";
                        action = "CLEAR";
                    }
                    break;
            }
            return new CommsRadioState("ROUTE INFO", content, action,
                LCDArrowState.Right, LEDState.On, ButtonBehaviourType.Override);
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            switch (action)
            {
                case InputAction.Up:
                    return new RouteManagerRouteInfoState((_item + 1) % 3);
                case InputAction.Down:
                    return new RouteManagerRouteInfoState((_item - 1 + 3) % 3);
                case InputAction.Activate:
                    switch (_item)
                    {
                        case 0:
                            Module.ActiveRoute.ClearRoute();
                            return new RouteManagerMessageState("Route cleared", new RouteManagerMainMenuState());
                        case 1:
                            return new RouteManagerFlipRouteState();
                        case 2:
                            return new RouteManagerMainMenuState();
                    }
                    return new RouteManagerMainMenuState();
                default:
                    return this;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Flip route state  — recomputes route in opposite heading
    // ─────────────────────────────────────────────────────────────
    public class RouteManagerFlipRouteState : AStateBehaviour
    {
        private enum FlipStatus { Pending, Done, Error }
        private FlipStatus _status = FlipStatus.Pending;
        private string _message = "";

        public RouteManagerFlipRouteState()
            : base(new CommsRadioState("ROUTE MANAGER", "Flipping route...", "WAIT"))
        {
            Module.StartCoroutine(Flip());
        }

        private IEnumerator Flip()
        {
            yield return null;

            if (!Module.ActiveRoute.IsSet)
            {
                _message = "No active route";
                _status = FlipStatus.Error;
                yield break;
            }

            System.Threading.Tasks.Task<Route> task = null;
            try { task = Module.ActiveRoute.Route.FindOppositeRoute(); }
            catch (Exception e)
            {
                _message = e.Message;
                _status = FlipStatus.Error;
                yield break;
            }

            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted || task.Result == null)
            {
                _message = task.Exception?.InnerException?.Message ?? "No opposite route found";
                _status = FlipStatus.Error;
                yield break;
            }

            Route newRoute = task.Result;
            Trainset trainset = newRoute.Trainset ?? PlayerManager.LastLoco?.trainset;

            Module.ActiveRoute.Route = newRoute;

            if (trainset != null)
            {
                var chain = RouteTaskChain.FromDestination(newRoute.LastTrack.LogicTrack(), trainset);
                var tracker = new RouteTracker(chain, true);
                tracker.SetRoute(newRoute, trainset);
                Module.ActiveRoute.RouteTracker = tracker;
            }

            newRoute.AdjustSwitches();

            _message = $"Flipped {(newRoute.Length / 1000.0):0.#}km\nHeading: {newRoute.StartHeading}";
            _status = FlipStatus.Done;
        }

        public override AStateBehaviour OnUpdate(CommsRadioUtility utility)
        {
            if (_status != FlipStatus.Pending)
                return new RouteManagerMessageState(_message, new RouteManagerRouteInfoState());
            return this;
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            if (action == InputAction.Down) return new RouteManagerMainMenuState();
            return this;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Loco AI menu
    // ─────────────────────────────────────────────────────────────
    public class RouteManagerLocoAIMenuState : AStateBehaviour
    {
        private readonly int _index;

        // Items are built dynamically so "Stop AI" reflects current state
        private static string[] GetItems()
        {
            TrainCar loco = PlayerManager.LastLoco;
            LocoAI ai = Module.TryGetLocoAI(loco);
            bool active = ai != null && (ai.IsRunning || ai.IsFreightHaulActive);
            var items = new System.Collections.Generic.List<string> { "Freight haul", "Drive to destination" };
            if (active) items.Add("Stop AI");
            items.Add("< Back");
            return items.ToArray();
        }

        public RouteManagerLocoAIMenuState(int index = 0)
            : base(BuildState(index))
        {
            _index = index;
        }

        private static CommsRadioState BuildState(int index)
        {
            var items = GetItems();
            int i = Mathf.Clamp(index, 0, items.Length - 1);
            return new CommsRadioState("LOCO AI", items[i], "SELECT",
                LCDArrowState.Right, LEDState.Off, ButtonBehaviourType.Override);
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            var items = GetItems();
            switch (action)
            {
                case InputAction.Up:
                    return new RouteManagerLocoAIMenuState((_index + 1) % items.Length);
                case InputAction.Down:
                    return new RouteManagerLocoAIMenuState((_index - 1 + items.Length) % items.Length);
                case InputAction.Activate:
                    string selected = items[Mathf.Clamp(_index, 0, items.Length - 1)];
                    if (selected == "Freight haul")
                        return BuildFreightHaulJobSelect();
                    if (selected == "Drive to destination")
                    {
                        if (PlayerManager.LastLoco == null)
                            return new RouteManagerMessageState("Board a locomotive first", new RouteManagerLocoAIMenuState());
                        return new RouteManagerSelectTrackState(new RouteManagerLocoAIMenuState(), aiDrive: true);
                    }
                    if (selected == "Stop AI")
                    {
                        TrainCar loco = PlayerManager.LastLoco;
                        Module.TryGetLocoAI(loco)?.StopAll();
                        return new RouteManagerMessageState("AI stopped", new RouteManagerLocoAIMenuState());
                    }
                    return new RouteManagerMainMenuState();
                default:
                    return this;
            }
        }

        private static AStateBehaviour BuildFreightHaulJobSelect()
        {
            if (PlayerManager.LastLoco == null)
                return new RouteManagerMessageState("Board a locomotive first", new RouteManagerLocoAIMenuState());

            var booklets = new System.Collections.Generic.List<JobBooklet>(JobBooklet.allExistingJobBooklets);
            if (booklets.Count == 0)
                return new RouteManagerMessageState("No job booklets found", new RouteManagerLocoAIMenuState());
            if (booklets.Count == 1)
                return new RouteManagerFreightHaulDestSelectState(booklets[0].job);
            return new RouteManagerFreightHaulJobSelectState(booklets);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Freight haul – job selection (when > 1 job)
    // ─────────────────────────────────────────────────────────────
    public class RouteManagerFreightHaulJobSelectState : AStateBehaviour
    {
        private readonly List<JobBooklet> _booklets;
        private readonly int _index;

        public RouteManagerFreightHaulJobSelectState(List<JobBooklet> booklets, int index = 0)
            : base(BuildState(booklets, index))
        {
            _booklets = booklets;
            _index = Mathf.Clamp(index, 0, booklets.Count);
        }

        private static CommsRadioState BuildState(List<JobBooklet> booklets, int index)
        {
            int i = Mathf.Clamp(index, 0, booklets.Count);
            string content = i < booklets.Count ? booklets[i].job.ID : "< Back";
            return new CommsRadioState("SELECT JOB", content, "SELECT",
                LCDArrowState.Right, LEDState.Off, ButtonBehaviourType.Override);
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            int total = _booklets.Count + 1;
            switch (action)
            {
                case InputAction.Up:
                    return new RouteManagerFreightHaulJobSelectState(_booklets, (_index + 1) % total);
                case InputAction.Down:
                    return new RouteManagerFreightHaulJobSelectState(_booklets, (_index - 1 + total) % total);
                case InputAction.Activate:
                    if (_index >= _booklets.Count)
                        return new RouteManagerLocoAIMenuState();
                    return new RouteManagerFreightHaulDestSelectState(_booklets[_index].job);
                default:
                    return this;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Freight haul – destination/task selection within a job
    // ─────────────────────────────────────────────────────────────
    public class RouteManagerFreightHaulDestSelectState : AStateBehaviour
    {
        private readonly Job _job;
        private readonly List<RouteTask> _tasks;
        private readonly int _index;

        public RouteManagerFreightHaulDestSelectState(Job job, int index = 0)
            : base(BuildState(job, GetTasks(job), index))
        {
            _job = job;
            _tasks = GetTasks(job);
            _index = Mathf.Clamp(index, 0, _tasks.Count);
        }

        private RouteManagerFreightHaulDestSelectState(Job job, List<RouteTask> tasks, int index)
            : base(BuildState(job, tasks, index))
        {
            _job = job;
            _tasks = tasks;
            _index = index;
        }

        private static List<RouteTask> GetTasks(Job job)
        {
            var list = new List<RouteTask>();
            RouteTaskChain chain = RouteTaskChain.FromDVJob(job);
            while (chain != null)
            {
                if (chain.tasks != null)
                    list.AddRange(chain.tasks);
                chain = chain.nextTasks;
            }
            return list;
        }

        private static CommsRadioState BuildState(Job job, List<RouteTask> tasks, int index)
        {
            int total = tasks.Count + 1;
            int i = Mathf.Clamp(index, 0, total - 1);
            string content = i < tasks.Count
                ? tasks[i].DestinationTrack?.ID?.FullID ?? "Unknown"
                : "< Back";
            return new CommsRadioState("SELECT DEST", content, "SELECT",
                LCDArrowState.Right, LEDState.Off, ButtonBehaviourType.Override);
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            int total = _tasks.Count + 1;
            switch (action)
            {
                case InputAction.Up:
                    return new RouteManagerFreightHaulDestSelectState(_job, _tasks, (_index + 1) % total);
                case InputAction.Down:
                    return new RouteManagerFreightHaulDestSelectState(_job, _tasks, (_index - 1 + total) % total);
                case InputAction.Activate:
                    if (_index >= _tasks.Count)
                        return new RouteManagerLocoAIMenuState();
                    return new RouteManagerFreightHaulStartState(_tasks[_index]);
                default:
                    return this;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Freight haul – start (kicks off AI, shows running state)
    // ─────────────────────────────────────────────────────────────
    public class RouteManagerFreightHaulStartState : AStateBehaviour
    {
        private readonly RouteTask _task;

        public RouteManagerFreightHaulStartState(RouteTask task)
            : base(new CommsRadioState("LOCO AI", "Starting freight haul...", "WAIT"))
        {
            _task = task;
            StartHaul();
        }

        private void StartHaul()
        {
            try
            {
                TrainCar loco = PlayerManager.LastLoco;
                if (loco == null) return;
                LocoAI ai = Module.GetLocoAI(loco);
                ai.StartFreightHaul(_task, loco);
            }
            catch (Exception e)
            {
                Module.mod.Logger.Log("StartFreightHaul error: " + e.Message);
            }
        }

        public override AStateBehaviour OnUpdate(CommsRadioUtility utility)
        {
            TrainCar loco = PlayerManager.LastLoco;
            LocoAI ai = Module.TryGetLocoAI(loco);
            if (ai != null && (ai.IsRunning || ai.IsFreightHaulActive))
                return new RouteManagerFreightHaulRunningState();
            // Still starting up — wait one more tick
            return this;
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            if (action == InputAction.Down)
            {
                Module.TryGetLocoAI(PlayerManager.LastLoco)?.StopAll();
                return new RouteManagerLocoAIMenuState();
            }
            return this;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Freight haul – running status display
    // ─────────────────────────────────────────────────────────────
    public class RouteManagerFreightHaulRunningState : AStateBehaviour
    {
        public RouteManagerFreightHaulRunningState()
            : base(BuildState())
        { }

        private static CommsRadioState BuildState()
        {
            string status = "Running...";
            var tracker = Module.ActiveRoute?.RouteTracker;
            if (tracker != null)
                status = $"To go: {(tracker.DistanceToFinish / 1000.0):0.0}km";
            return new CommsRadioState("LOCO AI", status, "STOP",
                LCDArrowState.Off, LEDState.On, ButtonBehaviourType.Override);
        }

        public override AStateBehaviour OnUpdate(CommsRadioUtility utility)
        {
            TrainCar loco = PlayerManager.LastLoco;
            LocoAI ai = Module.TryGetLocoAI(loco);
            if (ai == null || (!ai.IsRunning && !ai.IsFreightHaulActive))
                return new RouteManagerMessageState("Freight haul complete!", new RouteManagerLocoAIMenuState());
            // Refresh display
            return new RouteManagerFreightHaulRunningState();
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            if (action == InputAction.Activate)
            {
                Module.TryGetLocoAI(PlayerManager.LastLoco)?.StopAll();
                return new RouteManagerMessageState("AI stopped", new RouteManagerLocoAIMenuState());
            }
            return this;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Loco AI state (legacy – kept for reference, not in menu)
    // ─────────────────────────────────────────────────────────────
    public class RouteManagerLocoAIState : AStateBehaviour
    {
        public RouteManagerLocoAIState()
            : base(new CommsRadioState("LOCO AI",
                "Auto-drive to destination\nRequires active route",
                "START", LCDArrowState.Off, LEDState.Off, ButtonBehaviourType.Override))
        { }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            switch (action)
            {
                case InputAction.Activate:
                    try
                    {
                        if (!Module.ActiveRoute.IsSet)
                            return new RouteManagerMessageState("No active route", new RouteManagerMainMenuState());

                        TrainCar loco = PlayerManager.LastLoco;
                        if (loco == null)
                            return new RouteManagerMessageState("No locomotive found", this);

                        LocoAI ai = Module.GetLocoAI(loco);
                        if (Module.ActiveRoute.RouteTracker == null)
                            return new RouteManagerMessageState("No route tracker", this);
                        ai.StartAI(Module.ActiveRoute.RouteTracker);
                        return new RouteManagerMessageState("AI started", new RouteManagerMainMenuState());
                    }
                    catch (Exception e)
                    {
                        return new RouteManagerMessageState(e.Message, this);
                    }
                case InputAction.Down:
                    return new RouteManagerMainMenuState();
                default:
                    return this;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Settings state
    // ─────────────────────────────────────────────────────────────
    public class RouteManagerSettingsState : AStateBehaviour
    {
        private readonly int _index;

        private static string[] GetItems()
        {
            return new[]
            {
                $"Reversing: {Module.settings.ReversingStrategy}",
                "< Back"
            };
        }

        public RouteManagerSettingsState(int index = 0)
            : base(BuildState(index))
        {
            _index = index;
        }

        private static CommsRadioState BuildState(int index)
        {
            var items = GetItems();
            int i = Mathf.Clamp(index, 0, items.Length - 1);
            return new CommsRadioState("SETTINGS", items[i], "SELECT",
                LCDArrowState.Right, LEDState.Off, ButtonBehaviourType.Override);
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            var items = GetItems();
            switch (action)
            {
                case InputAction.Up:
                    return new RouteManagerSettingsState((_index + 1) % items.Length);
                case InputAction.Down:
                    return new RouteManagerSettingsState((_index - 1 + items.Length) % items.Length);
                case InputAction.Activate:
                    if (_index == 0)
                    {
                        Module.settings.ReversingStrategy = (ReversingStrategy)Utils.NextEnumItem(Module.settings.ReversingStrategy);
                        return new RouteManagerSettingsState(0);
                    }
                    return new RouteManagerMainMenuState();
                default:
                    return this;
            }
        }
    }
}
