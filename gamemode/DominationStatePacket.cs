using HarmonyLib;

namespace RavenM
{
    [HarmonyPatch(typeof(DominationMode), "EndDominationRound")]
    public class EndDominationRoundPatch
    {
        public static bool CanEndRound = false;

        static bool Prefix()
        {
            if (!IngameNetManager.instance.IsClient || IngameNetManager.instance.IsHost)
                return true;

            return CanEndRound;
        }
    }

    public class DominationStatePacket
    {
        public int[] RemainingBattalions;

        public float[] DominationRatio;

        public int[] SpawnPointOwners;

        public int[] ActiveFlagSet;

        public int TimeToStart;
    }
}
