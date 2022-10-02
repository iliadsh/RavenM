using Lua;
using Lua.Proxy;
using MoonSharp.Interpreter;
using RavenM.rspatch.Wrapper;
using System;

namespace RavenM.rspatch.Proxy
{
    [Proxy(typeof(WCommandManager))]
    public class CommandManagerProxy : IProxy
    {

        [MoonSharpHidden]
        public object GetValue()
        {
            throw new InvalidOperationException("Proxied type is static.");
        }
		[CallbackSignature(new string[]
		{
			"commandName",
			"arg1Type",
			"global",
			"hostOnly"
		})]
		public static void AddCustomCommand(string commandName, string arg1Type, bool global, bool hostOnly)
		{
			if (commandName == null)
			{
				throw new ScriptRuntimeException("argument 'commandName' is nil");
			}
			if (arg1Type == null)
			{
				throw new ScriptRuntimeException("argument 'arg1Type' is nil");
			}
			WCommandManager.AddCommand(commandName, arg1Type, global, hostOnly);
		}
		[CallbackSignature(new string[]
		{
			"commandName",
			"arg1Type",
			"arg2Type",
			"global",
			"hostOnly"
		})]
		public static void AddCustomCommand(string commandName, string arg1Type,string arg2Type, bool global, bool hostOnly)
		{
			if (commandName == null)
				throw new ScriptRuntimeException("argument 'commandName' is nil");
			if (arg1Type == null)
				throw new ScriptRuntimeException("argument 'arg1Type' is nil");
			if (arg2Type == null)
				throw new ScriptRuntimeException("argument 'arg2Type' is nil");
			WCommandManager.AddCommand(commandName, arg1Type,arg2Type, global, hostOnly);
		}
		[CallbackSignature(new string[]
		{
			"commandName",
			"arg1Type",
			"arg2Type",
			"arg3Type",
			"global",
			"hostOnly"
		})]
		public static void AddCustomCommand(string commandName, string arg1Type, string arg2Type, string arg3Type, bool global, bool hostOnly)
		{
			if (commandName == null)
				throw new ScriptRuntimeException("argument 'commandName' is nil");
			if (arg1Type == null)
				throw new ScriptRuntimeException("argument 'arg1Type' is nil");
			if (arg2Type == null)
				throw new ScriptRuntimeException("argument 'arg2Type' is nil");
			if (arg3Type == null)
				throw new ScriptRuntimeException("argument 'arg3Type' is nil");
			WCommandManager.AddCommand(commandName, arg1Type,arg2Type,arg3Type, global, hostOnly);
		}
		public static Actor GetActorByName(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				throw new ScriptRuntimeException("argument 'name' is nil");
			}
			return WCommandManager.GetActorByName(name);
		}

	}
}
