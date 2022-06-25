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
                    if(possibleDuplicate.gameObject.name == __instance.gameObject.name && possibleDuplicate.gameObject != __instance.gameObject)
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


}
