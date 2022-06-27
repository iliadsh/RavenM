using Lua;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenM.RSPatch.Wrapper
{
    public static class WLobby
    {
        [Getter]
        public static string[] GetLobbyMembers()
        {
            int numPlayers = SteamMatchmaking.GetNumLobbyMembers(LobbySystem.instance.ActualLobbyID);

            string[] members = new string[numPlayers];

            for (int i = 0; i < numPlayers; i++)
            {
                members[i] = SteamFriends.GetFriendPersonaName(SteamMatchmaking.GetLobbyMemberByIndex(LobbySystem.instance.ActualLobbyID, i));
                Plugin.logger.LogInfo(members[i]);
            }

            Plugin.logger.LogInfo(members.Length);
            Plugin.logger.LogInfo(LobbySystem.instance.ActualLobbyID);

            return members;
        }
        public static ScriptEvent GetOnReceivePackage(Actor self)
        {
            return self.onTakeDamage;
        }
    }
}
