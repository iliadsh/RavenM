using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Steamworks;
using System.Collections.Generic;
using System.Linq;
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
            string path = "NOT_REAL";
            if (Plugin.addToBuiltInMutators)
            {
                path = Plugin.customBuildInMutators;
                __instance.noContentMods = false;
                __instance.noWorkshopMods = true;
            }
            __instance.modStagingPathOverride = path;
            typeof(MapEditor.MapDescriptor).GetField("DATA_PATH", BindingFlags.Static | BindingFlags.Public).SetValue(null, path);
            return true;
        }
    }

    public class GuidComponent : MonoBehaviour
    {
        public int guid; //TODO: Replace with System.GUID?
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("RavenM.Updater", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {

        public bool FirstSteamworksInit = false;

        public static Plugin instance = null;

        public static BepInEx.Logging.ManualLogSource logger = null;

        public static bool changeGUID = false;

        public static bool addToBuiltInMutators = false;
        public static string customBuildInMutators;
        public static List<string> customMutatorsDirectories = new List<string>();

        public static bool JoinedLobbyFromArgument = false;
        public static Dictionary<string, string> Arguments = new Dictionary<string, string>();

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
        private ConfigEntry<bool> configRavenMDevMod;
        private ConfigEntry<bool> configRavenMAddToBuiltInMutators;
        private ConfigEntry<string> configRavenMBuiltInMutatorsDirectory;
        private void Awake()
        {
            instance = this;
            logger = Logger;

            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            configRavenMDevMod = Config.Bind("General.Toggles",
                                                "Enable Dev Mode",
                                                false,
                                                "Change GUID to bb3ef199-df63-4e99-a8a1-89a27d9e2fcb");
            configRavenMAddToBuiltInMutators = Config.Bind("General.Toggles",
                "Enable Custom Build In Mutators",
                false,
                "Add Directory in General.BuildInMutators");

            configRavenMBuiltInMutatorsDirectory = Config.Bind("General.BuildInMutators",
                                                                "Directory",
                                                                "",
                                                                "The mutators in the folder will be added automatically as Build In Mutators, this is for testing mutators without having to start the game with mods.");

            changeGUID = configRavenMDevMod.Value;
            addToBuiltInMutators = configRavenMAddToBuiltInMutators.Value;
            customBuildInMutators = configRavenMBuiltInMutatorsDirectory.Value;
            if (System.IO.Directory.Exists(customBuildInMutators))
            {
                Plugin.logger.LogInfo("Added Custom Build In Mutator Directory " + customBuildInMutators);
            }
            else
            {
                customBuildInMutators = "NOT_REAL";
                Plugin.logger.LogError($"Directory {customBuildInMutators} could not be found.");
            }
            var harmony = new Harmony("patch.ravenm");
            harmony.PatchAll();
            
            string[] args = Environment.GetCommandLineArgs();
            foreach (var argument in args)
            {
                if (argument.Contains("="))
                {
                    string[] argumentVals = argument.Split('=');
                    string argumentName = argumentVals[0];
                    string argumentValue = argumentVals[1];
                    Arguments.Add(argumentName, argumentValue);
                }
                else
                {
                    Arguments.Add(argument, "");
                }
            }
            
            // Test code
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

                var chatObject = new GameObject();
                chatObject.AddComponent<ChatManager>();
                DontDestroyOnLoad(chatObject);

                var netObject = new GameObject();
                netObject.AddComponent<IngameNetManager>();
                DontDestroyOnLoad(netObject);

                var discordObject = new GameObject();
                discordObject.AddComponent<DiscordIntegration>();
                DontDestroyOnLoad(discordObject);
            }
            else if (!JoinedLobbyFromArgument && Arguments.ContainsKey("-ravenm-lobby"))
            {
                JoinLobbyFromArgument();
            }
        }

        void JoinLobbyFromArgument()
        {
            JoinedLobbyFromArgument = true;
            CSteamID lobbyId = new CSteamID(ulong.Parse(Arguments["-ravenm-lobby"]));
            SteamMatchmaking.JoinLobby(lobbyId);
            LobbySystem.instance.InLobby = true;
            LobbySystem.instance.IsLobbyOwner = false;
            LobbySystem.instance.LobbyDataReady = false;
        }
    }
}
