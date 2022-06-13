using BepInEx;
using HarmonyLib;
using Steamworks;
using System.Reflection;
using UnityEngine;

namespace RavenM
{
    /// <summary>
    /// Removing this patch will take a LOT of effort. Syncing mods is not
    /// something I wish to tackle right now.
    /// 
    /// Well... if everyone has the same mods (the EXACT same), then its
    /// *probably* safe to remove this.
    /// </summary>
    //[HarmonyPatch(typeof(ModManager), nameof(ModManager.OnGameManagerStart))]
    //public class NoModsPatch
    //{
    //    static bool Prefix(ModManager __instance)
    //    {
    //        __instance.noWorkshopMods = true;
    //        __instance.builtInMutators.Clear();
    //        __instance.noContentMods = true;
    //        return true;
    //    }
    //}

    public class GuidComponent : MonoBehaviour
    {
        public int guid; //TODO: Replace with System.GUID?
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        public bool FirstSteamworksInit = false;

        public static Plugin instance = null;

        public static BepInEx.Logging.ManualLogSource logger = null;

        private void Awake()
        {
            instance = this;
            logger = Logger;

            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            var harmony = new Harmony("patch.ravenm");
            harmony.PatchAll();

            ProtoBuf.Meta.RuntimeTypeModel.Default.Add(typeof(Vector2), false).Add("x", "y");
            ProtoBuf.Meta.RuntimeTypeModel.Default.Add(typeof(Vector3), false).Add("x", "y", "z");
            ProtoBuf.Meta.RuntimeTypeModel.Default.Add(typeof(Vector4), false).Add("x", "y", "z", "w");
            ProtoBuf.Meta.RuntimeTypeModel.Default.Add(typeof(Quaternion), false).Add("x", "y", "z", "w");
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(10, Screen.height - 20, 400, 40), $"RavenM ID: {Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId}");
        }

        void Update()
        {
            if (!SteamManager.Initialized)
                return;

            SteamAPI.RunCallbacks();

            if (!FirstSteamworksInit)
            {
                FirstSteamworksInit = true;

                var lobbyObject = new GameObject();
                lobbyObject.AddComponent<LobbySystem>();
                DontDestroyOnLoad(lobbyObject);

                var netObject = new GameObject();
                netObject.AddComponent<IngameNetManager>();
                DontDestroyOnLoad(netObject);
            }
        }
    }
}
