using System;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using Ravenfield.SpecOps;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections;
using System.IO;
using System.Linq;
using Steamworks;

namespace RavenM
{
    /// <summary>
    /// We have to use InLobby rather than IsClient here because the following patches
    /// are ran before we even open the client connection.
    /// </summary>
    /// 
    public class CheckIfHostMethod
    {
        public static bool isOnHostTeam()
        {
            string hostName = SteamFriends.GetFriendPersonaName(LobbySystem.instance.OwnerID);
            foreach (Actor actor in ActorManager.instance.actors)
            {
                if (actor.name.ToLower() == hostName.ToLower() || actor.name == "TALON-1" || actor.name == "RAVEN-1")
                {
                    if (actor.team != GameManager.PlayerTeam())
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        public static Actor getHost()
        {
            string hostName = SteamFriends.GetFriendPersonaName(LobbySystem.instance.OwnerID);
            foreach (Actor actor in ActorManager.instance.actors)
            {
                if (actor.name.ToLower() == hostName.ToLower() || actor.name == "TALON-1" || actor.name == "RAVEN-1")
                {
                    if (actor.team != GameManager.PlayerTeam())
                    {
                        return actor;
                    }
                }
            }
            return null;
        }
    }
    [HarmonyPatch(typeof(SpecOpsMode), "SpawnScenarios")]
    public class ScenarioCreatePatch
    {
        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(SpecOpsMode), "FindAttackerSpawn")]
    public class AttackerSpawnPatch
    {
        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;

            return false;
        }
    }
    [HarmonyPatch(typeof(SpecOpsMode), "UpdateAttackers")]
    public class AttackerUpdatePatch
    {
        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;

            return false;
        }
    }
    [HarmonyPatch(typeof(SpecOpsMode), "InitializeAttackerSpawn")]
    public class InitializeAttackerSpawnPatch
    {
        static bool Prefix(ref Vector3 spawnPosition, SpecOpsMode __instance)
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;
            if (CheckIfHostMethod.isOnHostTeam() == false)
            {
                SpawnPoint viablePoint = ActorManager.instance.spawnPoints[0];
                foreach (SpawnPoint spawnPoint in new List<SpawnPoint>(ActorManager.instance.spawnPoints))
                {
                    if (spawnPoint.owner == GameManager.PlayerTeam() && spawnPoint != ActorManager.ClosestSpawnPoint(__instance.attackerSpawnPosition))
                    {
                        viablePoint = spawnPoint;
                    }
                }
                Transform spawnpointContainer = viablePoint.spawnpointContainer.transform;
                spawnPosition = spawnpointContainer.GetChild(Mathf.RoundToInt(UnityEngine.Random.Range(0, spawnpointContainer.childCount))).position;
                return true;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(SpecOpsMode), "SpawnAttackers")]
    public class SpawnAttackersPatch
    {
        static bool Prefix(SpecOpsMode __instance)
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;
            Vector3 spawnPos = __instance.attackerSpawnPosition;
            string hostName = SteamFriends.GetFriendPersonaName(LobbySystem.instance.OwnerID);
            bool isDefender = CheckIfHostMethod.isOnHostTeam();
            if (!isDefender)
            {
                IngameNetManager.instance.hideDialog = true;
                FpsActorController.instance.actor.team = __instance.defendingTeam;
                IngameDialog.PrintActorText("p unknown", "do some shenaniganery, " + FpsActorController.instance.actor.name + ". hehehe :)");
                IngameDialog.HideAfter(5);
                Actor host = CheckIfHostMethod.getHost();
                Plugin.logger.LogInfo($"host {host.name} is not on this client's team! (spawned)");

                var attackers = new List<Actor>
                        {
                            host,
                        };
                foreach (var client in IngameNetManager.instance.ClientActors.Values)
                {
                    if (client.team == host.team)
                    {
                        attackers.Add(client);
                    }
                }
                var specOpsObj = (GameModeBase.instance as SpecOpsMode);
                specOpsObj.attackerActors = attackers.ToArray();
                specOpsObj.attackerSquad = host.controller.GetSquad();
            }

            var player = FpsActorController.instance.actor;
            player.SpawnAt(spawnPos, __instance.attackerSpawnRotation);
            player.hasHeroArmor = true;
            if (isDefender)
            {
                Plugin.logger.LogInfo($"player {player.name} is on host's team!!! (spawned)");
                var attackers = new List<Actor>
            {
                FpsActorController.instance.actor
            };
                foreach (var actor in IngameNetManager.instance.ClientActors.Values)
                {
                    if (actor.team == player.team)
                    {
                        attackers.Add(actor);
                        FpsActorController.instance.playerSquad.AddMember(actor.controller);
                    }
                }
                var specOpsObj = (GameModeBase.instance as SpecOpsMode);
                specOpsObj.attackerActors = attackers.ToArray();
                specOpsObj.attackerSquad = FpsActorController.instance.playerSquad;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Actor), nameof(Actor.SpawnAt))]
    public class GiveAttackersHeroArmorPatch
    {
        static void Postfix(Actor __instance)
        {
            if (!LobbySystem.instance.InLobby)
                return;

            if (GameModeBase.instance.gameModeType != GameModeType.SpecOps)
                return;

            if (__instance.team != (GameModeBase.instance as SpecOpsMode).attackingTeam)
                return;

            __instance.hasHeroArmor = true;
        }
    }

    [HarmonyPatch(typeof(ExfilHelicopter), "AllAttackersPickedUp")]
    public class AllPlayersPickedUpPatch
    {
        static bool Prefix(ExfilHelicopter __instance, ref bool __result)
        {
            if (!LobbySystem.instance.InLobby)
                return true;

            if (FpsActorController.instance.playerSquad == null)
            {
                __result = false;
                return false;
            }

            var helicopter = typeof(ExfilHelicopter).GetField("helicopter", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance) as Helicopter;
            foreach (Actor actor in IngameNetManager.instance.GetPlayers())
            {
                if (actor.dead || actor.team != CheckIfHostMethod.getHost().team)
                    continue;

                if (!actor.IsSeated() || actor.seat.vehicle != helicopter)
                {
                    __result = false;
                    return false;
                }
            }
            __result = true;
            return false;
        }
    }
    [HarmonyPatch(typeof(ExfilHelicopter), nameof(ExfilHelicopter.StartExfiltration))]
    public class HeliDefenderPatch
    {
        static bool Prefix(ExfilHelicopter __instance, Vector3 landingPoint)
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;
            if (CheckIfHostMethod.isOnHostTeam() == false)
            {
                ObjectiveUi.CreateObjective("Prevent Exfil", landingPoint + Vector3.up);
                return false;
            }
            return true;

        }
    }

            [HarmonyPatch(typeof(ExfilHelicopter), "Update")]
    public class HostExfilWhenDeadPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                // IL_02c2: ldfld bool Vehicle::playerIsInside
                if (codes[i].opcode == OpCodes.Ldfld && (FieldInfo)codes[i].operand == typeof(Vehicle).GetField(nameof(Vehicle.playerIsInside), BindingFlags.Instance | BindingFlags.Public))
                {
                    // IL_02c7: brfalse.s IL_02f2 -> IL_02c7: brfalse.s IL_02e4
                    Label label = generator.DefineLabel();
                    codes[i + 1].operand = label;
                    // FIXME: Very brittle.
                    codes[i + 9].labels = new List<Label>() { label };
                }
            }

            return codes.AsEnumerable();
        }
    }

    [HarmonyPatch(typeof(SpecOpsMode), "Update")]
    public class NoEndPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool first = true;

            foreach (var instruction in instructions)
            {
                if (first && instruction.opcode == OpCodes.Call)
                {
                    instruction.operand = typeof(NoEndPatch).GetMethod("IsSpectatingOrDefeatControl", BindingFlags.NonPublic | BindingFlags.Static);
                    first = false;
                }

                // Transpiler moment.
                if (instruction.opcode == OpCodes.Call && (MethodInfo)instruction.operand == typeof(SpecOpsMode).GetMethod("DefeatSequence", BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    instruction.operand = typeof(NoEndPatch).GetMethod("DefeatDetour", BindingFlags.NonPublic | BindingFlags.Static);
                }

                yield return instruction;
            }
        }

        static bool IsSpectatingOrDefeatControl()
        {
            if (!LobbySystem.instance.InLobby)
                return GameManager.IsSpectating();

            if (FpsActorController.instance.actor.dead && !FpsActorController.instance.inPhotoMode)
                typeof(FpsActorController).GetMethod("TogglePhotoMode", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(FpsActorController.instance, null);

            if (IngameNetManager.instance.IsHost)
                return !IngameNetManager.instance.GetPlayers().All(actor => actor.dead || actor.team != FpsActorController.instance.actor.team);

            return true;
        }

        static IEnumerator DefeatDetour(SpecOpsMode __instance)
        {
            return typeof(SpecOpsMode).GetMethod("DefeatSequence", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, null) as IEnumerator;
        }
    }

    [HarmonyPatch(typeof(SpecOpsMode), "ExfiltrationVictorySequence")]
    public class ExfiltrationVictorySequencePatch
    {
        public static bool CanPerform = false;

        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby || CanPerform)
                return true;

            if (IngameNetManager.instance.IsHost)
            {
                using MemoryStream memoryStream = new MemoryStream();
                var sequencePacket = new SpecOpsSequencePacket
                {
                    Sequence = SpecOpsSequencePacket.SequenceType.ExfiltrationVictory,
                };

                using (var writer = new ProtocolWriter(memoryStream))
                {
                    writer.Write(sequencePacket);
                }

                byte[] data = memoryStream.ToArray();
                IngameNetManager.instance.SendPacketToServer(data, PacketType.SpecOpsSequence, Constants.k_nSteamNetworkingSend_Reliable);

                return true;
            }
            
            return false;
        }
    }

    [HarmonyPatch(typeof(SpecOpsMode), "StealthVictorySequence")]
    public class StealthVictorySequencePatch
    {
        public static bool CanPerform = false;

        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby || CanPerform)
                return true;

            if (IngameNetManager.instance.IsHost)
            {
                using MemoryStream memoryStream = new MemoryStream();
                var sequencePacket = new SpecOpsSequencePacket
                {
                    Sequence = SpecOpsSequencePacket.SequenceType.StealthVictory,
                };

                using (var writer = new ProtocolWriter(memoryStream))
                {
                    writer.Write(sequencePacket);
                }

                byte[] data = memoryStream.ToArray();
                IngameNetManager.instance.SendPacketToServer(data, PacketType.SpecOpsSequence, Constants.k_nSteamNetworkingSend_Reliable);

                return true;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(SpecOpsMode), "DefeatSequence")]
    public class DefeatSequencePatch
    {
        public static bool CanPerform = false;

        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby || CanPerform)
                return true;

            if (IngameNetManager.instance.IsHost)
            {
                using MemoryStream memoryStream = new MemoryStream();
                var sequencePacket = new SpecOpsSequencePacket
                {
                    Sequence = SpecOpsSequencePacket.SequenceType.Defeat,
                };

                using (var writer = new ProtocolWriter(memoryStream))
                {
                    writer.Write(sequencePacket);
                }

                byte[] data = memoryStream.ToArray();
                IngameNetManager.instance.SendPacketToServer(data, PacketType.SpecOpsSequence, Constants.k_nSteamNetworkingSend_Reliable);

                return true;
            }
            
            
            return false;
        }
    }

    [HarmonyPatch]
    public class PlayDialogBasicPatch
    {
        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;

            return false;
        }
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(SpecOpsDialog), "PlayDialog", new Type[] { (Type)typeof(SpecOpsDialog).GetMember("DialogDel", BindingFlags.NonPublic).GetValue(0) });
        }
    }

    [HarmonyPatch]
    public class PlayDialogSpawnPatch
    {
        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;

            return false;
        }
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(SpecOpsDialog), "PlayDialog", new Type[] { (Type)typeof(SpecOpsDialog).GetMember("DialogTargetSpawnDel", BindingFlags.NonPublic).GetValue(0),
                                                                                        typeof(SpawnPoint) });
        }
    }

    [HarmonyPatch]
    public class PlayDialogPatrolPatch
    {
        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;

            return false;
        }
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(SpecOpsDialog), "PlayDialog", new Type[] { (Type)typeof(SpecOpsDialog).GetMember("DialogTargetPatrolDel", BindingFlags.NonPublic).GetValue(0),
                                                                                        typeof(SpecOpsPatrol) });
        }
    }

    [HarmonyPatch]
    public class PlayDialogVehiclePatch
    {
        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;

            return false;
        }
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(SpecOpsDialog), "PlayDialog", new Type[] { (Type)typeof(SpecOpsDialog).GetMember("DialogTargetVehicleDel", BindingFlags.NonPublic).GetValue(0),
                                                                                        typeof(Vehicle) });
        }
    }

    [HarmonyPatch(typeof(SpecOpsScenario), nameof(SpecOpsScenario.SpawnActors))]
    public class SpawnActorsPatch
    {
        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(DestroyScenario), nameof(DestroyScenario.Initialize))]
    public class DestroyInitPatch
    {
        static bool Prefix(DestroyScenario __instance, SpecOpsMode specOps, SpawnPoint spawn)
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;

            // Why is it such a pain to bypass override?
            __instance.spawn = spawn;
            __instance.specOps = specOps;

            var target = (Vehicle)typeof(DestroyScenario).GetField("targetVehicle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);
            target.isLocked = true;
            __instance.objective = ObjectiveUi.CreateObjective("Destroy " + target.name, target.targetLockPoint);

            return false;
        }
    }

    [HarmonyPatch(typeof(SabotageScenario), nameof(SabotageScenario.Initialize))]
    public class SabotageTargetsPatch
    {
        static bool Prefix(SabotageScenario __instance, SpecOpsMode specOps, SpawnPoint spawn)
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;

            __instance.spawn = spawn;
            __instance.specOps = specOps;

            typeof(SabotageScenario).GetField("destroyedTargets", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(__instance, 0);
            typeof(SabotageScenario).GetMethod("RemoveExistingResupplyCrate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, null);
            var objectiveText = (string)typeof(SabotageScenario).GetMethod("GetObjectiveText", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, null);
            __instance.objective = ObjectiveUi.CreateObjective(objectiveText, __instance.SpawnPointObjectivePosition());

            return false;
        }
    }
    // Why does this exist in the first place?
    [HarmonyPatch(typeof(SabotageScenario), "RemoveExistingResupplyCrate")]
    public class NoRemoveCratePatch
    {
        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(SpecOpsPatrolGenerator), nameof(SpecOpsPatrolGenerator.PopulateWaypoints))]
    public class PatrolGenPatch
    {
        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;

            return false;
        }
    }

    // TODO: This is broken because for simplicity, all foreign actors have HasSpottedTarget()
    //       as true, but they don't actually have a target set. The assumptions this method
    //       makes is thus violated.
    [HarmonyPatch(typeof(SpecOpsDialog), nameof(SpecOpsDialog.Update))]
    public class NoUpdateDialogPatch
    {
        static bool Prefix()
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(IngameDialog), nameof(IngameDialog.PrintActorText))]
    public class PrintActorTextPatch
    {
        static void Prefix(string actorPose, string text, string overrideName)
        {
            if (!LobbySystem.instance.InLobby || !IngameNetManager.instance.IsHost)
                return;

            using MemoryStream memoryStream = new MemoryStream();
            var dialogPacket = new SpecOpsDialogPacket
            {
                Hide = false,
                ActorPose = actorPose,
                Text = text,
                OverrideName = overrideName,
            };

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(dialogPacket);
            }

            byte[] data = memoryStream.ToArray();
            IngameNetManager.instance.SendPacketToServer(data, PacketType.SpecOpsDialog, Constants.k_nSteamNetworkingSend_Reliable);
        }
    }

    [HarmonyPatch(typeof(IngameDialog), nameof(IngameDialog.Hide))]
    public class HideDialogPatch
    {
        static void Prefix()
        {
            if (!LobbySystem.instance.InLobby || !IngameNetManager.instance.IsHost)
                return;

            using MemoryStream memoryStream = new MemoryStream();
            var dialogPacket = new SpecOpsDialogPacket
            {
                Hide = true,
                ActorPose = string.Empty,
                Text = string.Empty,
                OverrideName = string.Empty,
            };

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(dialogPacket);
            }

            byte[] data = memoryStream.ToArray();
            IngameNetManager.instance.SendPacketToServer(data, PacketType.SpecOpsDialog, Constants.k_nSteamNetworkingSend_Reliable);
        }
    }

    [HarmonyPatch(typeof(SpecOpsMode), nameof(SpecOpsMode.FireFlare))]
    public class FireFlarePatch
    {
        static void Postfix(Actor actor, SpawnPoint spawn)
        {
            if (!IngameNetManager.instance.IsClient || !IngameNetManager.instance.IsHost)
                return;

            using MemoryStream memoryStream = new MemoryStream();
            var flarePacket = new FireFlarePacket
            {
                Actor = actor.GetComponent<GuidComponent>().guid,
                Spawn = Array.IndexOf(ActorManager.instance.spawnPoints, spawn),
            };

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(flarePacket);
            }

            byte[] data = memoryStream.ToArray();
            IngameNetManager.instance.SendPacketToServer(data, PacketType.SpecOpsFlare, Constants.k_nSteamNetworkingSend_Reliable);

            Plugin.logger.LogInfo($"{actor.name} fired a flare.");
        }
    }

    public class SpecOpsStatePacket
    {
        public Vector3 AttackerSpawn;

        public int[] SpawnPointOwners;

        public List<ScenarioPacket> Scenarios;

        public bool GameIsRunning;
    }

    public class ScenarioPacket
    {
        public int Spawn;

        public List<int> Actors;
    }

    public class AssassinateScenarioPacket : ScenarioPacket { }

    public class ClearScenarioPacket : ScenarioPacket { }

    public class DestroyScenarioPacket : ScenarioPacket
    {
        public int TargetVehicle;
    }

    public class SabotageScenarioPacket : ScenarioPacket
    {
        public List<int> Targets;
    }

    public class SpecOpsSequencePacket
    {
        public enum SequenceType
        {
            ExfiltrationVictory,
            StealthVictory,
            Defeat,
        }

        public SequenceType Sequence;
    }

    public class SpecOpsDialogPacket
    {
        public bool Hide;

        public string ActorPose;

        public string Text;

        public string OverrideName;
    }

    public class FireFlarePacket
    {
        public int Actor;

        public int Spawn;
    }
}
