using HarmonyLib;
using Lua;
using Lua.Wrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RavenM.RSPatch
{
    [GlobalInstance]
    [Include]
    [Name("GameEventsOnline")]
    public class RavenscriptMultiplayerEvents : ScriptEventManager
    {
        [CallbackSignature(new string[]
        {
            "actor",
            "packetType",
            "data",

        })]
        public ScriptEvent<int, string> onReceivePacket { get; protected set; }


        [CallbackSignature(new string[]
        {
            "data",
            "packetType"
        })]
        public ScriptEvent<string, string> onSendPacket { get; protected set; }

        public ScriptEvent<Actor> onPlayerDisconnect { get; protected set; }

        public ScriptEvent<Actor> onPlayerJoin { get; protected set; }

        [CallbackSignature(new string[]
        {
            "actor",
            "message",
        })]
        public ScriptEvent<Actor, string> onReceiveChatMessage { get;protected set;}
        [CallbackSignature(new string[]
        {
            "actor",
            "commandWithArgs",
            "flags {hasCommandPermission,hasRequiredArgs,local}"
        })]
        [Doc("Invoked when a registered Command is received. The Command has to be registered in the Start function of the script first by using CommandManager.AddCustomCommand().")]
        public ScriptEvent<Actor, string[], bool[]> onReceiveCommand { get; protected set; }

    }
}
