﻿using CommandTerminal;
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
        private static RouteTracker routeTracker;

        public static Route CurrentRoute
        {
            get => currentRoute;
            set
            {
                currentRoute = value;
                if (currentRoute != null)
                    PathMapMarker.DrawPathToMap(currentRoute);
                else
                {
                    RouteTracker = null;
                    PathMapMarker.DestroyPoints();
                }

            }
        }
        public static bool IsCurrentRouteSet { get => CurrentRoute != null; }

        public static RouteTracker RouteTracker { 
            get => routeTracker;
            set
            {
                if(routeTracker != null)
                {
                    routeTracker.Dispose();
                    routeTracker = null;
                }

                routeTracker = value;
            }
        }

        public static PathMapMarkers PathMapMarker { get; } = new PathMapMarkers();

        public static AudioClip stopTrainClip { get; private set; }
        public static AudioClip reverseTrainClip { get; private set; }
        public static AudioClip wrongWayClip { get; private set; }

        public static AudioSource generalAudioSource { get; private set; }

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

                stopTrainClip = AudioUtils.LoadAudioClip("stoptrain.wav", "stoptrain");
                reverseTrainClip = AudioUtils.LoadAudioClip("reversetrain.wav", "reversetrain");
                wrongWayClip = AudioUtils.LoadAudioClip("wrongway.wav", "wrongway");

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
                    string version = (string)releaseInfo?["Version"];

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

        public static IEnumerator SetupAudio()
        {
            AudioListener listener = null;

            //yield return new WaitForSeconds(5.0f);

            while (listener == null)
            {
                yield return new WaitForSeconds(0.5f);
                listener = UnityEngine.Object.FindObjectOfType<AudioListener>();
            }

            generalAudioSource = listener.gameObject.AddComponent<AudioSource>();
            //audioSource.outputAudioMixerGroup = Engine_Layered_Audio.audioMixerGroup;
            generalAudioSource.playOnAwake = true;
            generalAudioSource.loop = false;
            generalAudioSource.maxDistance = 300f;
            generalAudioSource.clip = Module.stopTrainClip;
            generalAudioSource.spatialBlend = 1f;
            generalAudioSource.dopplerLevel = 0f;
            generalAudioSource.spread = 10f;
        }

        public static void PlayClip(AudioClip clip)
        {
            if (generalAudioSource != null && clip != null)
            {
                generalAudioSource.clip = clip;
                generalAudioSource.Play();
            }
        }

        public static Coroutine StartCoroutine(IEnumerator coroutine)
        {
            MonoBehaviour mb = UnityEngine.Object.FindObjectOfType<Camera>().GetComponent<MonoBehaviour>(); //hopefuly we will have luck to choose some MonoBehaviour, that will not call stopAllCoroutines
            if (mb != null)
            {
                return mb.StartCoroutine(coroutine);
            }
            else
            {
                mod.Logger.Log("Cant start coroutines because no monobehaviour was found");
            }

            return null;
        }
        public static void StartCoroutines(IEnumerator[] coroutines)
        {
            MonoBehaviour mb = UnityEngine.Object.FindObjectOfType<Camera>().GetComponent<MonoBehaviour>(); //hopefuly we will have luck to choose some MonoBehaviour, that will not call stopAllCoroutines
            if (mb != null)
            {
                foreach (var coroutine in coroutines)
                {
                    mb.StartCoroutine(coroutine);
                }
            }
            else
            {
                mod.Logger.Log("Cant start coroutines because no monobehaviour was found");
            }
        }

        private static void StartInitCoroutines()
        {
            MonoBehaviour mb = UnityEngine.Object.FindObjectOfType<MonoBehaviour>();
            if (mb != null)
            {
                mb.StartCoroutine(SetupCommands());
                mb.StartCoroutine(SetupAudio());
                mb.StartCoroutine(CheckUpdates());
            }
            else
            {
                mod.Logger.Log("Cant start init coroutines because no monobehaviour was found");
            }
        }

        private static void StopInitCoroutines()
        {
            //this is wrong because MonoBehaviour could be different that started these coroutines
            MonoBehaviour mb = UnityEngine.Object.FindObjectOfType<MonoBehaviour>();
            if (mb != null)
            {
                mb.StopCoroutine(SetupCommands());
                mb.StopCoroutine(CheckUpdates());
            }
            else
            {
                mod.Logger.Log("Cant stop init coroutines because no monobehaviour was found");
            }
        }



        static bool OnToggle(UnityModManager.ModEntry _, bool active)
        {
            if (active)
            {
                StartInitCoroutines();
            }
            else
            {
                Terminal.Shell.Commands.Remove("route");
                StopInitCoroutines();
                //Terminal.Autocomplete.UnRegister("route"); //currently not able unregister
                CurrentRoute = null;
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
