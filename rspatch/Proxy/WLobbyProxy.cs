using Lua.Proxy;
using MoonSharp.Interpreter;
using RavenM.RSPatch.Wrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenM.RSPatch.Proxy
{
    [Proxy(typeof(WLobby))]
    public class WLobbyProxy : IProxy
    {
        public static string[] members
        {
            get
            {
                return WLobby.GetLobbyMembers();
            }
        }


        [MoonSharpHidden]
        public object GetValue()
        {
            throw new InvalidOperationException("Proxied type is static.");
        }
    }
}
