using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenM
{
    public class ChatCommandPacket
    {
        public int Id;

        public ulong SteamID;

        public string Command;

        public bool Scripted;
    }
}
