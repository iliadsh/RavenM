using ProtoBuf;
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

    [ProtoContract]
    public class BattleStatePacket
    {
        [ProtoMember(1)]
        public int[] RemainingBattalions;

        [ProtoMember(2)]
        public int[] Tickets;

        [ProtoMember(3)]
        public int[] SpawnPointOwners;
    }
}
