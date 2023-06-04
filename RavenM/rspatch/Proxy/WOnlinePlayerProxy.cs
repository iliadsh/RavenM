using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lua.Proxy;
using MoonSharp.Interpreter;
using RavenM.RSPatch.Wrapper;
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
       
        //public static void SendChatMessage(string input, bool global)
        //{
        //    IngameNetManager.instance.PushChatMessage(ActorManager.instance.player, input, global, GameManager.PlayerTeam());

        //    using MemoryStream memoryStream = new MemoryStream();
        //    var chatPacket = new ChatPacket
        //    {
        //        Id = ActorManager.instance.player.GetComponent<GuidComponent>().guid,
        //        Message = input,
        //        TeamOnly = !global,
        //    };

        //    using (var writer = new ProtocolWriter(memoryStream))
        //    {
        //        writer.Write(chatPacket);
        //    }
        //    byte[] data = memoryStream.ToArray();

        //    IngameNetManager.instance.SendPacketToServer(data, PacketType.Chat, Constants.k_nSteamNetworkingSend_Reliable);
        //}

        public static void PushChatMessage(string message)
        {
            ChatManager.instance.PushChatMessage(ActorManager.instance.player, message,true, -1);
        }
        public static void PushCommandChatMessage(string message,Color color,bool teamOnly,bool sendToAll)
        {
            ChatManager.instance.PushCommandChatMessage(message, color, teamOnly, sendToAll);
        }
        [MoonSharpHidden]
        public object GetValue()
        {
            throw new InvalidOperationException("Proxied type is static.");
        }
    }
}
