using HarmonyLib;
using Lua;
using Lua.Proxy;
using MoonSharp.Interpreter;
using RavenM.rspatch;
using RavenM.rspatch.Proxy;
using RavenM.RSPatch.Proxy;
using RavenM.RSPatch.Wrapper;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RavenM.RSPatch
{
    [HarmonyPatch(typeof(Registrar), nameof(Registrar.ExposeTypes))]
    public class RSPatchExposeTypes
    {
        static bool Prefix(Script script)
        {
            script.Globals["Lobby"] = typeof(WLobbyProxy);
            script.Globals["OnlinePlayer"] = typeof(WOnlinePlayerProxy);
            script.Globals["GameEventsOnline"] = typeof(RavenscriptEventsMProxy);
            script.Globals["GameObjectM"] = typeof(GameObjectMProxy);
            script.Globals["CommandManager"] = typeof(CommandManagerProxy);
            script.Globals["GameObjectNetConfig"] = typeof(GameObjectNetConfigProxy);
            return true;
        }
    }

    [HarmonyPatch(typeof(Registrar), nameof(Registrar.RegisterTypes))]
    public class RSPatchRegisterTypes
    {
        static bool Prefix()
        {
            UserData.RegisterType(typeof(WLobbyProxy), InteropAccessMode.Default, null);
            UserData.RegisterType(typeof(WOnlinePlayerProxy), InteropAccessMode.Default, null);
            UserData.RegisterType(typeof(RavenscriptEventsMProxy), InteropAccessMode.Default, null);
            Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<RavenscriptMultiplayerEvents>((Script s, RavenscriptMultiplayerEvents v) => DynValue.FromObject(s, RavenscriptEventsMProxy.New(v)));
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.UserData, typeof(RavenscriptMultiplayerEvents), (DynValue v) => v.ToObject<RavenscriptEventsMProxy>()._value);
            UserData.RegisterType(typeof(GameObjectMProxy), InteropAccessMode.Default, null);
            UserData.RegisterType(typeof(CommandManagerProxy),InteropAccessMode.Default, null);
            UserData.RegisterType(typeof(GameObjectNetConfigProxy), InteropAccessMode.Default, null);
            Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<GameObjectNetConfig>((Script s, GameObjectNetConfig v) => DynValue.FromObject(s, GameObjectNetConfigProxy.New(v)));
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.UserData, typeof(GameObjectNetConfig), (DynValue v) => v.ToObject<GameObjectNetConfigProxy>()._value);
            return true;
        }
    }

    [HarmonyPatch(typeof(Registrar), nameof(Registrar.GetProxyTypes))]
    public class RSPatchGetProxyTypes
    {
        static void Postfix(ref Type[] __result)
        {
            List<Type> proxyTypesList = new List<Type>(__result);

            proxyTypesList.Add(typeof(WLobbyProxy));
            proxyTypesList.Add(typeof(WOnlinePlayerProxy));
            proxyTypesList.Add(typeof(RavenscriptEventsMProxy));
            proxyTypesList.Add(typeof(GameObjectMProxy));
            proxyTypesList.Add(typeof(CommandManagerProxy));
            proxyTypesList.Add(typeof(GameObjectNetConfigProxy));
            __result = proxyTypesList.ToArray();
        }
    }

    [HarmonyPatch(typeof(ScriptedBehaviour), "Start")]
    public class RSPatchRemoveMutatorDuplicate
    {
        static AccessTools.FieldRef<ScriptedBehaviour, bool> isInitialized =
       AccessTools.FieldRefAccess<ScriptedBehaviour, bool>("isInitialized");

        static AccessTools.FieldRef<ScriptedBehaviour, bool> isAwake =
       AccessTools.FieldRefAccess<ScriptedBehaviour, bool>("isAwake");

        static AccessTools.FieldRef<ScriptedBehaviour, LuaClass.Method> start =
       AccessTools.FieldRefAccess<ScriptedBehaviour, LuaClass.Method>("start");

        static bool Prefix(ScriptedBehaviour __instance)
        {
            if (__instance.sourceMutator != null)
            {
                // Needed for HUDS to not be instanced every time a player joins
                if (!__instance.HasMethod("RemoveDuplicate")){
                    return true;
                }
                if (!(isInitialized(__instance) || !isAwake(__instance)))
                {
                    throw new Exception("ScriptedBehaviour started but not initialized.");
                }
                foreach (ScriptedBehaviour possibleDuplicate in GameObject.FindObjectsOfType<ScriptedBehaviour>())
                {
                    Plugin.instance.printConsole($"Checking duplicate for {__instance.gameObject.name}");
                    if (possibleDuplicate.gameObject.name == __instance.gameObject.name && possibleDuplicate.gameObject != __instance.gameObject)
                    {
                        Plugin.instance.printConsole($"Found duplicate of {__instance.gameObject.name}, removing...");
                        GameObject.Destroy(possibleDuplicate);
                    }
                }

                MethodInfo method = __instance.GetType().GetMethod("CallBuiltInMethod",
                BindingFlags.NonPublic | BindingFlags.Instance);
                method.Invoke(__instance, new object[] { start(__instance) });

                return false;
            }
            else
            {
                return true;
            }
        }
    }
    [HarmonyPatch(typeof(RavenscriptManager), "Awake")]
    public class RSPatchRavenscriptManagerAwake
    {
        static void Postfix(RavenscriptManager __instance)
        {
            __instance.gameObject.AddComponent<RavenscriptEventsManagerPatch>();
            Plugin.logger.LogInfo("Added RavenscriptEventsManagerPatch to " + __instance.gameObject.name);
        }
    }
    [HarmonyPatch(typeof(ModManager),nameof(ModManager.FinalizeLoadedModContent))]
    public class RSPatchMutatorToBuildIn
    {
        static bool Prefix(ModManager __instance)
        {
            if (Plugin.addToBuiltInMutators)
            {
                foreach (MutatorEntry entry in __instance.loadedMutators)
                {
                    if (!__instance.builtInMutators.Contains(entry))
                    {
                        __instance.builtInMutators.Add(entry);
                    }
                }
                foreach (WeaponManager.WeaponEntry weaponEntry in WeaponManager.instance.allWeapons)
                {
                    if (weaponEntry.sourceMod != ModInformation.OfficialContent)
                    {
                        WeaponManager.instance.weapons.Add(weaponEntry);
                    }
                }
            }
            return true;
        }
    }
    public class RSPatch
    {
        public static HashSet<int> OwnedObjects = new HashSet<int>();

        public static Dictionary<int, NetworkTransform> ClientObjects = new Dictionary<int, NetworkTransform>();

        public static Dictionary<int, NetworkGameObjectPacket> TargetGameObjectState = new Dictionary<int, NetworkGameObjectPacket>();
        public static void FixedUpdate(Packet packet, ProtocolReader dataStream)
        {
            switch (packet.Id)
            {
                case PacketType.CreateCustomGameObject:
                    {
                        SpawnCustomGameObjectPacket customGO_packet = dataStream.ReadSpawnCustomGameObjectPacket();
                        if (OwnedObjects.Contains(customGO_packet.GameObjectID))
                        {
                            break;
                        }
                        //Plugin.logger.LogInfo("Create Custom Game Object Packet: " + WLobby.GetNetworkPrefabByHash(customGO_packet.PrefabHash).name);
                        GameObject networkPrefab = WLobby.GetNetworkPrefabByHash(customGO_packet.PrefabHash);
                        if (networkPrefab == null)
                        {
                            Plugin.logger.LogError("Network prefab is null for " + customGO_packet.PrefabHash);
                            break;
                        }
                        GameObject InstantiatedPrefab = GameObject.Instantiate(networkPrefab);
                        InstantiatedPrefab.transform.position = customGO_packet.Position;
                        InstantiatedPrefab.transform.rotation = customGO_packet.Rotation;
                        NetworkTransform networkTransform = InstantiatedPrefab.AddComponent<NetworkTransform>();

                        networkTransform.SourceID = customGO_packet.SourceID;
                        networkTransform.GameObjectID = customGO_packet.GameObjectID;
                        ClientObjects.Add(customGO_packet.GameObjectID, networkTransform);
                        Plugin.logger.LogDebug("InstantiatedPrefab at " + networkTransform.transform.position);
                        Plugin.logger.LogDebug($"Created custom game object {networkPrefab.name} with PrefabHash {customGO_packet.PrefabHash} SourceID {customGO_packet.SourceID} GameObjectID {customGO_packet.GameObjectID}");
                    }
                    break;
                case PacketType.NetworkGameObjectsHashes:
                    {
                        NetworkGameObjectsHashesPacket syncGO_packet = dataStream.ReadSyncNetworkGameObjectsPacket();
                        Plugin.logger.LogInfo("Got syncGO packet with hashes: " + syncGO_packet.NetworkGameObjectHashes);
                        WLobby.RefreshHashes(syncGO_packet.NetworkGameObjectHashes);
                    }
                    break;
                case PacketType.ScriptedPacket:
                    ScriptedPacket scriptedPacket = dataStream.ReadScriptedPacket();
                    //var targetActor = IngameNetManager.instance.ClientActors[scriptedPacket.Id];
                    //Plugin.logger.LogInfo($"Received scripted packet {scriptedPacket.Id} Data: {scriptedPacket.Data} from {targetActor.name}");
                    Plugin.logger.LogInfo($"Received scripted packet {scriptedPacket.Id} Data: {scriptedPacket.Data}");
                    RavenscriptEventsManagerPatch.events.onReceivePacket.Invoke(scriptedPacket.Id, scriptedPacket.Data);
                    break;
                case PacketType.NetworkGameObject:
                    NetworkGameObjectPacket networkGameObjectPacket = dataStream.ReadNetworkGameObjectPacket();
                    if (OwnedObjects.Contains(networkGameObjectPacket.GameObjectID))
                        break;
                    if (!ClientObjects.ContainsKey(networkGameObjectPacket.GameObjectID))
                    {
                        Plugin.logger.LogInfo($"Could not update gameobject with id {networkGameObjectPacket.GameObjectID} SourceID: {networkGameObjectPacket.SourceID} because it was not registered");
                        break;
                    }
                    NetworkTransform networkTransformFromPacket = ClientObjects[networkGameObjectPacket.GameObjectID];
                    if (networkTransformFromPacket.gameObject == null)
                    {
                        Plugin.logger.LogInfo($"Could not update gameobject with id {networkGameObjectPacket.GameObjectID} SourceID: {networkGameObjectPacket.SourceID} because gameobject is null");
                        break;
                    }
                    
                    TargetGameObjectState[networkGameObjectPacket.GameObjectID] = networkGameObjectPacket;
                    // Only update the game objects that you spawned
                    Plugin.logger.LogInfo($"Updating pos from {networkTransformFromPacket.transform.position} to {networkGameObjectPacket.Position}");
                    // Temporary Solution
                    //networkTransformFromPacket.transform.position = networkGameObjectPacket.Position;
                    //networkTransformFromPacket.transform.rotation = networkGameObjectPacket.Rotation;
                    //networkTransformFromPacket.transform.position = Vector3.Lerp(networkTransformFromPacket.transform.position, networkGameObjectPacket.Position,Time.deltaTime);
                    //networkTransformFromPacket.transform.rotation = Quaternion.Slerp(networkTransformFromPacket.transform.rotation, networkGameObjectPacket.Rotation, Time.deltaTime);
                    //networkTransformFromPacket.transform.localScale = networkGameObjectPacket.Scale;
                    Plugin.logger.LogInfo($"Got game object update from {networkGameObjectPacket.SourceID} for {networkGameObjectPacket.GameObjectID}");
                    break;
            }
        }
    }
}
