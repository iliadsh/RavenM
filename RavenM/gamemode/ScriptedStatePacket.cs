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
                ChatManager.instance.PushCommandChatMessage("Starting Scripted Mission!", UnityEngine.Color.white, false, true);
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
            while (LobbySystem.instance.GetLobbyMembers().Any(x => SteamMatchmaking.GetLobbyMemberData(LobbySystem.instance.ActualLobbyID, x, "loaded") != "yes")) //wait until everyone is in the scene
            {
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
                    }
                }
            }
            return true;
        }
    }
}
