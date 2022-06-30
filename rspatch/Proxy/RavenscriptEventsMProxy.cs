using Lua;
using Lua.Proxy;
using MoonSharp.Interpreter;
using RavenM.RSPatch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenM.RSPatch.Proxy
{
	[Proxy(typeof(RavenscriptMultiplayerEvents))]
	public class RavenscriptEventsMProxy : IProxy
	{

		[MoonSharpHidden]
		public RavenscriptMultiplayerEvents _value;
		[MoonSharpHidden]
		public RavenscriptEventsMProxy(RavenscriptMultiplayerEvents value)
		{
			this._value = value;
		}
		public RavenscriptEventsMProxy()
		{
			this._value = new RavenscriptMultiplayerEvents();
		}
		public ScriptEventProxy onPacketReceived
		{
			get
			{
				return ScriptEventProxy.New(this._value.onReceivePacket);
			}
		}
		public ScriptEventProxy onSendPacket
		{
			get
			{
				return ScriptEventProxy.New(this._value.onSendPacket);
			}
		}
		[MoonSharpHidden]
		public object GetValue()
		{
			return this._value;
		}

		// Token: 0x06003B14 RID: 15124 RVA: 0x000FC918 File Offset: 0x000FAB18
		[MoonSharpHidden]
		public static RavenscriptEventsMProxy New(RavenscriptMultiplayerEvents value)
		{
			if (value == null)
			{
				return null;
			}
			RavenscriptEventsMProxy ravenscriptEventsMProxy = (RavenscriptEventsMProxy)ObjectCache.Get(typeof(RavenscriptEventsMProxy), value);
			if (ravenscriptEventsMProxy == null)
			{
				ravenscriptEventsMProxy = new RavenscriptEventsMProxy(value);
				ObjectCache.Add(typeof(RavenscriptEventsMProxy), value, ravenscriptEventsMProxy);
			}
			return ravenscriptEventsMProxy;
		}

		// Token: 0x06003B15 RID: 15125 RVA: 0x000FC962 File Offset: 0x000FAB62
		[MoonSharpUserDataMetamethod("__call")]
		public static RavenscriptEventsMProxy Call(DynValue _)
		{
			return new RavenscriptEventsMProxy();
		}

		// Token: 0x06003B16 RID: 15126 RVA: 0x000FC969 File Offset: 0x000FAB69

	}
}
