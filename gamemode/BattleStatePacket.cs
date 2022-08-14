using HarmonyLib;

namespace RavenM
{
    /// <summary>
    /// Don't naturally drain any of the tickets.
    /// </summary>
    [HarmonyPatch(typeof(BattleMode), "DrainTicket")]
    public class NoDrainPatch
    {
        static bool Prefix(BattleMode __instance, int team)
        {
            if (!IngameNetManager.instance.IsClient)
                return true;

            if (!IngameNetManager.instance.IsHost)
                return false;

            return true;
        }
    }

    public class BattleStatePacket
    {
        public int[] RemainingBattalions;

        public int[] Tickets;

        public int[] SpawnPointOwners;
    }
}
