using HarmonyLib;
using Lua;
using Lua.Proxy;
using Lua.Wrapper;
using MoonSharp.Interpreter;
using RavenM.RSPatch.Proxy;
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
            script.Globals["GameEventsM"] = typeof(RavenscriptEventsMProxy);
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
                if (!(isInitialized(__instance) || !isAwake(__instance)))
                {
                    throw new Exception("ScriptedBehaviour started but not initialized.");
                }

                //var possibleDuplicate = GameObject.Find(__instance.gameObject.name);
                //if (possibleDuplicate != null && possibleDuplicate != __instance.gameObject)
                //{
                //    GameObject.Destroy(possibleDuplicate);
                //}

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
    //[HarmonyPatch(typeof(RavenscriptManager), "Awake")]
    //public class RSPatchRavenscriptEvents
    //{

    //    static AccessTools.FieldRef<RavenscriptManager, ScriptEngine> _engine =
    //AccessTools.FieldRefAccess<RavenscriptManager, ScriptEngine>("_engine");

    //    static bool Postfix(ScriptedBehaviour __instance)
    //    {
    //        if (__instance.sourceMutator != null)
    //        {
    //            if (!(isInitialized(__instance) || !isAwake(__instance)))
    //            {
    //                throw new Exception("ScriptedBehaviour started but not initialized.");
    //            }

    //            //var possibleDuplicate = GameObject.Find(__instance.gameObject.name);
    //            //if (possibleDuplicate != null && possibleDuplicate != __instance.gameObject)
    //            //{
    //            //    GameObject.Destroy(possibleDuplicate);
    //            //}

    //            foreach (ScriptedBehaviour possibleDuplicate in GameObject.FindObjectsOfType<ScriptedBehaviour>())
    //            {
    //                Plugin.instance.printConsole($"Checking duplicate for {__instance.gameObject.name}");
    //                if (possibleDuplicate.gameObject.name == __instance.gameObject.name && possibleDuplicate.gameObject != __instance.gameObject)
    //                {
    //                    Plugin.instance.printConsole($"Found duplicate of {__instance.gameObject.name}, removing...");
    //                    GameObject.Destroy(possibleDuplicate);
    //                }
    //            }

    //            MethodInfo method = __instance.GetType().GetMethod("CallBuiltInMethod",
    //            BindingFlags.NonPublic | BindingFlags.Instance);
    //            method.Invoke(__instance, new object[] { start(__instance) });

    //            return false;
    //        }
    //        else
    //        {
    //            return true;
    //        }
    //    }
    //}


    //[HarmonyPatch(typeof(RavenscriptManager))]
    //[HarmonyPatch("events", MethodType.Getter)]
    //public sealed class RSPatchRavenscriptManagerEvents
    //{
    //    private RavenscriptMultiplayerEvents _events2;

    //    static AccessTools.FieldRef<RavenscriptManager, ScriptEngine> _engine =
    //AccessTools.FieldRefAccess<RavenscriptManager, ScriptEngine>("_engine");

    //    private static RSPatchRavenscriptManagerEvents instance = null;
    //    private static readonly object padlock = new object();
    //    //[HarmonyPrefix]
    //    //public static bool returnEvents(ref RavenscriptEvents __result) {
    //    //    //Replace get with my event Class that has the same events as the default one
    //    //    lock (padlock)
    //    //    {
    //    //        if (instance == null)
    //    //        {
    //    //            instance = new RSPatchRavenscriptManagerEvents();

    //    //        }
    //    //    }
    //    //    Plugin.logger.LogInfo("returnEvents awd awd awd aw dawd awd ad");
    //    //    //__result = instance._events2;
    //    //    return true;
    //    //}
    //    RSPatchRavenscriptManagerEvents()
    //    {
    //    }
    //    public static RSPatchRavenscriptManagerEvents Instance
    //    {
    //        get
    //        {
    //            lock (padlock)
    //            {
    //                if (instance == null)
    //                {
    //                    instance = new RSPatchRavenscriptManagerEvents();
    //                }
    //                return instance;
    //            }
    //        }
    //    }

    //    public static RavenscriptMultiplayerEvents events
    //    {
    //        get
    //        {
    //            return RSPatchRavenscriptManagerEvents.instance._events2;
    //        }
    //    }

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

        static AccessTools.FieldRef<RavenscriptManager, ScriptEngine> _engine =
        AccessTools.FieldRefAccess<RavenscriptManager, ScriptEngine>("_engine");

        static AccessTools.FieldRef<RavenscriptManager, RavenscriptEvents> _events =
       AccessTools.FieldRefAccess<RavenscriptManager, RavenscriptEvents>("_events");
        static RavenscriptMultiplayerEvents _events2;

        //public static RavenscriptMultiplayerEvents events
        //{
        //    get
        //    {
        //        return RavenscriptMultiplayerEvents.instance._events2;
        //    }
        //}
        static bool Prefix(RavenscriptManager __instance)
        {
            RavenscriptManager.instance = __instance;
            __instance.console.Initialize();
            Plugin.logger.LogInfo("1");
            _engine(__instance) = __instance.gameObject.AddComponent<ScriptEngine>();
            Plugin.logger.LogInfo("2");
            _events(__instance) = __instance.gameObject.AddComponent<RavenscriptMultiplayerEvents>();
            Plugin.logger.LogInfo("3");
            _engine(__instance).Set(NameAttribute.GetName(typeof(RavenscriptMultiplayerEvents)), _events(__instance));
            //_events2 = __instance.gameObject.AddComponent<RavenscriptMultiplayerEvents>();

            //_engine(__instance).Set(NameAttribute.GetName(typeof(RavenscriptMultiplayerEvents)), _events(__instance));
            Plugin.logger.LogInfo("4");
            __instance.Invoke("PrintStartupMessage",0);
            Plugin.instance.printConsole("-> Patched events");
            return false;
            //Plugin.logger.LogInfo("before Events global value -> " + _engine(__instance).GetProcessorStats());
            //RSPatchRavenscriptManagerEvents.Instance._events2 = _engine(__instance).gameObject.AddComponent<RavenscriptMultiplayerEvents>();
            //Plugin.instance.printConsole("_events " + RSPatchRavenscriptManagerEvents.Instance._events2.gameObject);
            //_engine(__instance).Set(NameAttribute.GetName(typeof(RavenscriptMultiplayerEvents)), RSPatchRavenscriptManagerEvents.events);
            //Plugin.logger.LogInfo("after Events global value -> " + _engine(__instance).GetProcessorStats());
        }
    }
}
