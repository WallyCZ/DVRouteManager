using CommandTerminal;
using DV.Logic.Job;
using DV.Teleporters;
using DVRouteManager.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions.Must;

namespace DVRouteManager
{

    public enum ReversingStrategy
    {
        Forbiden,
        ChooseBest
    }

    public class CommandException : Exception
    {
        public CommandException(string message) : base(message)
        {
        }
    }

    public static class RouteCommand
    {
        public static void DoTerminalCommand(CommandArg[] args)
        {
            try
            {
                if (!Terminal.IssuedError)
                {
                    if (args.Length == 0)
                    {
                        Terminal.Log("Build route command");
                        Terminal.Log("Examples:");
                        Terminal.Log("route job [jobname]");
                        Terminal.Log("route from [from_track] to [to_track]");
                        Terminal.Log("route loco");
                        Terminal.Log("route clear");
                        return;
                    }

                    DoCommand(args);
                }
            }
            catch (CommandException exc)
            {
                Terminal.Log(exc.Message);
            }
            catch (Exception exc)
            {
                Terminal.Log(exc.Message + " " + exc.StackTrace);
            }
        }
        public static void DoCommand(CommandArg[] args)
        {
            if(args.Length == 0)
            {
                throw new CommandException("No command args");
            }

            if (args[0].String == "job" || args[0].String == "loco")
            {
                List<JobBooklet> allJobBooklets = new List<JobBooklet>(JobBooklet.allExistingJobBooklets);
                JobBooklet jobBooklet = null;

                if (allJobBooklets.Count == 0)
                {
                    throw new CommandException("No current job");
                }

                if (allJobBooklets.Count > 1 || args.Length > 1)
                {
                    if (args.Length < 2)
                    {
                        throw new CommandException("Multiple job, specify job name");
                    }

                    jobBooklet = allJobBooklets.FirstOrDefault(j => j.job.ID == args[1].String);
                }
                else
                {
                    jobBooklet = allJobBooklets[0];
                }


                if (jobBooklet == null)
                {
                    throw new CommandException("Unknown job");
                }

                RouteTaskChain chain = RouteTaskChain.FromDVJob(jobBooklet.job);

                if (chain == null)
                {
                    throw new CommandException("Not supported job type yet");
                }

                Terminal.Log($"Using job {jobBooklet.job.ID} chain {chain}");

                TrainCar trainCar;

                if (args[0].String == "loco")
                {
                    trainCar = PlayerManager.LastLoco;

                    if (trainCar == null)
                    {
                        throw new CommandException("No last loco");
                    }
                }
                else
                {
                    trainCar = Module.RouteTracker.CurrentTask.TrainSets.FirstOrDefault()?.firstCar;
                }

                Module.RouteTracker = new RouteTracker(chain, trainCar, true);

                if (Module.RouteTracker.CurrentTask == null)
                {
                    Module.RouteTracker = null;
                    throw new CommandException("No suitable task");
                }

                Track startTrack = trainCar.trainset.firstCar.Bogies[0].track.logicTrack;

                FindAndSwitch(startTrack, Module.RouteTracker.CurrentTask.DestinationTrack, ReversingStrategy.ChooseBest, trainCar.trainset);

                Module.RouteTracker.SetRoute(Module.CurrentRoute);

            }
            else if (args[0].String == "tracker")
            {
                TrainCar trainCar = PlayerManager.LastLoco;

                if (trainCar == null)
                {
                    throw new CommandException("No last loco");
                }

                if (Module.RouteTracker.CurrentTask == null)
                {
                    throw new CommandException("Tracker has no task");
                }

                Track startTrack = trainCar.trainset.firstCar.Bogies[0].track.logicTrack;

                FindAndSwitch(startTrack, Module.RouteTracker.CurrentTask.DestinationTrack, ReversingStrategy.ChooseBest, trainCar.trainset);

                Module.RouteTracker.SetRoute(Module.CurrentRoute);
            }
            else if (args[0].String == "from" && args.Length == 4 && args[2].String == "to")
            {
                Trainset trainset = null; //default value if we don't have consist (trainset)

                if (args[1].String == "loco")
                {
                    TrainCar trainCar = PlayerManager.LastLoco;

                    if (trainCar == null)
                    {
                        throw new CommandException("No last loco");
                    }
                    args[1].String = trainCar.trainset.firstCar.logicCar.CurrentTrack.ID.FullDisplayID;
                    trainset = trainCar.trainset;
                }
                else if (args[1].String == "job.trainset")
                {
                    //TODO
                }

                RailTrack startTrack = TrackFinder.AllTracks.FirstOrDefault((RailTrack track) => track?.logicTrack.ID.FullDisplayID == args[1].String);

                RailTrack goalTrack = TrackFinder.AllTracks.FirstOrDefault((RailTrack track) => track?.logicTrack.ID.FullDisplayID == args[3].String);

                if (startTrack == null || goalTrack == null)
                {
                    throw new CommandException("start track or goal track not found");
                }

                FindAndSwitch(startTrack.logicTrack, goalTrack.logicTrack, ReversingStrategy.ChooseBest, trainset);
            }
            else if (args[0].String == "clear")
            {
                if (Module.IsCurrentRouteSet)
                {
                    Module.CurrentRoute = null;
                }
                else
                {
                    throw new CommandException("no active route");
                }
            }
            else if (args[0].String == "info")
            {
                if(Module.IsCurrentRouteSet)
                {
                    Terminal.Log("Active route:");
                    Terminal.Log(Module.CurrentRoute.ToString());
#if DEBUG
                    Terminal.Log( Module.CurrentRoute.Path.Select(t => t.logicTrack.ID.FullID).Aggregate((i, j) => i + "->" + j) );
#endif
                }
                else
                {
                    throw new CommandException("no active route");
                }
            }
#if DEBUG
            else if (args[0].String == "track")
            {
                var track = TrackFinder.AllTracks.Where(t => t.logicTrack.ID.FullDisplayID.ToLower() == args[1].String.ToLower()).FirstOrDefault();
                if(track == null)
                {
                    throw new CommandException("track not found");
                }

                Terminal.Log($"{track.logicTrack.ID.FullID} {track.logicTrack.length}m");

                if(track.inIsConnected)
                {
                    Terminal.Log("IN: " + track.GetAllInBranches().Select(b => b.track.logicTrack.ID.FullID).Aggregate((a, b) => a + "; " + b)  );
                }
                if (track.outIsConnected)
                {
                    Terminal.Log("OUT: " + track.GetAllOutBranches().Select(b => b.track.logicTrack.ID.FullID).Aggregate((a, b) => a + "; " + b));
                }
            }
#endif
            else if (args[0].String == "auto")
            {
                TrainCar trainCar = PlayerManager.LastLoco;

                if (trainCar == null)
                {
                    throw new CommandException("No last loco");
                }

                ILocomotiveRemoteControl remote = trainCar.GetComponent<ILocomotiveRemoteControl>();

                if (trainCar == null)
                {
                    throw new CommandException("No loco remote");
                }

                RailTrack goalTrack = TrackFinder.AllTracks.FirstOrDefault((RailTrack track) => track?.logicTrack.ID.FullDisplayID == args[1].String);
                if (goalTrack == null)
                {
                    throw new CommandException("Goal track not found");
                }

                RouteTaskChain chain = RouteTaskChain.FromDestination(goalTrack.logicTrack, trainCar.trainset);
                var tracker = new RouteTracker(chain, trainCar, false);

                Track startTrack = trainCar.trainset.firstCar.Bogies[0].track.logicTrack;

                var route = FindRoute(startTrack, tracker.CurrentTask.DestinationTrack, ReversingStrategy.ChooseBest, trainCar.trainset);

                tracker.SetRoute(route);

                var driver = new LocoAI(tracker, remote);
                driver.Start();
            }
            else
            {
                throw new CommandException($"Unknown subcommand {args[0]}");
            }

        }

        private static void FindAndSwitch(Track begin, Track end, ReversingStrategy reversingStrategy, Trainset trainset)
        {
            if (begin is null)
            {
                throw new CommandException("Empty begin");
            }

            if (end is null)
            {
                throw new CommandException("Empty end");
            }

            var route = FindRoute(begin, end, reversingStrategy, trainset);
            if (route == null)
            {
                Module.CurrentRoute = null;
                throw new CommandException("Path cannot be found");
            }
            else
            {
                Module.CurrentRoute = route;
                route.AdjustSwitches();
            }
        }

        private static Route FindRoute(Track begin, Track end, ReversingStrategy reversingStrategy, Trainset trainset)
        {
            List<TrackTransition> trackTransitions = null;



            double consistLength = 30.0;

            if(trainset != null)
            {
                consistLength = trainset.Length();
            }


            PathFinder pathFinder = new PathFinder(begin, end);
            var path = pathFinder.FindPath(false, consistLength, trackTransitions);
            Route route = null;

            if (path.Count == 0 || reversingStrategy == ReversingStrategy.ChooseBest)
            {
#if DEBUG
                Terminal.Log($"Trying path with allowed reversing");
#endif
                var pathWithReversing = pathFinder.FindPath(true, consistLength, trackTransitions);

                if (pathWithReversing.Count > 0)
                {
                    if (path.Count > 0) //choose best
                    {
                        var routeWithoutReversing = new Route(path, end);
                        var routeWithReversing = new Route(pathWithReversing, end);

                        Terminal.Log($"withoutreversing: {routeWithoutReversing.Length} withReversing: {routeWithReversing.Length}");

                        route  = routeWithoutReversing.Length > routeWithReversing.Length ? routeWithReversing : routeWithoutReversing;
                    }
                    else
                    {
                        path = pathWithReversing;
                    }
                }
            }

            if(route != null)
            {
                Terminal.Log($"Found {route}");
                return route;
            }

            if (path.Count == 0)
            {
                return null;
            }

            route = new Route(path, end);

            Terminal.Log($"Found {route}");

            return route;
        }

    }
}
