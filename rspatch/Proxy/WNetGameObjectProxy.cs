using Lua.Proxy;
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenM.RSPatch.Proxy
{
    [Proxy(typeof(RavenM.RSPatch.Wrapper.WNetGameObject))]
    public class WNetGameObjectProxy : IProxy
    {
        
        [MoonSharpHidden]
        public object GetValue()
        {
            throw new InvalidOperationException("Proxied type is static.");
        }
    }
}
