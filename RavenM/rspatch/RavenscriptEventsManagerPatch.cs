using HarmonyLib;
using Lua;
using RavenM.RSPatch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace RavenM.RSPatch
{
    [HarmonyPatch(typeof(RavenscriptManager), "ResolveCurrentlyInvokingSourceScript")]
    public class RavenscriptEventsManagerPatch : MonoBehaviour
    {
        private RavenscriptMultiplayerEvents _events;
        
        public static RavenscriptEventsManagerPatch instance;

        public static RavenscriptMultiplayerEvents events
        {
            get
            {
                return RavenscriptEventsManagerPatch.instance._events;
            }
        }
        private void Awake()
        {
            RavenscriptEventsManagerPatch.instance = this;
            ScriptEngine engine = GameObject.Find("_Managers(Clone)").GetComponent<ScriptEngine>();
            Plugin.logger.LogInfo("Got script engine");
            _events = base.gameObject.AddComponent<RavenscriptMultiplayerEvents>();
            engine.Set(NameAttribute.GetName(typeof(RavenscriptMultiplayerEvents)), this._events);
            Plugin.logger.LogInfo("Added events to global variable");
        }
        static void Postfix(ref ScriptedBehaviour __result)
        {
            if (!instance._events.IsCallStackEmpty())
            {
                ScriptedBehaviour currentInvokingListenerScript = instance._events.GetCurrentEvent().GetCurrentInvokingListenerScript();
                if (currentInvokingListenerScript != null)
                {
                    __result = currentInvokingListenerScript;
                }
            }

        }
    }
    // Implement garbage collector
    [HarmonyPatch(typeof(ScriptEvent), "UnsafeInvoke")]
    public class RSPatchScriptEvent
    {

        static bool Prefix(ScriptEvent __instance)
        {
            if (RavenscriptEventsManagerPatch.events.IsCallStackFull())
            {
                StackTraceLogType stackTraceLogType = Application.GetStackTraceLogType(LogType.Error);
                Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);
                ScriptConsole.instance.LogError("[Prefix] Event Call Stack is full, escaping event callback. If you see this message, one or more scripts generated an event feedback loop.");
                Application.SetStackTraceLogType(LogType.Error, stackTraceLogType);
                return false;
            }
            RavenscriptEventsManagerPatch.events.PushCallStack(__instance);
            return true;
        }
        static void Postfix()
        {
            RavenscriptEventsManagerPatch.events.PopCallStack();
        }
    }
    [HarmonyPatch(typeof(ScriptEventCache), "GetOrCreateEvent")]
    public class RSPatchScriptEventCacheGetOrCreateEvent
    {

        static bool Prefix(ScriptEventCache __instance,UnityEventBase unityEvent, ref ScriptEventCache.GetOrCreateResult __result, Dictionary<UnityEventBase, ScriptEvent> ___events)
        {

            if (!___events.ContainsKey(unityEvent))
            {
                ScriptEvent scriptEvent = RavenscriptEventsManagerPatch.events.CreateEvent();
                ___events.Add(unityEvent, scriptEvent);
                __result = new ScriptEventCache.GetOrCreateResult(scriptEvent, wasCreated: true);
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(ScriptEventCache), "GetOrCreateAction")]
    public class RSPatchScriptEventCacheGetOrCreateAction
    {

        static bool Prefix(ScriptEventCache __instance, Action action, ref ScriptEventCache.GetOrCreateResult __result, Dictionary<Action, ScriptEvent> ___actions)
        {

            if (!___actions.ContainsKey(action))
            {
                ScriptEvent scriptEvent = RavenscriptEventsManagerPatch.events.CreateEvent();
                ___actions.Add(action, scriptEvent);
                __result = new ScriptEventCache.GetOrCreateResult(scriptEvent, true);
                return false;
            }
            return true;
        }
    }
}
