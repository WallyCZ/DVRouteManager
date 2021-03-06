﻿using CommandTerminal;
using DV;
using DV.Logic.Job;
using DV.Teleporters;
using DVRouteManager.CommsRadio;
using HarmonyLib;
using SimpleJson;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Schema;
using UnityAsync;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityModManagerNet;
using static UnityModManagerNet.UnityModManager;

namespace DVRouteManager
{
#if DEBUG
    [EnableReloading]
#endif
    static class Module
    {
        private const string AUDIO_DIRECTORY = "audio\\";
        public static UnityModManager.ModEntry mod;
        public static Settings settings;

        public static ActiveRoute ActiveRoute { get; private set; }

        public static AudioClip stopTrainClip { get; private set; }
        public static AudioClip trainEnd { get; private set; }
        public static AudioClip wrongWayClip { get; private set; }
        public static AudioClip offClip { get; private set; }
        public static AudioClip onClip { get; private set; }
        public static AudioClip setClip { get; private set; }
        public static AudioSource generalAudioSource { get; private set; }

        public static string ModulePath
        {
            get
            {
                return mod.Path;
            }
        }

        private static Dictionary<string, LocoAI> locosAI = new Dictionary<string, LocoAI>();
        public static LocoAI GetLocoAI(TrainCar car)
        {
            LocoAI locoAI;
            if (!locosAI.TryGetValue(car.logicCar.ID, out locoAI))
            {
                DieselLocoSimulation dieselSim = car.GetComponent<DieselLocoSimulation>();
                if (dieselSim != null)
                {
                    if (!dieselSim.engineOn)
                    {
                        throw new CommandException("Engine off");
                    }
                }
                else
                {
                    ShunterLocoSimulation shunterSim = car.GetComponent<ShunterLocoSimulation>();
                    if (shunterSim != null)
                    {
                        if (!shunterSim.engineOn)
                        {
                            throw new CommandException("Engine off");
                        }

                    }
                    else
                    {
                        throw new CommandException("Loco not compatible");
                    }
                }

                LocoControllerShunter shunterController = car.GetComponent<LocoControllerShunter>();
                ILocomotiveRemoteControl remote = car.GetComponent<ILocomotiveRemoteControl>();
                locoAI = new LocoAI(remote);
                locosAI.Add(car.logicCar.ID, locoAI);
            }

            return locoAI;
        }

        public class VersionInfo
        {
            public string Version;
            public string downloadUrl;
        }
        public static VersionInfo VersionForUpdate { get; private set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
        static bool Load(UnityModManager.ModEntry modEntry)
        {
            try
            {
                settings = Settings.Load<Settings>(modEntry);
                mod = modEntry;
                mod.OnToggle = OnToggle;
                mod.OnUpdate = OnUpdate;
                mod.OnGUI = OnGUI;
                mod.OnSaveGUI = OnSaveGUI;
#if DEBUG
                modEntry.OnUnload = Unload;
#endif
                var harmony = new Harmony(modEntry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());

                AsyncManager.Initialize();

                ActiveRoute = new ActiveRoute();

                modEntry.Logger.Log("RouteManager initialized");

                Terminal.Log($"Load, audio source {generalAudioSource}");
            }
            catch (Exception exc)
            {
                modEntry.Logger.LogException(exc);
            }

            return true;
        }

#if DEBUG
        static bool Unload(UnityModManager.ModEntry modEntry)
        {
            //Before unloading OnToggle with active = false is called
            return true;
        }
#endif

        private static void OnSaveGUI(ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        private static void OnGUI(ModEntry modEntry)
        {
            settings.Draw(modEntry);
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
                    Version latestVersion = new Version(version);
                    Version moduleVersion = new Version(mod.Info.Version);

                    if (latestVersion > moduleVersion)
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

            stopTrainClip = AudioUtils.LoadAudioClip(AUDIO_DIRECTORY + "stoptrain.wav", "stoptrain");
            trainEnd = AudioUtils.LoadAudioClip(AUDIO_DIRECTORY + "trainend.wav", "trainend");
            wrongWayClip = AudioUtils.LoadAudioClip(AUDIO_DIRECTORY + "wrongway.wav", "wrongway");
            onClip = AudioUtils.LoadAudioClip(AUDIO_DIRECTORY + "on.wav", "on");
            offClip = AudioUtils.LoadAudioClip(AUDIO_DIRECTORY + "off.wav", "off");
            setClip = AudioUtils.LoadAudioClip(AUDIO_DIRECTORY + "set.wav", "set");

            Terminal.Shell.Commands.Remove("route");
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
                yield return new UnityEngine.WaitForSeconds(0.5f);
                listener = UnityEngine.Object.FindObjectOfType<AudioListener>();
            }

            SetupAudioSource(listener);

#if DEBUG
            Terminal.Log($"AudioListener found {generalAudioSource}");
            mod.Logger.Log("AudioListener found");
#endif
        }

        private static void SetupAudioSource(AudioListener listener)
        {
            generalAudioSource = listener.gameObject.AddComponent<AudioSource>();
            //audioSource.outputAudioMixerGroup = Engine_Layered_Audio.audioMixerGroup;
            generalAudioSource.playOnAwake = true;
            generalAudioSource.loop = false;
            generalAudioSource.maxDistance = 300f;
            //generalAudioSource.clip = Module.stopTrainClip;
            generalAudioSource.spatialBlend = 1f;
            generalAudioSource.dopplerLevel = 0f;
            generalAudioSource.spread = 10f;
        }

        public static void PlayClip(AudioClip clip)
        {
            if (generalAudioSource == null)
            {
                AudioListener listener = UnityEngine.Object.FindObjectOfType<AudioListener>();
#if DEBUG
                Terminal.Log("PlayClip Init #2");
#endif
                SetupAudioSource(listener);
            }

            if (generalAudioSource != null && clip != null)
            {
                generalAudioSource.clip = clip;
                generalAudioSource.Play();
            }
            else if(clip == null)
            {
                Terminal.Log("Cannot play sound, clip == null");
            }
            else if (generalAudioSource == null)
            {
                Terminal.Log("Cannot play sound, generalAudioSource == null");
            }
        }

        public static Coroutine StartCoroutine(IEnumerator coroutine)
        {
            return AsyncManager.StartCoroutine(coroutine);
        }
        public static void StartCoroutines(IEnumerator[] coroutines)
        {
            foreach (var coroutine in coroutines)
            {
                AsyncManager.StartCoroutine(coroutine);
            }
        }

        private static void StartInitCoroutines()
        {
            AsyncManager.StartCoroutine(SetupCommands());
            AsyncManager.StartCoroutine(SetupAudio());
            AsyncManager.StartCoroutine(CheckUpdates());
        }

        private static void StopInitCoroutines()
        {
        }



        static bool OnToggle(UnityModManager.ModEntry _, bool active)
        {
            if (active)
            {
                StartInitCoroutines();
                AddCommsRouteManager();
            }
            else
            {
                Deactivate();
            }

            return true;
        }

        private static void Deactivate()
        {
            Terminal.Log("RouteManager deactivating");

            RemoveCommsRouteManager();
            Terminal.Shell.Commands.Remove("route");
            StopInitCoroutines();
            //Terminal.Autocomplete.UnRegister("route"); //currently not able unregister
            Module.ActiveRoute.ClearRoute();
        }

        private static void OnUpdate(ModEntry arg1, float arg2)
        {
            if (Module.ActiveRoute.IsSet && Module.ActiveRoute.RouteTracker != null && Module.settings.TrainEndAlarm.Down())
            {
                Module.ActiveRoute.RouteTracker.NotifyTrainEnd();
            }

            if (Module.settings.CruiseControlToggle.Down())
            {
                LocoCruiseControl.ToggleCruiseControl();
            }

            if (Module.settings.CruiseControl30.Down())
            {
                LocoCruiseControl.ToggleCruiseControl(30.0f);
            }

            if (Module.settings.CruiseControl60.Down())
            {
                LocoCruiseControl.ToggleCruiseControl(60.0f);
            }

            if (Module.settings.CruiseControlMinus.Down())
            {
                LocoCruiseControl.UpdateTargetSpeed(-5.0f);
            }

            if (Module.settings.CruiseControlPlus.Down())
            {
                LocoCruiseControl.UpdateTargetSpeed(+5.0f);
            }
        }


        static CommsRadioController commsRadioController;
        static CommsRouteManager commsRouteManager;

        private static void AddCommsRouteManager()
        {

            if (commsRadioController == null)
            {
                commsRadioController = Resources.FindObjectsOfTypeAll<CommsRadioController>().FirstOrDefault();
                if (commsRadioController == null)
                {
                    Module.mod.Logger.Log("commsRadioController empty");
                    return;
                }
            }

            try
            {
                var modes = Traverse.Create(commsRadioController).Field("allModes").GetValue<List<ICommsRadioMode>>();

                if (modes != null && modes.Count > 0)
                {
                    GameObject objToSpawn = new GameObject("CommsRouteManager");
                    objToSpawn.AddComponent<CommsRouteManager>();

                    CommsRouteManager radioMode = objToSpawn.GetComponent<CommsRouteManager>();
                    radioMode.display = Traverse.Create(modes[0]).Field("display").GetValue<CommsRadioDisplay>();
                    objToSpawn.transform.parent = (modes[0] as MonoBehaviour).gameObject.transform;
                    objToSpawn.SetActive(true);
                    modes.Add(radioMode);
                    commsRouteManager = radioMode;
                    Module.mod.Logger.Log("Comm radio mode added");
                }
                else
                {
                    Module.mod.Logger.Log($"No commsradio modes found");
                }

            }
            catch (Exception e)
            {
                Module.mod.Logger.Log("Error in mod CommsRadioController registration: " + e.Message);
            }
        }

        private static void RemoveCommsRouteManager()
        {

            if (commsRadioController == null)
            {
                Module.mod.Logger.Log("commsRadioController empty");
                return;
            }

            try
            {
                var modes = Traverse.Create(commsRadioController).Field("allModes").GetValue<List<ICommsRadioMode>>();

                if (modes != null && commsRouteManager != null)
                {
                    modes.Remove(commsRouteManager);
                    Module.mod.Logger.Log("Comm radio mode removed");
                }
                else
                {
                    Module.mod.Logger.Log($"No commsradio modes found");
                }

            }
            catch (Exception e)
            {
                Module.mod.Logger.Log("Error in mod CommsRadioController removing: " + e.Message);
            }
        }


        [HarmonyPatch(typeof(CommsRadioController), "Awake")]
        static class TrainSpawnerPlus_Patch
        {
            static void Postfix(CommsRadioController __instance)
            {
                commsRadioController = __instance;
                Module.mod.Logger.Log("commsRadioController set");
                AddCommsRouteManager();
            }
        }
    }
}
