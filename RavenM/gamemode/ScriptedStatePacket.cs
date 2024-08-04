using HarmonyLib;
using Ravenfield.Trigger;
using System.Collections;
using System.Collections.Generic;
using Steamworks;
using System.Linq;
namespace RavenM
{
    [HarmonyPatch(typeof(ScriptedGameMode), "StartGame")]
    public class ScriptedMissionPlayerCheckPatch
    {
        public static List<TriggerOnStart> triggerOnStart = new List<TriggerOnStart>();

        public static bool ready = false;


        static bool Prefix(ScriptedGameMode __instance)
        {
            if (IngameNetManager.instance.IsClient && !IngameNetManager.instance.IsHost)
                return false;
            if (ready)
            {
                foreach (TriggerOnStart triggerOnStart in triggerOnStart)
                {
                    if (triggerOnStart != null)
                    {
                        triggerOnStart.Start();
                    }
                }
                triggerOnStart.Clear();
                ready = false;
                return true;
            }
            __instance.StartCoroutine(WaitForPlayers(__instance));
            return false;
        }

        public static IEnumerator WaitForPlayers(ScriptedGameMode gamemode)
        {
            ready = false;




            while (LobbySystem.instance.GetLobbyMembers().Any(x => SteamMatchmaking.GetLobbyMemberData(LobbySystem.instance.ActualLobbyID, x, "loaded") != "yes") ||
                LobbySystem.instance.GetLobbyMembers().Count > IngameNetManager.instance.GetPlayers().Count)
            {
                //wait until everyone is in the scene
                //just checking if all players have "loaded" set to true won't 100% work because the other lobby members could still be in the previous scene and are technically still loaded
                //we can also check if the players are inside the host's scene just to be extra sure
                yield return null;
            }            
            ready = true;
            yield return null;


            gamemode.StartGame();
            yield break;
        }

    }
    [HarmonyPatch(typeof(TriggerOnStart), "Start")]
    public class ScriptedMissionStartPatch
    {
        static bool Prefix(TriggerOnStart __instance)
        {
            if (!IngameNetManager.instance.IsHost && IngameNetManager.instance.IsClient)
                return false;
            if (IngameNetManager.instance.IsHost)
            {
                if (GameModeBase.activeGameMode is ScriptedGameMode && __instance.type == TriggerOnStart.Type.OnStart)
                {
                    if (ScriptedMissionPlayerCheckPatch.ready)
                    {
                        return true;
                    }
                    else
                    {
                        ScriptedMissionPlayerCheckPatch.triggerOnStart.Add(__instance);
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
