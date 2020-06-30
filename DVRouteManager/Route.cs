using CommandTerminal;
using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DVRouteManager
{
    public class Route
    {

        public List<RailTrack> Path { get; }
        public Track Destination { get; }
        public double Length { get; }

        public Dictionary<string, Junction> Reverses { get; } = new Dictionary<string, Junction>();

        public Route(List<RailTrack> path, Track destination)
        {
            this.Path = path ?? throw new ArgumentNullException(nameof(path));
            this.Destination = destination ?? throw new ArgumentNullException(nameof(destination));

            IEnumerator<RailTrack> enumerator = Path.GetEnumerator();

            double length = 0.0;

            WalkPath((prevTrack, track, nextTrack, junctionId) =>
            {
                Junction reversingJunction;

                if(nextTrack == null || track.CanGoToDirectly(prevTrack, nextTrack, out reversingJunction))
                {
                    length += track.logicTrack.length;
                }
                else
                {
                    if (reversingJunction != null)
                    {
                        Reverses.Add(junctionId, reversingJunction);
                        Terminal.Log($"Reversing needed on junction {junctionId}");
                    }
                    length += 10.0; //Here should be train length to be perfect
                }
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

        /// <summary>
        /// Goes throuh whole path and calls callback with current, previous and next track
        /// </summary>
        /// <param name="callback"></param>
        public void WalkPath(Action<RailTrack, RailTrack, RailTrack, string> callback)
        {
            IEnumerator<RailTrack> enumerator = Path.GetEnumerator();

            if (enumerator.MoveNext())
            {
                RailTrack prevTrack = null;

                while (enumerator.Current != null)
                {
                    var track = enumerator.Current;

#if DEBUG
                    Terminal.Log($"Track ID: {track.logicTrack.ID.FullID}");
#endif

                    RailTrack nextTrack = null;

                    if (enumerator.MoveNext())
                        nextTrack = enumerator.Current;

                    string junctionId = $"{prevTrack?.logicTrack.ID.FullID}->{track?.logicTrack.ID.FullID}->{nextTrack?.logicTrack.ID.FullID}";

                    callback(prevTrack, track, nextTrack, junctionId);

                    if (nextTrack == null)
                        break;

                    prevTrack = track;
                }
            }
        }

        public void AdjustSwitches()
        {
            int count = 0;
            
            WalkPath((prevTrack, track, nextTrack, junctionId) =>
            {
                int switchedCount = 0;

                Junction reversingJunction = null;

                Reverses.TryGetValue(junctionId, out reversingJunction);


                if (track.inJunction != null && prevTrack != null)
                {
                   string branches = "[" + track.inJunction.outBranches.Select(b => b.track.logicTrack.ID.FullID).Aggregate((a, b) => a + "|" + b) + "]";
#if DEBUG
                        Terminal.Log($"InJunction track: {track.logicTrack.ID.FullID} nexttrack {nextTrack.logicTrack.ID.FullID} inbranch {track.inJunction.inBranch.track.logicTrack.ID.FullID} outbranches {branches} selectedBranch {track.inJunction.selectedBranch}");
#endif
                    if (reversingJunction != track.inJunction && SwitchJunctionIfNeeded(track, prevTrack, track.inJunction))
                    {
                       count++;
                    }
                }

                if (track.outJunction != null && nextTrack != null)
                {
                   string branches = "[" + track.outJunction.outBranches.Select(b => b.track.logicTrack.ID.FullID).Aggregate((a, b) => a + "|" + b) + "]";
#if DEBUG
                        Terminal.Log($"OutJunction track: {track.logicTrack.ID.FullID} nexttrack {nextTrack.logicTrack.ID.FullID} inbranch {track.outJunction.inBranch.track.logicTrack.ID.FullID} outbranches {branches} selectedBranch {track.outJunction.selectedBranch}");
#endif
                    if (reversingJunction != track.outJunction && SwitchJunctionIfNeeded(track, nextTrack, track.outJunction))
                    {
                       count++;
                    }
                }
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

    }
}
