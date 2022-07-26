using MoonSharp.Interpreter;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenM.RSPatch.Wrapper
{
    [Lua.Name("OnlinePlayer")]
    public static class WOnlinePlayer
    {
        public static void SendPacketToServer(string data, int packetType, bool reliable)
        {
            if (data == null)
            {
                throw new ScriptRuntimeException("argument 'data' is nil");
            }

            // I know letting the user specify a packetType is bad
            // But it will suffice for now
            using MemoryStream memoryStream = new MemoryStream();
            var scriptedPacket = new ScriptedPacket
            {
                ScriptId = ActorManager.instance.player.GetComponent<GuidComponent>().guid,
                Id = packetType,
                Data = data,
            };
            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(scriptedPacket);
            }
            int flag = 0;
            if (reliable)
            {
                flag = Constants.k_nSteamNetworkingSend_Reliable;
            }
            else
            {
                flag = Constants.k_nSteamNetworkingSend_Unreliable;
            }
            
            byte[] data2 = memoryStream.ToArray();
            IngameNetManager.instance.SendPacketToServer(data2, PacketType.ScriptedPacket, flag);
        }
        [Lua.Getter]
        public static string GetOwnGUID()
        {
            return IngameNetManager.instance.OwnGUID.ToString();
        }
    }
}
