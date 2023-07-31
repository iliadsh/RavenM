using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lua.Proxy;
using MoonSharp.Interpreter;
using RavenM.RSPatch.Wrapper;
using RavenM.UI;
using Steamworks;
using UnityEngine;

namespace RavenM.RSPatch.Proxy
{
    [Proxy(typeof(WOnlinePlayer))]
    public class WOnlinePlayerProxy : IProxy
    {
        public static bool SendPacketToServer(string data,int packetType, bool reliable)
        {
            return WOnlinePlayer.SendPacketToServer(data, packetType, reliable);
        }
        public static string OwnGUID
        {
            get
            {
                return WOnlinePlayer.GetOwnGUID();
            }
        }

        public static void PushChatMessage(string message)
        {
            ChatManager.instance.PushChatMessage(ActorManager.instance.player, message,true, -1);
        }
        public static void PushCommandChatMessage(string message,Color color,bool teamOnly,bool sendToAll)
        {
            ChatManager.instance.PushCommandChatMessage(message, color, teamOnly, sendToAll);
        }
        public static void SetNameTagForActor(Actor actor, string newName)
        {
            GameUI.instance.SetNameTagForActor(actor, newName);
        }
        public static void ResetNameTags()
        {
            GameUI.instance.ResetNameTagsToOriginal();
        }
        public static Actor GetPlayerFromName(string name)
        {
            return WOnlinePlayer.GetPlayerFromName(name);
        }
        [MoonSharpHidden]
        public object GetValue()
        {
            throw new InvalidOperationException("Proxied type is static.");
        }
    }
}
