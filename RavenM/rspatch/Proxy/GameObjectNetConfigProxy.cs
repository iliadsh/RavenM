using Lua.Proxy;
using MoonSharp.Interpreter;

namespace RavenM.rspatch.Proxy
{
    [Proxy(typeof(GameObjectNetConfig))]
    public class GameObjectNetConfigProxy : IProxy
    {
		[MoonSharpHidden]
		public GameObjectNetConfigProxy(GameObjectNetConfig value)
		{
			this._value = value;
		}

		public GameObjectNetConfigProxy(bool HostOnly,bool OnlySyncIfChanged,bool SyncPos,bool SyncRot,bool SyncScale,float Tickrate)
		{
			
			this._value = new GameObjectNetConfig(HostOnly,OnlySyncIfChanged,SyncPos,SyncRot,SyncScale,Tickrate);
		}

		public GameObjectNetConfigProxy(GameObjectNetConfigProxy source)
		{
			if (source == null)
			{
				throw new ScriptRuntimeException("argument 'source' is nil");
			}
			this._value = new GameObjectNetConfig(source._value);
		}

		public GameObjectNetConfigProxy()
		{
			this._value = default(GameObjectNetConfig);
		}

		public bool hostOnly
		{
			get
			{
				return this._value.HostOnly;
			}
			set
			{
				this._value.HostOnly = value;
			}
		}
		public bool syncOnChange
		{
			get
			{
				return this._value.OnlySyncIfChanged;
			}
			set
			{
				this._value.OnlySyncIfChanged = value;
			}
		}

		public bool syncPosition
		{
			get
			{
				return this._value.SyncPosition;
			}
			set
			{
				this._value.SyncPosition = value;
			}
		}

		public bool syncRotation
		{
			get
			{
				return this._value.SyncRotation;
			}
			set
			{
				this._value.SyncRotation = value;
			}
		}

		public bool syncScale
		{
			get
			{
				return this._value.SyncScale;
			}
			set
			{
				this._value.SyncScale = value;
			}
		}

		public float tickRate
		{
			get
			{
				return this._value.TickRate;
			}
			set
			{
				this._value.TickRate = value;
			}
		}


		[MoonSharpHidden]
		public object GetValue()
		{
			return this._value;
		}

		[MoonSharpHidden]
		public static GameObjectNetConfigProxy New(GameObjectNetConfig value)
		{
			return new GameObjectNetConfigProxy(value);
		}

		[MoonSharpUserDataMetamethod("__call")]
		public static GameObjectNetConfigProxy Call(DynValue _, bool hostOnly, bool onlySyncIfChanged, bool syncPosition, bool syncRotation, bool syncScale, float tickRate)
		{
			return new GameObjectNetConfigProxy(hostOnly, onlySyncIfChanged, syncPosition, syncRotation, syncScale, tickRate);
		}

		[MoonSharpUserDataMetamethod("__call")]
		public static GameObjectNetConfigProxy Call(DynValue _, GameObjectNetConfigProxy source)
		{
			return new GameObjectNetConfigProxy(source);
		}

		[MoonSharpUserDataMetamethod("__call")]
		public static GameObjectNetConfigProxy Call(DynValue _)
		{
			return new GameObjectNetConfigProxy();
		}


		public override string ToString()
		{
			return this._value.ToString();
		}

		[MoonSharpHidden]
		public GameObjectNetConfig _value;
	}
}
