using HarmonyLib;
using System.Reflection;

namespace RavenM
{
    // TODO: Haunted Skeleton Altars.
    [HarmonyPatch(typeof(SpookOpsMode), "ActivateGameMode")]
    public class NoAltarPatch
    {
        static void Prefix(SpookOpsMode __instance)
        {
            if (!LobbySystem.instance.InLobby)
                return;

            __instance.altarPrefab = null;
        }
    }

    [HarmonyPatch(typeof(SpookOpsMode), "SpawnSkeleton")]
    public class SpawnSkeletonPatch
    {
        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(SpookOpsMode), "SpawnPlayerSquad")]
    public class SpawnPlayerSquadPatch
    {
        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(SpookOpsMode), "PrepareNextPhase")]
    public class PrepareNextPhasePatch 
    {
        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;

            // TODO: Some of the StartGame() code requires a valid target spawn, but we only get the correct spawn
            //       once the host sends it. This will at least avoid breaking the start routine but it's bad because
            //       the initial intro-cutscene-thing will point to some random position.
            typeof(SpookOpsMode).GetField("currentSpawnPoint", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(GameModeBase.activeGameMode, ActorManager.instance.spawnPoints[0]);

            return false;
        }
    }

    [HarmonyPatch(typeof(SpookOpsMode), nameof(SpookOpsMode.ActorDied))]
    public class HauntedActorDiedPatch
    {
        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby)
                return true;

            if (FpsActorController.instance.actor.dead)
                typeof(FpsActorController).GetMethod("TogglePhotoMode", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(FpsActorController.instance, null);

            if (IngameNetManager.instance.IsHost)
                return true;

            return false;
        }

        public static void CheckLoseCondition()
        {
            if ((bool)typeof(SpookOpsMode).GetField("gameOver", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(GameModeBase.activeGameMode))
                return;

            if (((TimedAction)typeof(SpookOpsMode).GetField("introAction", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(GameModeBase.activeGameMode)).TrueDone())
            {
                bool anyPlayerAlive = false;
                foreach (var actor in IngameNetManager.instance.ClientActors.Values)
                {
                    // FIXME: yeah...
                    if ((!actor.aiControlled && !actor.dead) ||
                        (actor.controller is NetActorController &&
                            ((actor.controller as NetActorController).Flags & (int)ActorStateFlags.AiControlled) == 0 &&
                            ((actor.controller as NetActorController).Flags & (int)ActorStateFlags.Dead) == 0))
                    {
                        anyPlayerAlive = true;
                        break;
                    }
                }

                if (!anyPlayerAlive)
                {
                    HauntedEndPatch.CanPerform = true;
                    typeof(SpookOpsMode).GetMethod("EndGame", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(GameModeBase.activeGameMode, null);
                    HauntedEndPatch.CanPerform = false;
                }
            }
        }
    }

    [HarmonyPatch(typeof(SpookOpsMode), "StartPhase")]
    public class StartPhasePatch
    {
        public static bool CanPerform = false;

        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost || CanPerform)
                return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(SpookOpsMode), "EndGame")]
    public class HauntedEndPatch
    {
        public static bool CanPerform = false;

        static bool Prefix(SpookOpsMode __instance)
        {
            if (!LobbySystem.instance.InLobby || CanPerform || (bool)typeof(SpookOpsMode).GetField("gameWon", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance))
                return true;

            return false;
        }
    }

    public class HauntedStatePacket
    {
        public int CurrentSpawnPoint;

        public int PlayerSpawn;

        public int CurrentPhase;

        public int KillCount;

        public bool AwaitingNextPhase;

        public bool PhaseEnded;

        public float SkeletonCountModifier;
    }
}
