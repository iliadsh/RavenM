using HarmonyLib;

namespace RavenM
{
    [HarmonyPatch(typeof(PointMatch), nameof(PointMatch.ScoreMultiplier))]
    public class NoScorePatch
    {
        static void Postfix(ref int __result)
        {
            if (!IngameNetManager.instance.IsClient || IngameNetManager.instance.IsHost)
                return;

            __result = 0;
        }
    }

    public class PointMatchStatePacket
    {
        public int BlueScore;

        public int RedScore;

        public int[] SpawnPointOwners;
    }
}
