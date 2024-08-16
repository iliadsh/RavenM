using HarmonyLib;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace RavenM
{

    [HarmonyPatch(typeof(SkirmishMode), "SpawnReinforcementWave")]
    public class SkirmishWavePatch
    {
        public static bool CanSpawnWave = false;

        static bool Prefix()
        {
            if (!IngameNetManager.instance.IsClient || IngameNetManager.instance.IsHost)
                return true;

            return CanSpawnWave;
        }
    }

    [HarmonyPatch(typeof(SkirmishMode), "PlayerTakeOverBot", MethodType.Enumerator)]
    public class SkirmishDontTakeOverPlayerPatch {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Call && (MethodInfo)instruction.operand == typeof(ActorManager).GetMethod("AliveActorsOnTeam", BindingFlags.Public | BindingFlags.Static))
                {
                    instruction.operand = typeof(SkirmishDontTakeOverPlayerPatch).GetMethod("AliveActorsDetour", BindingFlags.NonPublic | BindingFlags.Static);
                }

                yield return instruction;
            }
        }

        static List<Actor> AliveActorsDetour(int team)
        {
            var nresult = ActorManager.AliveActorsOnTeam(team);

            if (!IngameNetManager.instance.IsClient)
                return nresult;

            List<Actor> output = new List<Actor>();
            foreach (var actor in nresult) {
                var controller = actor.controller as NetActorController;

                if (controller == null || (controller.Flags & (int)ActorStateFlags.AiControlled) != 0)
                    output.Add(actor);
            }
            return output;
        }
    }

    public class SkirmishStatePacket
    {
        public float Domination;

        public bool[] SpawningReinforcements;

        public int[] WavesRemaining;

        public int[] SpawnPointOwners;

        public int TimeToDominate;
    }
}
