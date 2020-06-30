using CommandTerminal;
using DV.Logic.Job;
using DV.Teleporters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

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

                Terminal.Log($"Using job {jobBooklet.job.ID}");

                foreach (var task in jobBooklet.job.tasks)
                {
                    TransportTask transport = task as TransportTask;
                    if (transport != null && task.GetTaskData().cars.Count > 0)
                    {

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
                            //find first car of job trainset
                            if (!TrainCar.logicCarToTrainCar.TryGetValue(task.GetTaskData().cars[0], out trainCar))
                            {
                                throw new CommandException("Error 50");
                            }
                        }

                        Track startTrack = trainCar.trainset.firstCar.Bogies[0].track.logicTrack;

                        FindAndSwitch(startTrack, transport.GetTaskData().destinationTrack, ReversingStrategy.ChooseBest);
                    }
                    else
                    {
                        throw new CommandException("Not supported job type yet");
                    }
                }
            }
            else if (args[0].String == "from" && args.Length == 4 && args[2].String == "to")
            {
                if (args[1].String == "loco")
                {
                    TrainCar trainCar = PlayerManager.LastLoco;

                    if (trainCar == null)
                    {
                        throw new CommandException("No last loco");
                    }
                    args[1].String = trainCar.trainset.firstCar.logicCar.CurrentTrack.ID.FullDisplayID;
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

                FindAndSwitch(startTrack.logicTrack, goalTrack.logicTrack, ReversingStrategy.ChooseBest);
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
                }
                else
                {
                    throw new CommandException("no active route");
                }
            }
            else
            {
                throw new CommandException($"Unknown subcommand {args[0]}");
            }

        }

        private static void FindAndSwitch(Track begin, Track end, ReversingStrategy reversingStrategy)
        {
            if (begin is null)
            {
                throw new CommandException("Empty begin");
            }

            if (end is null)
            {
                throw new CommandException("Empty end");
            }

            var route = FindRoute(begin, end, reversingStrategy);
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

        private static Route FindRoute(Track begin, Track end, ReversingStrategy reversingStrategy)
        {
            PathFinder pathFinder = new PathFinder(begin, end);
            var path = pathFinder.FindPath(false);
            Route route = null;

            if (path.Count == 0 || reversingStrategy == ReversingStrategy.ChooseBest)
            {
#if DEBUG
                Terminal.Log($"Trying path with allowed reversing");
#endif
                var pathWithReversing = pathFinder.FindPath(true);

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
