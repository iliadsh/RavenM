using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lua.Proxy;
using MoonSharp.Interpreter;
using RavenM.RavenScriptExtension.Wrapper;

namespace RavenM.RavenScriptExtension.Proxy
{
    [Proxy(typeof(WOnlinePlayer))]
    public class OnlinePlayerProxy : IProxy
    {
        public static void SendPacketToServer(string data,int packetType, int send_flags)
        {
            WOnlinePlayer.SendPacketToServer(data, packetType, send_flags);
        }
        public static string OwnGUID
        {
            get
            {
                return WOnlinePlayer.GetOwnGUID();
            }
        }
        [MoonSharpHidden]
        public object GetValue()
        {
            throw new InvalidOperationException("Proxied type is static.");
        }
    }
}
