using Lua;
using MoonSharp.Interpreter;
using RavenM.Commands;
using System.Collections.Generic;
namespace RavenM.rspatch.Wrapper
{
    [Name("CommandManager")]
    public static class WCommandManager
    {
        private static List<Command> customCommands = new List<Command>();
        [CallbackSignature(new string[]
        {
            "commandName",
            "arg1Type",
            "global",
            "hostOnly"
        })]
        public static void AddCommand(string commandName,string arg1Type,bool global, bool hostOnly)
        {
            if (customCommands.Exists(x => x.CommandName == commandName))
                return;
            Command command;
            if (arg1Type.ToLower() == "null")
            {
                command = new Command(commandName, new object[] { null }, global, hostOnly, true);
            }
            else
            {
                command = new Command(commandName, new object[] { arg1Type }, global, hostOnly, true);
                if (!IngameNetManager.instance.commandManager.HasRequiredArgs(command,new string[] { commandName, arg1Type }))
                    throw new ScriptRuntimeException("argument 'arg1Type' is not a valid Type");
            }
            customCommands.Add(command);
            Plugin.logger.LogInfo("Added Custom Command /" + commandName);
            IngameNetManager.instance.commandManager.AddCustomCommand(command);
        }
        public static void AddCommand(string commandName, string arg1Type,string arg2Type, bool global, bool hostOnly)
        {
            if (customCommands.Exists(x => x.CommandName == commandName))
                return;
            Command command = new Command(commandName, new object[] { arg1Type, arg2Type }, global, hostOnly, true);
            if (!IngameNetManager.instance.commandManager.HasRequiredArgs(command, new string[] { commandName, arg1Type,arg2Type }))
                throw new ScriptRuntimeException("argument 'arg1Type' or 'arg2Type' is not a valid Type");
            customCommands.Add(command);
            Plugin.logger.LogInfo("Added Custom Command /" + commandName);
            IngameNetManager.instance.commandManager.AddCustomCommand(command);
        }
        public static void AddCommand(string commandName, string arg1Type, string arg2Type,string arg3Type, bool global, bool hostOnly)
        {
            if (customCommands.Exists(x => x.CommandName == commandName))
                return;
            Command command = new Command(commandName, new object[] { arg1Type, arg2Type, arg3Type }, global, hostOnly, true);
            if (!IngameNetManager.instance.commandManager.HasRequiredArgs(command, new string[] { commandName, arg1Type, arg2Type, arg3Type }))
                throw new ScriptRuntimeException("argument 'arg1Type', 'arg2Type' or 'arg3Type' is not a valid Type");
            customCommands.Add(command);
            Plugin.logger.LogInfo("Added Custom Command /" + commandName);
            IngameNetManager.instance.commandManager.AddCustomCommand(command);
        }
        // Fix Teleport in Script
        public static Actor GetActorByName(string name)
        {
            foreach (Actor actor in ActorManager.instance.actors)
            {
                if (actor.name.ToLower() == name.ToLower())
                {
                    return actor;
                }
            }
            return null;
        }
    }
}
