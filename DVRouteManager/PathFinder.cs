using CommandTerminal;
using DV.Logic.Job;
using Priority_Queue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
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


    public class PathFinder
    {
        public Dictionary<RailTrack, RailTrack> cameFrom;
        public Dictionary<RailTrack, double> costSoFar;

        private RailTrack start;
        private RailTrack goal;

        // Heuristic that computes approximate distance between two rails
        protected double Heuristic(RailTrack a, RailTrack b)
        {
            return (a.transform.position - b.transform.position).sqrMagnitude; //we dont need exact distance because that result is used only as a priority
        }


        public PathFinder(Track start, Track goal)
        {
            Terminal.Log($"{start.ID.FullID} -> {goal.ID.FullID}");

            RailTrack startTrack = TrackFinder.AllTracks.FirstOrDefault((RailTrack track) => track?.logicTrack.ID.FullID == start.ID.FullID);
            RailTrack goalTrack = TrackFinder.AllTracks.FirstOrDefault((RailTrack track) => track?.logicTrack.ID.FullID == goal.ID.FullID);

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
        // A* search
        public void AStart(bool allowReverse, HashSet<string> carsToIgnore)
        {
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
                // Get the Location from the frontier that has the lowest
                // priority, then remove that Location from the frontier
                current = queue.Dequeue().track;

                // If we're at the goal Location, stop looking.
                //if (current.Equals(goal)) break;

                RailTrack prev = null;
                cameFrom.TryGetValue(current, out prev);

                string debug = $"ID: {current.logicTrack.ID.FullID} Prev: {prev?.logicTrack.ID.FullID}";

                List<RailTrack> neighbors = new List<RailTrack>();

                if (current.outIsConnected)
                {
                    neighbors.AddRange(current.GetAllOutBranches().Select(b => b.track));
                }

                if (current.inIsConnected)
                {
                    neighbors.AddRange(current.GetAllInBranches().Select(b => b.track));
                }
                string branches = DumpNodes(neighbors, current);
                debug += "\n" + $"all branches: {branches}";

                if (!allowReverse)
                {
                    // we must remove branches, where the path involves reversing
                    neighbors = neighbors.Where(t => current.CanGoToDirectly(prev, t)).ToList();
                    if (neighbors.Count > 0)
                    {
                        branches = DumpNodes(neighbors, current);
                        debug += "\n" + $"reduced branches: {branches}";
                    }
                    else
                    {
                        debug += "\n" + $"no branches after reducing";
                    }
                }

               
#if DEBUG
                Terminal.Log(debug);
#endif


                foreach (var neighbor in neighbors)
                {
                    //if track is not free omit it
                    if (neighbor != start && neighbor != goal && !neighbor.IsFree(carsToIgnore))
                    {
                        Terminal.Log($"{neighbor.logicTrack.ID.FullID} not free");
                        continue;
                    }

                    // compute exact cost
                    double newCost = costSoFar[current] + neighbor.logicTrack.length;

                    // If there's no cost assigned to the neighbor yet, or if the new
                    // cost is lower than the assigned one, add newCost for this neighbor
                    if (!costSoFar.ContainsKey(neighbor) || newCost < costSoFar[neighbor])
                    {

                        // If we're replacing the previous cost, remove it
                        if (costSoFar.ContainsKey(neighbor))
                        {
                            costSoFar.Remove(neighbor);
                            cameFrom.Remove(neighbor);
                        }

                        //Terminal.Log($"neighbor {neighbor.logicTrack.ID.FullID} update {newCost}");

                        costSoFar.Add(neighbor, newCost);
                        cameFrom.Add(neighbor, current);
                        double priority = newCost + Heuristic(neighbor, goal);
                        queue.Enqueue(new RailTrackNode(neighbor), priority);
                    }
                }
            }

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
                    return prefix + t.logicTrack.ID.FullID;
                })
                .Aggregate((a, b) =>
                {
                    return a + "|" + b;
                })
                + "]";
        }

        // Return a List of Locations representing the found path
        public List<RailTrack> FindPath(bool allowReverse)
        {
            List<RailTrack> path = new List<RailTrack>();

            if (start == null || goal == null)
                return path;

            HashSet<string> carsToIgnore = new HashSet<string>();

            if (PlayerManager.LastLoco != null)
                carsToIgnore.Add(PlayerManager.LastLoco.logicCar.ID);

            AStart(allowReverse, carsToIgnore);

            RailTrack current = goal;
            //path.Add(current);

            while (!current.Equals(start))
            {
                if (!cameFrom.ContainsKey(current))
                {
                    Terminal.Log($"cameFrom does not contain current {current.logicTrack.ID.FullID}");
                    return new List<RailTrack>();
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
