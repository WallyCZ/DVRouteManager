using CommandTerminal;
using DV;
using DV.Logic.Job;
using DVRouteManager.Internals;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace DVRouteManager
{

    public class CommsRouteManager : MonoBehaviour, ICommsRadioMode
    {
        protected enum State
        {
            MainMenu,
            PickMode,
            SelectJob,
            SelectTown,
            SelectStation,
            SelectTrack,
            Settings,
            Update,
            Message
        }

        protected enum FindRouteMode
        {
            Job,
            LocoJob,
            LocoTrack,
            ClearRoute,
            Settings,
            Cancel
        }


        private const string TITLE = "ROUTE MNGR";
        private const string GENERAL_TRACK_PREFIX = "#Y-";
        private const string TRACK_PARTS_SEPARATOR = "-";

        public CommsRadioDisplay display;

        public ButtonBehaviourType ButtonBehaviour { get; set; }

        protected State state = State.MainMenu;

        protected FindRouteMode routeMode = FindRouteMode.Job;

        private Selector<string> jobSelector;

        private Selector<string> townSelector;
        private string[] townCodesArray;

        private Selector<string> stationSelector;

        private Selector<string> trackSelector;

        public bool ButtonACustomAction()
        {
            switch (state)
            {
                case State.PickMode:
                    routeMode = (FindRouteMode)Utils.NextEnumItem(routeMode);
                    PrintRouteMode();
                    return true;
                case State.SelectJob:
                    jobSelector.MoveNextRewind();
                    PrintCurrentJob();
                    return true;
                case State.SelectTown:
                    townSelector.MoveNextRewind();
                    PrintCurrentTown();
                    return true;
                case State.SelectStation:
                    stationSelector.MoveNextRewind();
                    PrintCurrentStation();
                    return true;
                case State.SelectTrack:
                    trackSelector.MoveNextRewind();
                    PrintCurrentTrack();
                    return true;
            }

            return false;
        }

        public bool ButtonBCustomAction()
        {
            switch (state)
            {
                case State.PickMode:
                    routeMode = (FindRouteMode) Utils.PreviousEnumItem(routeMode);
                    PrintRouteMode();
                    return true;
                case State.SelectJob:
                    jobSelector.MovePrevRewind();
                    PrintCurrentJob();
                    return true;
                case State.SelectTown:
                    townSelector.MovePrevRewind();
                    PrintCurrentTown();
                    return true;
                case State.SelectStation:
                    stationSelector.MovePrevRewind();
                    PrintCurrentStation();
                    return true;
                case State.SelectTrack:
                    trackSelector.MovePrevRewind();
                    PrintCurrentTrack();
                    return true;
            }

            return false;
        }

        public void Disable()
        {
            
        }

        public void Enable()
        {
            
        }

        public Color GetLaserBeamColor()
        {
            return new Color(0.5f, 0.5f, 0.5f, 0.0f);
        }

        public void OnUpdate()
        {
            
        }

        public void OnUse()
        {
            switch(state)
            {
                case State.MainMenu:
                    SetState(State.PickMode);
                    break;
                case State.PickMode:
                    switch (routeMode)
                    {
                        case FindRouteMode.Job:
                        case FindRouteMode.LocoJob:
                            List<JobBooklet> allJobBooklets = new List<JobBooklet>(JobBooklet.allExistingJobBooklets);

                            if (allJobBooklets.Count == 0)
                            {
                                this.display.SetDisplay(TITLE, "No job", "Confirm");
                                SetState(State.Message);
                                break;
                            }

                            if (allJobBooklets.Count > 1)
                            {
                                jobSelector = new Selector<string>( allJobBooklets.Select(b => b.job.ID).ToList());
                                jobSelector.MoveNext();
                                SetState(State.SelectJob);
                            }
                            else
                            {
                                UseJob(allJobBooklets[0].job.ID);
                            }

                            break;
                        case FindRouteMode.ClearRoute:
                            Module.CurrentRoute = null;
                            this.display.SetDisplay(TITLE, "Route cleared", "MENU");
                            SetState(State.Message);
                            break;
                        case FindRouteMode.LocoTrack:
                            FillTownSelectors();
                            SetState(State.SelectTown);
                            break;
                        case FindRouteMode.Settings:
                            SetState(State.Settings);
                            break;
                        default:
                            SetState(State.MainMenu);
                            break;
                    }

                    break;
                case State.SelectJob:
                    UseJob(jobSelector.Current);
                    break;
                case State.SelectTown:
                    FillStationSelectors();
                    SetState(State.SelectStation);
                    break;
                case State.SelectStation:
                    FillTrackSelectors();
                    SetState(State.SelectTrack);
                    break;
                case State.SelectTrack:
                    {
                        string destinationTrackId = townCodesArray[townSelector.Index] + TRACK_PARTS_SEPARATOR + stationSelector.Current + TRACK_PARTS_SEPARATOR + trackSelector.Current;
                        Terminal.Log($"Selected track {destinationTrackId}");
                        CommandArg[] args = new CommandArg[]
                        {
                            new CommandArg() { String = "from" },
                            new CommandArg() { String = "loco" },
                            new CommandArg() { String = "to" },
                            new CommandArg() { String = destinationTrackId }
                        };
                        BuildRoute(args);
                        break;
                    }
                case State.Settings:
                    SetState(State.MainMenu);
                    break;
                case State.Update:
                    StartCoroutine(DoUpdate());
                    break;
                case State.Message:
                    SetState(State.MainMenu);
                    break;
            }

        }

        private IEnumerator DoUpdate()
        {
            UnityWebRequest www = null;

            try
            {
                this.display.SetDisplay(TITLE, "Downloading update...", "");

                www = UnityWebRequest.Get(Module.VersionForUpdate.downloadUrl);
                www.downloadHandler = new DownloadHandlerBuffer();
            }
            catch (Exception e)
            {
                Terminal.Log(e.Message + " " + e.StackTrace);
                SetState(State.MainMenu);
            }

            if (www != null)
            {
                yield return www.SendWebRequest();

                while (!www.downloadHandler.isDone)
                    yield return null;

                try
                { 
                    if (!www.isHttpError && !www.isNetworkError)
                    {
                        this.display.SetDisplay(TITLE, "Extracting update...", "");
                        string outFile = Path.GetTempFileName();

                        try
                        {
                            File.WriteAllBytes(outFile, www.downloadHandler.data);

                            string assemblyPath = Path.GetDirectoryName(this.GetType().Assembly.Location);

                            //rename currently used DLLs
                            foreach (string file in Directory.GetFiles(assemblyPath, "*.dll"))
                            {
                                if (Path.GetFileName(file).StartsWith("DVRouteManager"))
                                    continue;

                                string renameTo = file + ".old";
                                File.Delete(renameTo);
                                System.IO.File.Move(file, renameTo);
                            }

                            (new Unzip(outFile)).ExtractToDirectory(
                                Path.GetDirectoryName(assemblyPath)); //get parent directory

                            this.display.SetDisplay(TITLE, "Done, update applies after game restart", "OK");
                        }
                        finally
                        {
                            SetState(State.Message);

                            File.Delete(outFile);
                        }
                    }
                }
                catch (Exception e)
                {
                    Terminal.Log(e.Message + " " + e.StackTrace);
                    SetState(State.MainMenu);
                }
            }
            else
            {
                SetState(State.MainMenu);
            }


        }

        public void OverrideSignalOrigin(Transform signalOrigin)
        {
            
        }

        public void UseJob(string jobName)
        {
            CommandArg[] args;
            switch (routeMode)
            {
                case FindRouteMode.Job:
                    args = new CommandArg[]
                    {
                        new CommandArg() { String = "job" },
                        new CommandArg() { String = jobName }
                    };
                    break;
                case FindRouteMode.LocoJob:
                    args = new CommandArg[]
                    {
                        new CommandArg() { String = "loco" },
                        new CommandArg() { String = jobName }
                    };
                    break;
                default:
                    SetState(State.MainMenu);
                    return;
            }
            BuildRoute(args);
        }

        private void BuildRoute(CommandArg[] args)
        {
            try
            {
                RouteCommand.DoCommand(args);

                if (Module.IsCurrentRouteSet)
                {
                    StringBuilder via = Module.CurrentRoute.Path.Select(p => p.logicTrack.ID.FullDisplayID)
                        .Where(s => !s.StartsWith(GENERAL_TRACK_PREFIX))
                        .Select(s => s.GetUntilOrEmpty(TRACK_PARTS_SEPARATOR))
                        .Distinct()
                        .Aggregate(new StringBuilder(), (current, next) => current.Append(current.Length == 0 ? "" : ", ").Append(next));

                    string routeInfo = $"Route {(Module.CurrentRoute.Length / 1000.0):0.#}km\nHeading: {Module.CurrentRoute.StartHeading}\nvia: {via}";

                    if (Module.CurrentRoute.Reverses.Count > 0)
                        routeInfo += $"\nReverses: {Module.CurrentRoute.Reverses.Count}";

                    this.display.SetDisplay(TITLE, routeInfo, "MENU");
                }
                else
                {
                    this.display.SetDisplay(TITLE, "Route not found", "MENU");
                }
                SetState(State.Message);
            }
            catch (CommandException exc)
            {
                this.display.SetDisplay(TITLE, exc.Message, "MENU");
                SetState(State.Message);
            }
            catch (Exception exc)
            {
                Terminal.Log(exc.Message + ": " + exc.StackTrace);
                this.display.SetDisplay(TITLE, "Error in building path, see console", "MENU");
                SetState(State.Message);
            }
        }

        private void FillStationSelectors()
        {
            var stationList = TrackFinder.AllTracks.Select(p => p.logicTrack.ID.FullDisplayID)
                .Where(s => s.StartsWith( townCodesArray[townSelector.Index] + TRACK_PARTS_SEPARATOR) )
                .Select(s => s.GetAfterOrEmpty(TRACK_PARTS_SEPARATOR).GetUntilOrEmpty(TRACK_PARTS_SEPARATOR))
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            if(stationList.Count == 0)
            {
                Terminal.Log("empty station selector!");
            }

            stationSelector = new Selector<string>(stationList);
            stationSelector.MoveNext();
        }

        private void FillTrackSelectors()
        {
            var trackList = TrackFinder.AllTracks.Select(p => p.logicTrack.ID.FullDisplayID)
                .Where(s => s.StartsWith(townCodesArray[townSelector.Index] + TRACK_PARTS_SEPARATOR + stationSelector.Current + TRACK_PARTS_SEPARATOR))
                .Select(s => s.GetAfterOrEmpty(TRACK_PARTS_SEPARATOR).GetAfterOrEmpty(TRACK_PARTS_SEPARATOR))
                .OrderBy(s => s)
                .ToList();

            if (trackList.Count == 0)
            {
                Terminal.Log("empty track selector!");
            }

            trackSelector = new Selector<string>(trackList);
            trackSelector.MoveNext();
        }

        private void FillTownSelectors()
        {
            if (townCodesArray != null && townSelector != null)
                return;

            townCodesArray = TrackFinder.AllTracks.Select(p => p.logicTrack.ID.FullDisplayID)
                .Where(s => !s.StartsWith(GENERAL_TRACK_PREFIX))
                .Select(s => s.GetUntilOrEmpty(TRACK_PARTS_SEPARATOR))
                .Distinct()
                .OrderBy(s => s)
                .ToArray();

            List<string> townNames = new List<string>();

            foreach (var townCode in townCodesArray)
            {
                switch(townCode)
                {
                    case "HB":
                        townNames.Add("Harbor and town"); break;
                    case "GF":
                        townNames.Add("Goods factory and town"); break;
                    case "FF":
                        townNames.Add("Foods factory and town"); break;
                    case "OWN":
                        townNames.Add("Oil well north"); break;
                    case "OWC":
                        townNames.Add("Oil well central"); break;
                    case "CM":
                        townNames.Add("Coal mine"); break;
                    case "SM":
                        townNames.Add("Steel mill"); break;
                    case "CSW":
                        townNames.Add("City SW"); break;
                    case "IME":
                        townNames.Add("Iron ore mine east"); break;
                    case "IMW":
                        townNames.Add("Iron ore mine west"); break;
                    case "FRC":
                        townNames.Add("Forest central"); break;
                    case "FRS":
                        townNames.Add("Forest south"); break;
                    case "FM":
                        townNames.Add("Farm"); break;
                    case "MF":
                        townNames.Add("Machine factory and town"); break;
                    case "MB":
                        townNames.Add("Military base"); break;
                    case "SW":
                        townNames.Add("Sawmill"); break;
                    case "HMB":
                        townNames.Add("Harbor military base"); break;
                    case "MFMB":
                        townNames.Add("Machine factory military base"); break;
                    default:
                        townNames.Add(townCode); break;
                }
            }

            townSelector = new Selector<string>(townNames);
            townSelector.MoveNext();
        }

        protected void PrintCurrentJob()
        {
            this.display.SetDisplay(TITLE, jobSelector.Current, "Select");
        }

        protected void PrintCurrentTown()
        {
            this.display.SetDisplay(TITLE, townSelector.Current, "Select place");
        }
        protected void PrintCurrentStation()
        {
            this.display.SetDisplay(TITLE, stationSelector.Current, "Select");
        }

        protected void PrintCurrentTrack()
        {
            this.display.SetDisplay(TITLE, trackSelector.Current, "Select");
        }

        protected void PrintRouteMode()
        {
            switch(routeMode)
            {
                case FindRouteMode.Job:
                    this.display.SetDisplay(TITLE, "From job cars\nto job destination", "Build route");
                    break;
                case FindRouteMode.LocoJob:
                    this.display.SetDisplay(TITLE, "From last used locomotion\nto job destination", "Build route");
                    break;
                case FindRouteMode.LocoTrack:
                    this.display.SetDisplay(TITLE, "From last used locomotion\nto specific track", "Select");
                    break;
                case FindRouteMode.ClearRoute:
                    this.display.SetDisplay(TITLE, "Clear route", "Clear");
                    break;
                case FindRouteMode.Settings:
                    this.display.SetDisplay(TITLE, "", "Settings");
                    break;
                case FindRouteMode.Cancel:
                    this.display.SetDisplay(TITLE, "Cancel", "Main menu");
                    break;
            }
        }

        protected void PrintSettings()
        {
            this.display.SetDisplay(TITLE, "Under construction", "MENU");
        }

        public void SetMainDisplay()
        {
            this.display.SetDisplay(TITLE, "Route Manager v" + this.GetType().Assembly.GetName().Version.ToString(3), "Menu");
        }

        public void SetStartingDisplay()
        {
            if ( Module.VersionForUpdate != null )
            {
                this.display.SetDisplay(TITLE, $"NEW VERSION {Module.VersionForUpdate.Version} AVAILABLE!" , "UPDATE");
                SetState(State.Update);
            }
            else
            {
                SetMainDisplay();
            }

        }

        private void SetState(State newState)
        {
            if (this.state == newState)
            {
                return;
            }

#if DEBUG
            Terminal.Log($"{state} -> {newState}");
#endif
            this.state = newState;


            switch (this.state)
            {
                case State.MainMenu:
                    this.SetMainDisplay();
                    this.ButtonBehaviour = ButtonBehaviourType.Regular;
                    return;
                case State.PickMode:
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    PrintRouteMode();
                    return;
                case State.SelectJob:
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    PrintCurrentJob();
                    return;
                case State.SelectTown:
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    PrintCurrentTown();
                    return;
                case State.SelectStation:
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    PrintCurrentStation();
                    return;
                case State.SelectTrack:
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    PrintCurrentTrack();
                    return;
                case State.Settings:
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    PrintSettings();
                    return;
                case State.Update:
                    this.ButtonBehaviour = ButtonBehaviourType.Regular;
                    return;
                case State.Message:
                    this.ButtonBehaviour = ButtonBehaviourType.Regular;
                    return;
                default:
                    return;
            }
        }
    }

}
