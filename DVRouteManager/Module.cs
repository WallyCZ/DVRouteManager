using CommandTerminal;
using DV;
using DV.Logic.Job;
using DV.Teleporters;
using HarmonyLib;
using SimpleJson;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Schema;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityModManagerNet;
using static UnityModManagerNet.UnityModManager;

namespace DVRouteManager
{
    static class Module
    {
        public static UnityModManager.ModEntry mod;
        private static Route currentRoute;

        public static Route CurrentRoute
        {
            get => currentRoute;
            set
            {
                currentRoute = value;
                if (currentRoute != null)
                    PathMapMarker.DrawPathToMap(currentRoute);
                else
                    PathMapMarker.DestroyPoints();

            }
        }
        public static bool IsCurrentRouteSet { get => CurrentRoute != null; }

        private static RouteTracker RouteTracker { get; } = new RouteTracker();

        public static PathMapMarkers PathMapMarker { get; } = new PathMapMarkers();


        public class VersionInfo
        {
            public string Version;
            public string downloadUrl;
        }
        public static VersionInfo VersionForUpdate { get; private set; }


        static bool Load(UnityModManager.ModEntry modEntry)
        {
            try
            {
                mod = modEntry;
                mod.OnToggle = OnToggle;

                var harmony = new Harmony(modEntry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception exc)
            {
                modEntry.Logger.LogException(exc);
            }

            return true;
        }
        public static IEnumerator CheckUpdates()
        {
            while (Terminal.Shell == null || Terminal.Autocomplete == null)
            {
                yield return null;
            }

            UnityWebRequest www = null;

            try
            {
                www = UnityWebRequest.Get(mod.Info.Repository);
                www.downloadHandler = new DownloadHandlerBuffer();
            }
            catch (Exception e)
            {
                Terminal.Log(e.Message + " " + e.StackTrace);
            }

            if (www != null)
            {
                yield return www.SendWebRequest();

                while (!www.downloadHandler.isDone)
                    yield return null;

                if (!www.isHttpError && !www.isNetworkError)
                {
                    var json = (IDictionary<string, object>)SimpleJson.SimpleJson.DeserializeObject(www.downloadHandler.text);

                    JsonObject releaseInfo = ((json["Releases"] as JsonArray)?[0] as JsonObject);
                    string version = (string) releaseInfo?["Version"];

                    if (version != mod.Info.Version)
                    {
                        VersionForUpdate = new VersionInfo();
                        VersionForUpdate.Version = version;
                        VersionForUpdate.downloadUrl = (string)releaseInfo["DownloadUrl"]; ;
                        Terminal.Log($"{version} {VersionForUpdate.downloadUrl}");
                    }

                }
            }

        }
        public static IEnumerator SetupCommands()
        {
            while (Terminal.Shell == null || Terminal.Autocomplete == null)
            {
                yield return null;
            }

            Terminal.Shell.AddCommand("route", RouteCommand.DoTerminalCommand, 0, -1, "", null);
            Terminal.Autocomplete.Register("route");
            Terminal.Log("Route command registered");
        }
        public static IEnumerator SetupLocoPositionUpdate()
        {
            string lastTrackFullID = "";

            while (true)
            {
                if (PlayerManager.LastLoco != null)
                {
                    RailTrack track = PlayerManager.LastLoco.Bogies[0].track;
                    if (track != null && track.logicTrack.ID.FullID != lastTrackFullID)
                    {
                        lastTrackFullID = track.logicTrack.ID.FullID;
                        RouteTracker.UpdateCurrentTrack(track, null);
                    }
                }
                yield return null;
            }
        }

        private static void StartCoroutines()
        {
            MonoBehaviour mb = UnityEngine.Object.FindObjectOfType<MonoBehaviour>();
            if (mb != null)
            {
                mb.StartCoroutine(SetupCommands());
                mb.StartCoroutine(SetupLocoPositionUpdate());
                mb.StartCoroutine(CheckUpdates());
            }
            else
            {
                mod.Logger.Log("Cant start coroutines because no monobehaviour was found");
            }
        }

        private static void StopCoroutines()
        {
            MonoBehaviour mb = UnityEngine.Object.FindObjectOfType<MonoBehaviour>();
            if (mb != null)
            {
                mb.StopCoroutine(SetupCommands());
                mb.StopCoroutine(SetupLocoPositionUpdate());
                mb.StopCoroutine(CheckUpdates());
            }
            else
            {
                mod.Logger.Log("Cant stop coroutines because no monobehaviour was found");
            }
        }



        static bool OnToggle(UnityModManager.ModEntry _, bool active)
        {
            if (active)
            {
                StartCoroutines();
            }
            else
            {
                Terminal.Shell.Commands.Remove("route");
                StopCoroutines();
                //Terminal.Autocomplete.UnRegister("route"); //currently not able unregister
            }

            return true;
        }

    }

    [HarmonyPatch(typeof(CommsRadioController), "Awake")]
    static class TrainSpawnerPlus_Patch
    {
        static void Postfix(CommsRadioController __instance)
        {
            try
            {
                var modes = Traverse.Create(__instance).Field("allModes").GetValue<List<ICommsRadioMode>>();
                Terminal.Log($"{modes.Count} modes found");

                if (modes.Count > 0)
                {
                    GameObject objToSpawn = new GameObject("CommsRouteManager");
                    objToSpawn.AddComponent<CommsRouteManager>();

                    CommsRouteManager radioMode = objToSpawn.GetComponent<CommsRouteManager>();
                    radioMode.display = Traverse.Create(modes[0]).Field("display").GetValue<CommsRadioDisplay>();
                    objToSpawn.transform.parent = (modes[0] as MonoBehaviour).gameObject.transform;
                    objToSpawn.SetActive(true);
                    modes.Add(radioMode);
                }

            }
            catch (Exception e)
            {
                Terminal.Log("Error in mod CommsRadioController registration: " + e.Message);
            }
        }
    }

}
