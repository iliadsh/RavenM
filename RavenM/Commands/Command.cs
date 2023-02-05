using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenM.Commands
{
    public class Command
    {
        public string CommandName { get; }
        public object[] reqArgs { get; }
        public bool Global { get; }
        public bool HostOnly { get; }
        public bool Scripted { get; }
        public bool AllowInLobby { get; }
        public bool AllowInGame { get;  }
        public Command(string _name, object[] _reqArgs,bool _global, bool _hostOnly, bool scripted = false, bool allowInLobby = false, bool allowInGame = true)
        {
            CommandName = _name;
            reqArgs = _reqArgs;
            Global = _global;
            HostOnly = _hostOnly;
            Scripted = scripted;
            AllowInLobby = allowInLobby;
            AllowInGame = allowInGame;
        }

    }
}
