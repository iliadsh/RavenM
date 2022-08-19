using HarmonyLib;

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

    public class SkirmishStatePacket
    {
        public float Domination;

        public bool[] SpawningReinforcements;

        public int[] WavesRemaining;

        public int[] SpawnPointOwners;

        public int TimeToDominate;
    }
}
