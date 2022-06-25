using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace RavenM.RavenScriptExtension.Wrapper
{
    [Lua.Name("OnlinePlayer")]
    public static class WOnlinePlayer
    {
        public static void SendPacketToServer(string data, int packetType, int send_flags)
        {
            if (data == null)
            {
                throw new ScriptRuntimeException("argument 'data' is nil");
            }
            IngameNetManager.instance.SendPacketToServer(Encoding.ASCII.GetBytes(data), (PacketType)packetType, send_flags);
        }
        [Lua.Getter]
        public static string GetOwnGUID()
        {
            return IngameNetManager.instance.OwnGUID.ToString();
        }
    }
}
