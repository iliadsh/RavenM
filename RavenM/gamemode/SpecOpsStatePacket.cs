using System;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using Ravenfield.SpecOps;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections;
using System.IO;
using Steamworks;

namespace RavenM
{
    /// <summary>
    /// We have to use InLobby rather than IsClient here because the following patches
    /// are ran before we even open the client connection.
    /// </summary>
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

    [HarmonyPatch(typeof(SpecOpsMode), "SpawnAttackers")]
    public class SpawnAttackersPatch
    {
        static bool Prefix(SpecOpsMode __instance)
        {
            if (!LobbySystem.instance.InLobby || IngameNetManager.instance.IsHost)
                return true;

            var player = FpsActorController.instance.actor;
            player.SpawnAt(__instance.attackerSpawnPosition, __instance.attackerSpawnRotation);
            player.hasHeroArmor = true;

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
            return false;
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
                    instruction.operand = typeof(NoEndPatch).GetMethod("IsSpectatingOrClient", BindingFlags.NonPublic | BindingFlags.Static);
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

        static bool IsSpectatingOrClient()
        {
            if (!LobbySystem.instance.InLobby)
                return GameManager.IsSpectating();

            if (IngameNetManager.instance.IsHost)
                return false;

            if (FpsActorController.instance.actor.dead && !FpsActorController.instance.inPhotoMode)
                typeof(FpsActorController).GetMethod("TogglePhotoMode", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(FpsActorController.instance, null);

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
