using System.IO;
using HarmonyLib;
using Steamworks;

namespace RavenM
{
    /// <summary>
    /// When swapping seats we don't need to trigger a Leave packet.
    /// </summary>
    [HarmonyPatch(typeof(Actor), "LeaveSeatForSwap")]
    public class LeaveSwapPatch
    {
        static bool Prefix(Actor __instance)
        {
            if (!IngameNetManager.instance.IsClient)
                return true;

            var vehicleGuid = __instance.seat.vehicle.GetComponent<GuidComponent>();
            if (vehicleGuid == null)
                return true;

            var vehicleId = vehicleGuid.guid;

            // Give up control if we temporarily required it.
            if (!IngameNetManager.instance.IsHost && __instance.seat.IsDriverSeat())
                IngameNetManager.instance.OwnedVehicles.Remove(vehicleId);

            var actorGuid = __instance.GetComponent<GuidComponent>();
            if (actorGuid == null)
                return true;

            var actorId = actorGuid.guid;

            if (!IngameNetManager.instance.OwnedActors.Contains(actorId) && IngameNetManager.instance.IsHost && __instance.seat.IsDriverSeat())
            {
                IngameNetManager.instance.OwnedVehicles.Add(vehicleId);
                __instance.seat.vehicle.isInvulnerable = false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Actor), nameof(Actor.LeaveSeat))]
    public class LeaveSeatPatch
    {
        static bool Prefix(Actor __instance)
        {
            if (!IngameNetManager.instance.IsClient)
                return true;

            // Vehicle destroyed?
            if (__instance.seat.vehicle == null)
                return true;

            var vehicleGuid = __instance.seat.vehicle.GetComponent<GuidComponent>();
            if (vehicleGuid == null)
                return true;

            var vehicleId = vehicleGuid.guid;

            // Give up control if we temporarily required it.
            if (!IngameNetManager.instance.IsHost && __instance.seat.IsDriverSeat())
                IngameNetManager.instance.OwnedVehicles.Remove(vehicleId);

            var actorGuid = __instance.GetComponent<GuidComponent>();
            if (actorGuid == null)
                return true;

            var actorId = actorGuid.guid;

            if (!IngameNetManager.instance.OwnedActors.Contains(actorId) && IngameNetManager.instance.IsHost && __instance.seat.IsDriverSeat())
            {
                IngameNetManager.instance.OwnedVehicles.Add(vehicleId);
                __instance.seat.vehicle.isInvulnerable = false;
            }
            return true;
        }

        static void Postfix(Actor __instance, bool forcedByFallingOver)
        {
            if (!IngameNetManager.instance.IsClient)
                return;

            var actorGuid = __instance.GetComponent<GuidComponent>();
            if (actorGuid == null)
                return;

            var actorId = actorGuid.guid;

            if (!IngameNetManager.instance.OwnedActors.Contains(actorId))
                return;

            Plugin.logger.LogInfo($"Leaving vehicle from: {__instance.name}");

            using MemoryStream memoryStream = new MemoryStream();
            var leavePacket = new LeaveSeatPacket
            {
                Id = actorId,
            };

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(leavePacket);
            }
            byte[] data = memoryStream.ToArray();

            IngameNetManager.instance.SendPacketToServer(data, PacketType.LeaveSeat, Constants.k_nSteamNetworkingSend_Reliable);
        }
    }

    /// <summary>
    /// Send when an actor leaves a Vehicle seat.
    /// </summary>
    public class LeaveSeatPacket
    {
        /// <summary>
        /// The actor that is leaving the seat.
        /// </summary>
        public int Id;
    }
}
