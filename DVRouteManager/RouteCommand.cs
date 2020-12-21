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

    public class CommandException : Exception
    {
        public CommandException(string message) : base(message)
        {
        }
    }

    public static class RouteCommand
    {
        public static async void DoTerminalCommand(CommandArg[] args)
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

                    await DoCommand(args);
                }
            }
            catch (CommandException exc)
            {
                Terminal.Log(exc.Message);
            }
            catch (Exception exc)
            {
                Terminal.Log($"{exc.Message} {exc.InnerException} {exc.StackTrace}");
            }
        }
        public async static System.Threading.Tasks.Task DoCommand(CommandArg[] args)
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


                RouteTracker routeTracker = new RouteTracker(chain, true);

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
                    trainCar = routeTracker.CurrentTask.TrainSets.FirstOrDefault()?.firstCar;
                }

                if (routeTracker.CurrentTask == null)
                {
                    throw new CommandException("No suitable task");
                }

                Track startTrack = trainCar.trainset.firstCar.Bogies[0].track.logicTrack;

                await FindAndSwitch(startTrack, routeTracker.CurrentTask.DestinationTrack, ReversingStrategy.ChooseBest, trainCar.trainset);

                routeTracker.SetRoute(Module.ActiveRoute.Route, trainCar.trainset);

                Module.ActiveRoute.RouteTracker = routeTracker;

            }
            else if (args[0].String == "tracker")
            {
                TrainCar trainCar = PlayerManager.LastLoco;

                if (trainCar == null)
                {
                    throw new CommandException("No last loco");
                }

                if (Module.ActiveRoute.RouteTracker.CurrentTask == null)
                {
                    throw new CommandException("Tracker has no task");
                }

                Track startTrack = trainCar.trainset.firstCar.Bogies[0].track.logicTrack;

                await FindAndSwitch(startTrack, Module.ActiveRoute.RouteTracker.CurrentTask.DestinationTrack, ReversingStrategy.ChooseBest, trainCar.trainset);

                Module.ActiveRoute.RouteTracker.SetRoute(Module.ActiveRoute.Route, trainCar.trainset);
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

                    if (trainCar.trainset.firstCar.logicCar.BogiesOnSameTrack)
                    {
                        args[1].String = trainCar.trainset.firstCar.logicCar.CurrentTrack.ID.FullID;
                    }
                    else
                    {
                        args[1].String = trainCar.trainset.firstCar.logicCar.FrontBogieTrack.ID.FullID;
                    }

                    trainset = trainCar.trainset;
                    Terminal.Log($"current loco track end");
                }
                else if (args[1].String == "job.trainset")
                {
                    //TODO
                }

                if (trainset == null)
                {
                    throw new CommandException("Goal track must be associated with car");
                }


                RailTrack startTrack = RailTrackRegistry.AllTracks.FirstOrDefault((RailTrack track) => track?.logicTrack.ID.FullID == args[1].String);

                RailTrack goalTrack = RailTrackRegistry.AllTracks.FirstOrDefault((RailTrack track) => track?.logicTrack.ID.FullID == args[3].String);

                RouteTaskChain chain = RouteTaskChain.FromDestination(goalTrack.logicTrack, trainset);
                var tracker = new RouteTracker(chain, false);


                if (startTrack == null || goalTrack == null)
                {
                    throw new CommandException("start track or goal track not found");
                }

                await FindAndSwitch(startTrack.logicTrack, goalTrack.logicTrack, Module.settings.ReversingStrategy, trainset);

                tracker.SetRoute(Module.ActiveRoute.Route, trainset);
                Module.ActiveRoute.RouteTracker = tracker;
            }
            else if (args[0].String == "opposite")
            {
                if ( ! Module.ActiveRoute.IsSet)
                {
                    throw new CommandException("No route active");
                }


                var route = await Module.ActiveRoute.Route.FindOppositeRoute();

                if (route != null)
                {

                    Module.ActiveRoute.Route = route;
                    route.AdjustSwitches();

                    Module.ActiveRoute.RouteTracker.SetRoute(route, route.Trainset);

                    Terminal.Log($"Route {route.Length} {route?.SecondTrack?.logicTrack.ID.FullDisplayID}");
                }
                else
                {
                    throw new CommandException("Opposite route could not be found");
                }
            }
            else if (args[0].String == "clear")
            {
                if (Module.ActiveRoute.IsSet)
                {
                    Module.ActiveRoute.ClearRoute();
                }
                else
                {
                    throw new CommandException("no active route");
                }
            }
            else if (args[0].String == "info")
            {
                if(Module.ActiveRoute.IsSet)
                {
                    Terminal.Log("Active route:");
                    Terminal.Log( Module.ActiveRoute.Route.ToString());
#if DEBUG
                    Terminal.Log( Module.ActiveRoute.Route.Path.Select(t => t.logicTrack.ID.FullID).Aggregate((i, j) => i + "->" + j) );
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
                var track = RailTrackRegistry.AllTracks.Where(t => t.logicTrack.ID.FullID.ToLower() == args[1].String.ToLower()).FirstOrDefault();
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
            else if (args[0].String == "trainset")
            {
                TrainCar trainCar = PlayerManager.LastLoco;

                if (trainCar == null)
                {
                    throw new CommandException("No last loco");
                }

                Terminal.Log($"loco trainset cars: {trainCar.trainset.cars.Count}");
                Terminal.Log($"first car: {trainCar.trainset.firstCar.logicCar.ID}");
                Terminal.Log($"last car: {trainCar.trainset.lastCar.logicCar.ID}");
                Terminal.Log($"first car track: {trainCar.trainset.firstCar.logicCar.CurrentTrack?.ID.FullDisplayID}");
                Terminal.Log($"last car track: {trainCar.trainset.lastCar.logicCar.CurrentTrack?.ID.FullDisplayID}");
                Terminal.Log($"first car front boogie track: {trainCar.trainset.firstCar.logicCar.FrontBogieTrack?.ID.FullDisplayID}");
                Terminal.Log($"first car rear boogie track: {trainCar.trainset.firstCar.logicCar.RearBogieTrack?.ID.FullDisplayID}");
            }

#endif
            else if (args[0].String == "auto")
            {
                TrainCar trainCar = PlayerManager.LastLoco;

                if (trainCar == null)
                {
                    throw new CommandException("No last loco");
                }

                if(args[1].String == "stop")
                {
                    var locoAI = Module.GetLocoAI(trainCar);
                    locoAI.Stop();
                    return;
                }

                RailTrack goalTrack = RailTrackRegistry.AllTracks.FirstOrDefault((RailTrack track) => track?.logicTrack.ID.FullID == args[1].String);
                if (goalTrack == null)
                {
                    throw new CommandException("Goal track not found");
                }

                RouteTaskChain chain = RouteTaskChain.FromDestination(goalTrack.logicTrack, trainCar.trainset);
                var tracker = new RouteTracker(chain, false);

                Track startTrack = trainCar.trainset.firstCar.Bogies[0].track.logicTrack;

                var route = await Route.FindRoute(startTrack, tracker.CurrentTask.DestinationTrack, ReversingStrategy.ChooseBest, trainCar.trainset);

                tracker.SetRoute(route, trainCar.trainset);

                var driver = Module.GetLocoAI(trainCar);
                driver.StartAI(tracker);
            }
            else
            {
                throw new CommandException($"Unknown subcommand {args[0]}");
            }

        }

        private async static System.Threading.Tasks.Task FindAndSwitch(Track begin, Track end, ReversingStrategy reversingStrategy, Trainset trainset)
        {
            if (begin is null)
            {
                throw new CommandException("Empty begin");
            }

            if (end is null)
            {
                throw new CommandException("Empty end");
            }

            var route = await Route.FindRoute(begin, end, reversingStrategy, trainset);
            if (route == null)
            {
                Module.ActiveRoute.ClearRoute();
                throw new CommandException("Path cannot be found");
            }
            else
            {
                Module.ActiveRoute.Route = route;
                route.AdjustSwitches();
            }
        }

    }
}
