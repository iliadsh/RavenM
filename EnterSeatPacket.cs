using ProtoBuf;
using HarmonyLib;
using System.IO;
using Steamworks;

namespace RavenM
{
    [HarmonyPatch(typeof(Actor), nameof(Actor.EnterSeat))]
    public class EnterSeatPatch
    {
        static void Postfix(Actor __instance, Seat seat, bool kickOutOccupant, bool __result)
        {
            if (!IngameNetManager.instance.IsClient)
                return;

            if (!__result)
                return;

            var actorId = __instance.GetComponent<GuidComponent>().guid;

            if (!IngameNetManager.instance.OwnedActors.Contains(actorId))
                return;

            var vehicle = seat.vehicle;

            var targetVehicleId = vehicle.GetComponent<GuidComponent>().guid;

            var seatId = vehicle.seats.IndexOf(seat);

            if (seatId == -1)
                return; // Is this possible?

            Plugin.logger.LogInfo($"Entering vehicle from: {__instance.name}. Seat ID: {seatId}");

            // Take control if we need it.
            if (!IngameNetManager.instance.IsHost && seat.IsDriverSeat())
                IngameNetManager.instance.OwnedVehicles.Add(targetVehicleId);

            using MemoryStream memoryStream = new MemoryStream();
            var enterPacket = new EnterSeatPacket
            {
                ActorId = actorId,
                VehicleId = targetVehicleId,
                SeatId = seatId,
            };

            Serializer.Serialize(memoryStream, enterPacket);
            byte[] data = memoryStream.ToArray();

            IngameNetManager.instance.SendPacketToServer(data, PacketType.EnterSeat, Constants.k_nSteamNetworkingSend_Reliable);
        }
    }

    /// <summary>
    /// Send when an actor enters a Vehicle.
    /// </summary>
    [ProtoContract]
    public class EnterSeatPacket
    {
        /// <summary>
        /// GUID of the Actor that is entering a seat.
        /// </summary>
        [ProtoMember(1)]
        public int ActorId;

        /// <summary>
        /// GUID of the Vehicle that owns the seat.
        /// </summary>
        [ProtoMember(2)]
        public int VehicleId;

        /// <summary>
        /// Index of the seat in the owning vehicle.
        /// </summary>
        [ProtoMember(3)]
        public int SeatId;
    }
}
