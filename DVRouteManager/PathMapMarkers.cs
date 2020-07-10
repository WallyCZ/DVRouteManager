using CommandTerminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DVRouteManager
{
    public class PathMapMarkers
    {
        private List<GameObject> points = new List<GameObject>();

        public void DestroyPoints()
        {
            foreach (var point in points)
            {
                UnityEngine.Object.Destroy(point);
            }
        }

        public void DrawPathToMap(Route route)
        {
            DestroyPoints();

            WorldMap map = (WorldMap)Resources.FindObjectsOfTypeAll(typeof(WorldMap)).FirstOrDefault();
            MapMarkersController mapController = (MapMarkersController)Resources.FindObjectsOfTypeAll(typeof(MapMarkersController)).FirstOrDefault();
            MethodInfo GetMapPositionMethod = mapController.GetType().GetMethod("GetMapPosition", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            GameObject prefab = mapController.shopMarkerPrefab;

            double totalLength = 0;
            const double step = 200;
            double next = step;
            Color color = Color.green;

            route.WalkPath((walkData) =>
            {
                double length = walkData.currentTrack.logicTrack.length;

                if(route.Reverses.ContainsKey(walkData.junctionId))
                {
                    length = Route.REVERSE_SECTOR_LENGTH;
                    color = color == Color.red ? Color.green : Color.red;
                }

                while (next > totalLength && next < (totalLength + length))
                {
                    float localDistance = Mathf.InverseLerp((float)totalLength, (float)(totalLength + length), (float)next);

                    if ((walkData.prevTrack != null && !walkData.currentTrack.IsTrackInBranch(walkData.prevTrack))
                        || (walkData.nextTrack != null && walkData.currentTrack.IsTrackInBranch(walkData.nextTrack)))
                    {
                        localDistance = 1.0f - localDistance;
                    }


                    Vector3 pointPos = walkData.currentTrack.curve.GetPointAt(localDistance); ;

                    Vector3 mapPosition = (Vector3)GetMapPositionMethod.Invoke(mapController, new object[] { pointPos - WorldMover.currentMove, map.triggerExtentsXZ });

                    GameObject point = UnityEngine.Object.Instantiate<GameObject>(prefab, map.transform);
                    point.transform.localPosition = mapPosition + Vector3.up * 0.0002f;
                    point.transform.localScale *= 0.5f;
                    MeshRenderer mr = point.GetComponent<MeshRenderer>();

                    mr.material.color = color;

                    points.Add(point);

                    next += step;
                }

                totalLength += length;
                return true;
            });
        }
    }
}
