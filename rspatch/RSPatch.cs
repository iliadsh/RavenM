using HarmonyLib;
using Lua;
using Lua.Proxy;
using Lua.Wrapper;
using MoonSharp.Interpreter;
using RavenM.RSPatch.Proxy;
using RavenM.RSPatch.Wrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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


    //[HarmonyPatch(typeof(RavenscriptManager))]
    //[HarmonyPatch("events", MethodType.Getter)]
    //public sealed class RSPatchRavenscriptManagerEvents
    //{
    //    static AccessTools.FieldRef<RavenscriptManager, ScriptEngine> _engine =
    //AccessTools.FieldRefAccess<RavenscriptManager, ScriptEngine>("_engine");

    //    private static RSPatchRavenscriptManagerEvents instance = null;
    //    private static readonly object padlock = new object();
    //    [HarmonyPrefix]
    //    public static bool returnEvents(ref RavenscriptEvents __result)
    //    {
    //        //Replace get with my event Class that has the same events as the default one
    //        //lock (padlock)
    //        //{
    //        //    if (instance == null)
    //        //    {
    //        //        instance = new RSPatchRavenscriptManagerEvents();


    //        //    }
    //        //}
    //        //Plugin.logger.LogMessage(" awdw " + RSPatchRavenscriptManagerAwake._events2.ToString());
    //        //__result = RSPatchRavenscriptManagerAwake._events2;
    //        return true;
    //    }
    //    //RSPatchRavenscriptManagerEvents()
    //    //{
    //    //}
    //    //public static RSPatchRavenscriptManagerEvents Instance
    //    //{
    //    //    get
    //    //    {
    //    //        lock (padlock)
    //    //        {
    //    //            if (instance == null)
    //    //            {
    //    //                instance = new RSPatchRavenscriptManagerEvents();
    //    //            }
    //    //            return instance;
    //    //        }
    //    //    }
    //    //}

    //    //public static RavenscriptMultiplayerEvents events
    //    //{
    //    //    get
    //    //    {
    //    //        return RSPatchRavenscriptManagerEvents.instance._events2;
    //    //    }
    //    //}

    //    //static void Postfix(RavenscriptManager __instance) {
    //    //    lock (padlock)
    //    //    {
    //    //        if (instance == null)
    //    //        {
    //    //            instance = new RSPatchRavenscriptManagerEvents();
    //    //        }
    //    //    }
    //    //    //Plugin.logger.LogInfo("before Events global value -> " + _engine(__instance).GetProcessorStats());
    //    //    //RSPatchRavenscriptManagerEvents.Instance._events2 = _engine(__instance).gameObject.AddComponent<RavenscriptMultiplayerEvents>();
    //    //    //Plugin.instance.printConsole("_events " + RSPatchRavenscriptManagerEvents.Instance._events2.gameObject);
    //    //    //_engine(__instance).Set(NameAttribute.GetName(typeof(RavenscriptMultiplayerEvents)), RSPatchRavenscriptManagerEvents.events);
    //    //    //Plugin.logger.LogInfo("after Events global value -> " + _engine(__instance).GetProcessorStats());
    //    //}
    //}
    [HarmonyPatch(typeof(RavenscriptManager), "Awake")]
    public class RSPatchRavenscriptManagerAwake
    {
        static void Postfix(RavenscriptManager __instance)
        {
            __instance.gameObject.AddComponent<RavenscriptEventsManagerPatch>();
            Plugin.logger.LogInfo("Added RavenscriptEventsManagerPatch to " + __instance.gameObject.name);
        }
    }
    public class RSPatch
    {
        public static void FixedUpdate(Packet packet, ProtocolReader dataStream)
        {
            switch (packet.Id)
            {
                case PacketType.CreateCustomGameObject:
                    {
                        SpawnCustomGameObjectPacket customGO_packet = dataStream.ReadSpawnCustomGameObjectPacket();
                        Plugin.logger.LogInfo("Create Custom Game Object Packet: " + WLobby.GetNetworkPrefabByHash(customGO_packet.PrefabHash).name);
                        //var actor = ClientActors[customGO_packet.];
                        //if(actor == ActorManager.instance.player)
                        //{
                        //    Plugin.logger.LogInfo("Did not create objects for current player because it already has been created");
                        //    break;
                        //}
                        GameObject networkPrefab = WLobby.GetNetworkPrefabByHash(customGO_packet.PrefabHash);
                        GameObject instantiaedPrefab = GameObject.Instantiate(networkPrefab);
                        instantiaedPrefab.transform.position = customGO_packet.Position;
                        instantiaedPrefab.transform.eulerAngles = customGO_packet.Rotation;
                        Plugin.logger.LogInfo("ianstantiatedPrefab at " + instantiaedPrefab.transform.position);
                        if (networkPrefab == null)
                        {
                            Plugin.logger.LogDebug("Network prefab is null");
                            break;
                        }
                        Plugin.logger.LogDebug("Created custom gameobject " + networkPrefab.name + " with hash " + customGO_packet.PrefabHash);
                    }
                    break;
                case PacketType.NetworkGameObjectsHashes:
                    {
                        NetworkGameObjectsHashesPacket syncGO_packet = dataStream.ReadSyncNetworkGameObjectsPacket();
                        Plugin.logger.LogInfo("Got syncGO packet with hashes: " + syncGO_packet.NetworkGameObjectHashes);
                        WLobby.RefreshHashes(syncGO_packet.NetworkGameObjectHashes);

                        //var actor = ClientActors[customGO_packet.];
                        //if(actor == ActorManager.instance.player)
                        //{
                        //    Plugin.logger.LogInfo("Did not create objects for current player because it already has been created");
                        //    break;
                        //}
                    }
                    break;
            }
        }
    }



}
