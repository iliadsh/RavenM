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
		public ScriptEventProxy onReceivePacket
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
		public ScriptEventProxy onPlayerJoin
		{
			get
			{
				return ScriptEventProxy.New(this._value.onPlayerJoin);
			}
		}
		public ScriptEventProxy onPlayerDisconnect
		{
			get
			{
				return ScriptEventProxy.New(this._value.onPlayerDisconnect);
			}
		}
		public ScriptEventProxy onReceiveChatMessage
		{
			get
			{
				return ScriptEventProxy.New(this._value.onReceiveChatMessage);
			}
		}
		[MoonSharpHidden]
		public object GetValue()
		{
			return this._value;
		}

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

		[MoonSharpUserDataMetamethod("__call")]
		public static RavenscriptEventsMProxy Call(DynValue _)
		{
			return new RavenscriptEventsMProxy();
		}


	}
}
