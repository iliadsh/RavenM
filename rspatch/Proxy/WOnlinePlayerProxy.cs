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
        public static void SendPacketToServer(string data,int packetType, bool reliable)
        {
            WOnlinePlayer.SendPacketToServer(data, packetType, reliable);
        }
        public static string OwnGUID
        {
            get
            {
                return WOnlinePlayer.GetOwnGUID();
            }
        }
       
        public static void SendChatMessage(string input, bool global)
        {
            IngameNetManager.instance.PushChatMessage(ActorManager.instance.player.name, input, global, GameManager.PlayerTeam());

            using MemoryStream memoryStream = new MemoryStream();
            var chatPacket = new ChatPacket
            {
                Id = ActorManager.instance.player.GetComponent<GuidComponent>().guid,
                Message = input,
                TeamOnly = !global,
            };

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(chatPacket);
            }
            byte[] data = memoryStream.ToArray();

            IngameNetManager.instance.SendPacketToServer(data, PacketType.Chat, Constants.k_nSteamNetworkingSend_Reliable);
        }


        [MoonSharpHidden]
        public object GetValue()
        {
            throw new InvalidOperationException("Proxied type is static.");
        }
    }
}
