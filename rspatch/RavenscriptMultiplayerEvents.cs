using HarmonyLib;
using Lua;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenM.RSPatch
{
    public class RavenscriptMultiplayerEvents : RavenscriptEvents
    {
        [CallbackSignature(new string[]
        {
            "data",
        })]
        public ScriptEvent<string> onReceivePacket { get; protected set; }
    }

}
