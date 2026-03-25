using CommandTerminal;
using DV.Logic.Job;
using Priority_Queue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityAsync;
using UnityEngine;
using UnityModManagerNet;

namespace DVRouteManager
{

    // Prepare for loops support
    /*
    public class MultiKeyDictionary<K1, K2, V> : Dictionary<K1, Dictionary<K2, V>>
    {

        public V this[K1 key1, K2 key2]
        {
            get
            {
                if (!ContainsKey(key1) || !this[key1].ContainsKey(key2))
                    throw new ArgumentOutOfRangeException();
                return base[key1][key2];
            }
            set
            {
                if (!ContainsKey(key1))
                    this[key1] = new Dictionary<K2, V>();
                this[key1][key2] = value;
            }
        }

        public void Add(K1 key1, K2 key2, V value)
        {
            if (!ContainsKey(key1))
                this[key1] = new Dictionary<K2, V>();
            this[key1][key2] = value;
        }

        public bool ContainsKey(K1 key1, K2 key2)
        {
            return base.ContainsKey(key1) && this[key1].ContainsKey(key2);
        }

        public new IEnumerable<V> Values
        {
            get
            {
                return from baseDict in base.Values
                       from baseKey in baseDict.Keys
                       select baseDict[baseKey];
            }
        }

    }

    class NodeInfo
    {
        private Dictionary<RailTrack, double> costSoFar;

        public bool HasCameFrom(RailTrack from)
        {
            return costSoFar.ContainsKey(from);
        }
    }
    */

    public class TrackTransition
    {
        public RailTrack track;
        public RailTrack nextTrack;
    }

    public class PathFinder
    {
        public Dictionary<RailTrack, RailTrack> cameFrom;
        public Dictionary<RailTrack, double> costSoFar;

        private RailTrack start;
        private RailTrack goal;

        // ── Turntable cache ──────────────────────────────────────────────────
        // Maps spur RailTrack → the TurntableRailTrack whose rim it touches.
        // Also maps the turntable's own track → its TurntableRailTrack.
        // Built once on first pathfinding call (main thread), read on background.
        private static Dictionary<RailTrack, TurntableRailTrack> _spurToTurntable;
        public  static Dictionary<RailTrack, TurntableRailTrack> _turntableTrackToTRT;
        private static bool _turntableCacheBuilt = false;

        public static void BuildTurntableCache()
        {
            _spurToTurntable    = new Dictionary<RailTrack, TurntableRailTrack>();
            _turntableTrackToTRT = new Dictionary<RailTrack, TurntableRailTrack>();

            foreach (var trt in UnityEngine.Object.FindObjectsOfType<TurntableRailTrack>())
            {
                if (trt == null || trt.trackEnds == null) continue;

                var ttTrack = trt.Track;
                if (ttTrack != null && !_turntableTrackToTRT.ContainsKey(ttTrack))
                    _turntableTrackToTRT[ttTrack] = trt;

                foreach (var te in trt.trackEnds)
                {
                    if (te?.track != null && !_spurToTurntable.ContainsKey(te.track))
                        _spurToTurntable[te.track] = trt;
                }
            }

            _turntableCacheBuilt = true;
            Terminal.Log($"TurntableCache: {_turntableTrackToTRT.Count} turntables, {_spurToTurntable.Count} spurs");
        }
        // ─────────────────────────────────────────────────────────────────────

        // Heuristic that computes approximate distance between two rails
        protected double Heuristic(RailTrack a, RailTrack b)
        {
            return (a.transform.position - b.transform.position).sqrMagnitude; //we dont need exact distance because that result is used only as a priority
        }


        public PathFinder(Track start, Track goal)
        {
            Terminal.Log($"{start.ID.FullID} -> {goal.ID.FullID}");

            RailTrack startTrack = RailTrackRegistryBase.RailTracks.FirstOrDefault((RailTrack track) => track?.LogicTrack().ID.FullID == start.ID.FullID);
            RailTrack goalTrack = RailTrackRegistryBase.RailTracks.FirstOrDefault((RailTrack track) => track?.LogicTrack().ID.FullID == goal.ID.FullID);

            if (startTrack == null || goalTrack == null)
            {
                Terminal.Log("start track or goal track not found");
                return;
            }

            this.start = startTrack;
            this.goal = goalTrack;
        }

        public PathFinder(RailTrack start, RailTrack goal)
        {
            this.start = start;
            this.goal = goal;
        }
        public class RailTrackNode : GenericPriorityQueueNode<double>
        {
            public RailTrack track;

            public RailTrackNode(RailTrack track)
            {
                this.track = track;
            }
        }
        /// <summary>
        /// A* search
        /// </summary>
        /// <param name="allowReverse"></param>
        /// <param name="carsToIgnore"></param>
        /// <param name="consistLength"></param>
        protected async System.Threading.Tasks.Task Astar(bool allowReverse, HashSet<string> carsToIgnore, double consistLength, List<TrackTransition> bannedTransitions)
        {
            // Snapshot the yard organizer reference on the main thread before going background.
            // IsTrackManagedByOrganizer / GetReservedSpace read a Dictionary that only changes
            // during job generation (main thread, infrequent) — safe to read from background.
            YardTracksOrganizer yardOrganizer = UnityEngine.Object.FindObjectOfType<YardTracksOrganizer>();

            await Await.BackgroundSyncContext();

            cameFrom = new Dictionary<RailTrack, RailTrack>();
            costSoFar = new Dictionary<RailTrack, double>();

            //var queue = new PriorityQueue<RailTrack>();
            var queue = new GenericPriorityQueue<RailTrackNode, double>(10000);
            queue.Enqueue(new RailTrackNode(start), 0.0);

            cameFrom.Add(start, start);
            costSoFar.Add(start, 0.0);

            RailTrack current = null;
            

            while (queue.Count > 0)
            {
                current = queue.Dequeue().track;

                RailTrack prev = null;
                cameFrom.TryGetValue(current, out prev);

                string debug = $"ID: {current.LogicTrack().ID.FullID} Prev: {prev?.LogicTrack().ID.FullID}";

                List<RailTrack> neighbors = new List<RailTrack>();
                // Tracks reachable via turntable rotation — always treated as direct (no reverse needed)
                HashSet<RailTrack> turntableDirect = new HashSet<RailTrack>();

                TurntableRailTrack currentAsTurntable;
                if (_turntableTrackToTRT != null && _turntableTrackToTRT.TryGetValue(current, out currentAsTurntable))
                {
                    // Current track IS a turntable — add every spur on its rim as a neighbor
                    foreach (var te in currentAsTurntable.trackEnds)
                    {
                        if (te?.track == null) continue;
                        neighbors.Add(te.track);
                        turntableDirect.Add(te.track);
                    }
                }
                else
                {
                    if (current.outIsConnected)
                    {
                        neighbors.AddRange(current.GetAllOutBranches().Where(b => b != null).Select(b => b.track).Where(t => t != null));
                    }

                    if (current.inIsConnected)
                    {
                        neighbors.AddRange(current.GetAllInBranches().Where(b => b != null).Select(b => b.track).Where(t => t != null));
                    }

                    // Also add any turntable this spur touches, even if not currently aligned
                    TurntableRailTrack adjacentTRT;
                    if (_spurToTurntable != null && _spurToTurntable.TryGetValue(current, out adjacentTRT) && adjacentTRT?.Track != null)
                    {
                        if (!neighbors.Contains(adjacentTRT.Track))
                        {
                            neighbors.Add(adjacentTRT.Track);
                            turntableDirect.Add(adjacentTRT.Track);
                        }
                    }
                }
                string branches = DumpNodes(neighbors, current);
                debug += "\n" + $"all branches: {branches}";

#if DEBUG2
                Terminal.Log(debug);
#endif

                foreach (var neighbor in neighbors)
                {
                    if (neighbor == null) continue;

                    Track neighborLogic = neighbor.LogicTrack();
                    if (neighborLogic == null) continue;

                    if(bannedTransitions != null && bannedTransitions.Any(t=> t.track == current && t.nextTrack == neighbor))
                    {
                        Terminal.Log($"{current.LogicTrack().ID.FullID}->{neighborLogic.ID.FullID} banned");
                        continue;
                    }

                    //if non start/end track is not free omit it
                    if (neighbor != start && neighbor != goal && !neighborLogic.IsFree(carsToIgnore))
                    {
                        Terminal.Log($"{neighborLogic.ID.FullID} not free");
                        continue;
                    }

                    //if we could go through junction directly (without reversing)
                    // Turntable crossings are always direct — the table rotates to align
                    bool isDirect = turntableDirect.Contains(neighbor) || current.CanGoToDirectly(prev, neighbor);

                    if ( ! allowReverse && ! isDirect)
                    {
                        Terminal.Log($"{neighborLogic.ID.FullID} reverse needed");
                        continue;
                    }

                    // compute exact cost
                    double newCost = costSoFar[current] + neighborLogic.length / neighbor.GetAverageSpeed();

                    // Penalise routing through classified yard sidings (storage/in/out/loading).
                    // Uses YardTracksOrganizer which tracks job reservations — this means tracks
                    // reserved for unspawned cars are also penalised, not just physically occupied ones.
                    if (neighbor != start && neighbor != goal && yardOrganizer != null
                        && yardOrganizer.IsTrackManagedByOrganizer(neighborLogic))
                    {
                        double reserved = yardOrganizer.GetReservedSpace(neighborLogic);
                        // reserved > 40.5: track has active job reservations (the 40 m buffer is always present)
                        if (reserved > 40.5 || !neighborLogic.IsFree())
                            newCost += 5000.0; // occupied or reserved — likely has cars (spawned or not)
                        else
                            newCost += 300.0;  // empty classified siding — mild discourage
                    }

                    if ( ! isDirect)
                    {
                        // if we can't fit consist on this track to reverse, drop this neighbor
                        if ( prev != null && !current.IsDirectLengthEnough(prev, consistLength))
                        {
                            Terminal.Log($"{neighbor.LogicTrack().ID.FullID} not long enough to reverse");
                            continue;
                        }

                        //add penalty when we must reverse
                        
                        //newCost += 2.0 * consistLength + 30.0;
                    }

                    // If there's no cost assigned to the neighbor yet, or if the new
                    // cost is lower than the assigned one, add newCost for this neighbor
                    if ( ! costSoFar.ContainsKey(neighbor) || newCost < costSoFar[neighbor])
                    {

                        // If we're replacing the previous cost, remove it
                        if (costSoFar.ContainsKey(neighbor))
                        {
                            costSoFar.Remove(neighbor);
                            cameFrom.Remove(neighbor);
                        }

                        //Terminal.Log($"neighbor {neighbor.LogicTrack().ID.FullID} update {newCost}");

                        costSoFar.Add(neighbor, newCost);
                        cameFrom.Add(neighbor, current);
                        double priority = newCost + Heuristic(neighbor, goal)
                            / 20.0f; //convert distance to time (t = s / v)
                        queue.Enqueue(new RailTrackNode(neighbor), priority);
                    }
                }
            }

            await Await.UnitySyncContext();
        }

        private static string DumpNodes(List<RailTrack> neighbors, RailTrack parent)
        {
            return "[" + neighbors.Select(
                t =>
                {
                    string prefix = "NC";
                    
                    if(parent.outJunction != null)
                    {
                        if (parent.outJunction.inBranch.track == t)
                            prefix = "OJin";
                        if (parent.outJunction.outBranches.Any(b => b.track == t))
                            prefix = "OJout";
                    }
                    else if (parent.outIsConnected && parent.outBranch.track == t)
                    {
                        prefix = "OB";
                    }

                    if (parent.inJunction != null)
                    {
                        if (parent.inJunction.inBranch.track == t)
                            prefix = "IJin";
                        if (parent.inJunction.outBranches.Any(b => b.track == t))
                            prefix = "IJout";
                    }
                    else if (parent.inIsConnected && parent.inBranch.track == t)
                    {
                        prefix = "IB";
                    }

                    prefix += ":";
                    return prefix + t.LogicTrack().ID.FullID;
                })
                .Aggregate(string.Empty, (a, b) =>
                {
                    return a + "|" + b;
                })
                + "]";
        }

        // Return a List of Locations representing the found path
        public async Task<List<RailTrack>> FindPath(bool allowReverse, double consistLength, List<TrackTransition> bannedTransitions)
        {
            List<RailTrack> path = new List<RailTrack>();

            if (start == null || goal == null)
                return null;

            // Build turntable cache on main thread before going async
            if (!_turntableCacheBuilt)
                BuildTurntableCache();

            HashSet<string> carsToIgnore = new HashSet<string>();

            if (PlayerManager.LastLoco != null)
            {
                PlayerManager.LastLoco.trainset.cars.ForEach(c => carsToIgnore.Add(c.logicCar.ID));
            }

            await Astar(allowReverse, carsToIgnore, consistLength, bannedTransitions);

            RailTrack current = goal;
            //path.Add(current);

            while (!current.Equals(start))
            {
                if (!cameFrom.ContainsKey(current))
                {
                    Terminal.Log($"cameFrom does not contain current {current.LogicTrack().ID.FullID}");
                    return null;
                }

                path.Add(current);
                current = cameFrom[current];
            }
            
            if (path.Count > 0)
            {
                path.Add(start);
            }
            
            path.Reverse();

            return path;
        }
    }
}
