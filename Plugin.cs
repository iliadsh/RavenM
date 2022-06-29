using BepInEx;
using HarmonyLib;
using Steamworks;
using System.Reflection;
using UnityEngine;

namespace RavenM
{
    /// <summary>
    /// Disable mods that are NOT workshop mods.
    /// </summary>
    [HarmonyPatch(typeof(ModManager), nameof(ModManager.OnGameManagerStart))]
    public class NoCustommodsPatch
    {
        static bool Prefix(ModManager __instance)
        {
            __instance.modStagingPathOverride = "NOT_REAL";
            typeof(MapEditor.MapDescriptor).GetField("DATA_PATH", BindingFlags.Static | BindingFlags.Public).SetValue(null, "NOT_REAL");
            return true;
        }
    }

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
