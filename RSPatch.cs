using HarmonyLib;
using Lua.Proxy;
using Lua.Wrapper;
using MoonSharp.Interpreter;
using RavenM.RavenScriptExtension.Proxy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenM
{
    // Can't use nameOf because Awake is private
    [HarmonyPatch(typeof(Lua.ScriptEngine))]
    [HarmonyPatch("Awake")]
    class RSPatch
    {
        static AccessTools.FieldRef<Lua.ScriptEngine, Type[]> proxyTypes =
       AccessTools.FieldRefAccess<Lua.ScriptEngine, Type[]>("proxyTypes");
        static void Postfix(Lua.ScriptEngine __instance,MoonSharp.Interpreter.Script ___script)
        {
            Plugin.logger.LogInfo("proxyTypes1 -> " + proxyTypes(__instance).Length);
            //proxyTypes(__instance).AddItem(typeof(OnlinePlayerProxy));
            Plugin.logger.LogInfo("proxyTypes2 -> " + proxyTypes(__instance));
            UserData.RegisterType(typeof(OnlinePlayerProxy), InteropAccessMode.Default, null);
            ___script.Globals["OnlinePlayer"] = typeof(OnlinePlayerProxy);



            Plugin.instance.printConsole("<color=green>---------------Successfully added RS features---------------</color>");
        }
    }
}
