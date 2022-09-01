﻿using HarmonyLib;
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
            "data",
            "packetType",
            "sender",

        })]
        public ScriptEvent<int, string> onReceivePacket { get; protected set; }


        [CallbackSignature(new string[]
        {
            "data",
            "PacketType"
        })]
        public ScriptEvent<string, string> onSendPacket { get; protected set; }


        public ScriptEvent<Actor> onPlayerDisconnect { get; protected set; }

        public ScriptEvent<Actor> onPlayerJoin { get; protected set; }

        public ScriptEvent<Actor, string[],bool> onReceiveChatMessage { get;protected set;}

    }
}