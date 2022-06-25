using BepInEx;
using BepInEx.Configuration;
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

        public static bool changeGUID = false;

        public static string BuildGUID
        {
            get
            {
                if (!changeGUID)
                {
                    return Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId.ToString();
                }
                else
                {
                    return "bb3ef199-df63-4e99-a8a1-89a27d9e2fcb";
                }
            }
        }
        private ConfigEntry<bool> configDisplayGreeting;

        private void Awake()
        {
            instance = this;
            logger = Logger;

            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            var harmony = new Harmony("patch.ravenm");
            harmony.PatchAll();
           

            configDisplayGreeting = Config.Bind("General.Toggles",
                                                "Enable Dev Mode",
                                                false,
                                                "Change GUID to aaaa");
            changeGUID = configDisplayGreeting.Value;
            // Test code
            Logger.LogInfo("Hello, world!");
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(10, Screen.height - 20, 400, 40), $"RavenM ID: {BuildGUID}");
        }
        public void printConsole(string message)
        {
            Lua.ScriptConsole.instance.LogInfo(message);
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
