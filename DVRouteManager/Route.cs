using CommandTerminal;
using DV.Logic.Job;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DVRouteManager
{
    public class Route
    {
        public const double REVERSE_SECTOR_LENGTH = 10.0;

        public List<RailTrack> Path { get; }
        public Track Destination { get; }
        public double Length { get; }

        public Dictionary<string, Junction> Reverses { get; } = new Dictionary<string, Junction>();

        public RailTrack FirstTrack { get => Path.FirstOrDefault(); }
        public RailTrack SecondTrack { get => Path.Skip(1).FirstOrDefault(); }
        public RailTrack LastTrack { get => Path.LastOrDefault(); }

        public Route(List<RailTrack> path, Track destination)
        {
            this.Path = path ?? throw new ArgumentNullException(nameof(path));
            this.Destination = destination ?? throw new ArgumentNullException(nameof(destination));

            IEnumerator<RailTrack> enumerator = Path.GetEnumerator();

            double length = 0.0;

            WalkPath((walkData) =>
            {
                Junction reversingJunction;

                if (walkData.nextTrack != null && !walkData.currentTrack.CanGoToDirectly(walkData.prevTrack, walkData.nextTrack, out reversingJunction))
                {
                    if (reversingJunction != null)
                    {
                        Reverses.Add(walkData.junctionId, reversingJunction);
                        Terminal.Log($"Reversing needed on junction {walkData.junctionId}");
                    }
                }

                length = walkData.distanceFromStart;

                return true;
            });

            Length = length;
        }

        public override string ToString()
        {
            return $"Route to {Destination.ID.FullDisplayID} {Length} length";
        }

        public string StartHeading
        {
            get
            {
                if (Path.Count < 2)
                    return "??";

                Vector2 v1 = new Vector2(Path[0].transform.position.x, Path[0].transform.position.z);
                Vector2 v2 = new Vector2(Path[1].transform.position.x, Path[1].transform.position.z);

                var v = (v2 - v1);

                if (Vector2.Angle(v, Vector2.up) <= 45.0)
                {
                    return "N";
                }
                else if (Vector2.Angle(v, Vector2.right) <= 45.0)
                {
                    return "E";
                }
                else if (Vector2.Angle(v, Vector2.down) <= 45.0)
                {
                    return "S";
                }


                return "W";

            }
        }

        public static string GetJunctionId(RailTrack prevTrack, RailTrack track, RailTrack nextTrack)
        {
            return $"{prevTrack?.logicTrack.ID.FullID}->{track?.logicTrack.ID.FullID}->{nextTrack?.logicTrack.ID.FullID}";
        }

        public class WalkPathData
        {
            public RailTrack prevTrack;
            public RailTrack currentTrack;
            public RailTrack nextTrack;
            public string junctionId;
            public int pathIndex;
            public double distanceFromStart;
        }

        /// <summary>
        /// Goes throuh whole path and calls callback with current, previous and next track
        /// </summary>
        /// <param name="callback"></param>
        public void WalkPath(Func<WalkPathData, bool> callback)
        {
            IEnumerator<RailTrack> enumerator = Path.GetEnumerator();

            WalkPathData walkData = new WalkPathData();
            walkData.pathIndex = -1;
            walkData.distanceFromStart = 0.0;

            if (enumerator.MoveNext())
            {
                RailTrack prevTrack = null;

                while (enumerator.Current != null)
                {
                    var track = enumerator.Current;

#if DEBUG2
                    Terminal.Log($"Track ID: {track.logicTrack.ID.FullID}");
#endif

                    RailTrack nextTrack = null;

                    if (enumerator.MoveNext())
                        nextTrack = enumerator.Current;

                    string junctionId = GetJunctionId(prevTrack, track, nextTrack);


                    walkData.prevTrack = prevTrack;
                    walkData.currentTrack = track;
                    walkData.nextTrack = nextTrack;
                    walkData.junctionId = junctionId;
                    walkData.pathIndex++;

                    if (!callback(walkData))
                    {
                        break;
                    }

                    if (nextTrack == null)
                        break;

                    if (Reverses.ContainsKey(junctionId))
                    {
                        walkData.distanceFromStart += REVERSE_SECTOR_LENGTH;
                    }
                    else
                    {
                        walkData.distanceFromStart += track.logicTrack.length;
                    }

                    prevTrack = track;
                }
            }
        }

        public void AdjustSwitches()
        {
            int count = 0;

            HashSet<Junction> junctionsForReversing = new HashSet<Junction>();

            WalkPath((walkData) =>
            {
                Junction reversingJunction = null;

                Reverses.TryGetValue(walkData.junctionId, out reversingJunction);


                if (walkData.currentTrack.inJunction != null && walkData.prevTrack != null)
                {
                    string branches = "[" + walkData.currentTrack.inJunction.outBranches.Select(b => b.track.logicTrack.ID.FullID).Aggregate((a, b) => a + "|" + b) + "]";
#if DEBUG
                        Terminal.Log($"InJunction track: {walkData.currentTrack.logicTrack.ID.FullID} nexttrack {walkData.nextTrack.logicTrack.ID.FullID} inbranch {walkData.currentTrack.inJunction.inBranch.track.logicTrack.ID.FullID} outbranches {branches} selectedBranch {walkData.currentTrack.inJunction.selectedBranch}");
#endif
                    if (!junctionsForReversing.Contains(walkData.currentTrack.inJunction) && SwitchJunctionIfNeeded(walkData.currentTrack, walkData.prevTrack, walkData.currentTrack.inJunction))
                    {
                        count++;
                    }
                }

                if (walkData.currentTrack.outJunction != null && walkData.nextTrack != null)
                {
                    string branches = "[" + walkData.currentTrack.outJunction.outBranches.Select(b => b.track.logicTrack.ID.FullID).Aggregate((a, b) => a + "|" + b) + "]";
#if DEBUG
                        Terminal.Log($"OutJunction track: {walkData.currentTrack.logicTrack.ID.FullID} nexttrack {walkData.nextTrack.logicTrack.ID.FullID} inbranch {walkData.currentTrack.outJunction.inBranch.track.logicTrack.ID.FullID} outbranches {branches} selectedBranch {walkData.currentTrack.outJunction.selectedBranch}");
#endif
                    if ( !junctionsForReversing.Contains(walkData.currentTrack.outJunction) && SwitchJunctionIfNeeded(walkData.currentTrack, walkData.nextTrack, walkData.currentTrack.outJunction))
                    {
                        count++;
                    }
                }

                if(reversingJunction != null)
                {
#if DEBUG
                    Terminal.Log($"reversing junction {reversingJunction.GetInstanceID()}");
#endif
                    junctionsForReversing.Add(reversingJunction);
                }

                return true;
            });

            Terminal.Log($"Switched {count}");

        }

        private static bool SwitchJunctionIfNeeded(RailTrack track, RailTrack nextTrack, Junction junction)
        {
            int branchIndex = -1;

            RailTrack trackToSwitch = junction.inBranch.track == track ? nextTrack : track;

            for (int i = 0; i < junction.outBranches.Count; i++)
            {
                if (junction.outBranches[i].track == trackToSwitch)
                {
                    branchIndex = i;
                    break;
                }
            }

            if (branchIndex != -1 && branchIndex != junction.selectedBranch)
            {
                Terminal.Log($"Switch {track.logicTrack.ID.FullID} -> {trackToSwitch.logicTrack.ID.FullID}");
                junction.Switch(Junction.SwitchMode.NO_SOUND);
                return true;
            }

            return false;
        }


        public WalkPathData GetPrevTrack(RailTrack currentTrack, RailTrack nextTrack)
        {
            WalkPathData result = null;

            WalkPath((walkData) =>
            {
                if (currentTrack == walkData.currentTrack && nextTrack == walkData.nextTrack)
                {
                    result = walkData;
                    return false;
                }

                return true;
            });

            return result;
        }

        public WalkPathData GetNextTrack(RailTrack currentTrack, RailTrack prevTrack)
        {
            WalkPathData result = null;

            WalkPath((walkData) =>
            {
                if (currentTrack == walkData.currentTrack && prevTrack == walkData.prevTrack)
                {
                    result = walkData;
                    return false;
                }

                return true;
            });

            return result;
        }

        public RailTrack GetPrevTrack(RailTrack currentTrack)
        {
            RailTrack result = null;

            WalkPath((walkData) =>
            {
                if (currentTrack == walkData.currentTrack)
                {
                    result = walkData.nextTrack;
                    return false;
                }

                return true;
            });

            return result;
        }

    }

}
