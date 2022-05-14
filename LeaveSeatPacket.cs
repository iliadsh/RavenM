using System.IO;
using ProtoBuf;
using HarmonyLib;
using Steamworks;

namespace RavenM
{
    [HarmonyPatch(typeof(Actor), nameof(Actor.LeaveSeat))]
    public class LeaveSetPatch
    {
        static bool Prefix(Actor __instance)
        {
            if (!IngameNetManager.instance.IsClient)
                return true;

            var vehicleId = __instance.seat.vehicle.GetComponent<GuidComponent>().guid;

            // Give up control if we temporarily required it.
            if (!IngameNetManager.instance.IsHost && __instance.seat.IsDriverSeat())
                IngameNetManager.instance.OwnedVehicles.Remove(vehicleId);

            return true;
        }

        static void Postfix(Actor __instance, bool forcedByFallingOver)
        {
            if (!IngameNetManager.instance.IsClient)
                return;

            var actorId = __instance.GetComponent<GuidComponent>().guid;

            if (!IngameNetManager.instance.OwnedActors.Contains(actorId))
                return;

            Plugin.logger.LogInfo($"Leaving vehicle from: {__instance.name}");

            using MemoryStream memoryStream = new MemoryStream();
            var leavePacket = new LeaveSeatPacket
            {
                Id = actorId,
            };

            Serializer.Serialize(memoryStream, leavePacket);
            byte[] data = memoryStream.ToArray();

            IngameNetManager.instance.SendPacketToServer(data, PacketType.LeaveSeat, Constants.k_nSteamNetworkingSend_Reliable);
        }
    }

    /// <summary>
    /// Send when an actor leaves a Vehicle seat.
    /// </summary>
    [ProtoContract]
    public class LeaveSeatPacket
    {
        /// <summary>
        /// The actor that is leaving the seat.
        /// </summary>
        [ProtoMember(1)]
        public int Id;
    }
}
