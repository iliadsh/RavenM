using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenM.Commands
{
    public class Command
    {
        public string CommandName { get; set; }
        public object[] reqArgs { get; set; }
        public bool Global { get; set; }
        public bool HostOnly { get; set; }
        public bool Scripted { get; set; }
        public bool AllowInLobby { get; set; }
        public bool AllowInGame { get; set; }
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
