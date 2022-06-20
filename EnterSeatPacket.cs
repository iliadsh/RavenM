using ProtoBuf;
using HarmonyLib;
using System.IO;
using Steamworks;

namespace RavenM
{
    [HarmonyPatch(typeof(AiActorController), nameof(AiActorController.CreateRougeSquad))]
    public class RogueSquadPatch
    {
        static bool Prefix(AiActorController __instance)
        {
            if (!IngameNetManager.instance.IsClient)
                return true;

            if (!IngameNetManager.instance.OwnedActors.Contains(__instance.actor.GetComponent<GuidComponent>().guid))
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(Actor), nameof(Actor.SwitchSeat))]
    public class SwitchSeatPatch
    {
        static void Prefix(Actor __instance, int seatIndex, ref bool swapIfOccupied)
        {
            if (!swapIfOccupied)
                return;

            if (!__instance.IsSeated())
                return;

            var seat = __instance.seat.vehicle.seats[seatIndex];

            if (!seat.IsOccupied())
                return;

            Actor occupant = seat.occupant;

            var guid = occupant.GetComponent<GuidComponent>();

            if (guid == null)
                return;

            var id = guid.guid;

            if (IngameNetManager.instance.OwnedActors.Contains(id))
                return;

            var controller = occupant.controller as NetActorController;
            if ((controller.Targets.Flags & (int)ActorStateFlags.AiControlled) == 0)
                swapIfOccupied = false;
        }
    }

    [HarmonyPatch(typeof(Actor), nameof(Actor.EnterSeat))]
    public class EnterSeatPatch
    {
        static void Prefix(ref Seat seat, bool kickOutOccupant)
        {
            // We are only interested in the EVIL player.
            if (!kickOutOccupant)
                return;

            if (seat.IsOccupied())
            {
                Actor occupant = seat.occupant;

                var guid = occupant.GetComponent<GuidComponent>();

                if (guid == null)
                    return;

                var id = guid.guid;

                if (IngameNetManager.instance.OwnedActors.Contains(id))
                    return;

                // If the seat we want is held by a player, then we look
                // for a seat that's not.
                var controller = occupant.controller as NetActorController;
                if ((controller.Targets.Flags & (int)ActorStateFlags.AiControlled) == 0)
                {
                    var vehicle = seat.vehicle;
                    foreach (var potentialSeat in vehicle.seats)
                    {
                        if (!potentialSeat.IsOccupied())
                        {
                            seat = potentialSeat;
                            return;
                        }

                        occupant = potentialSeat.occupant;

                        guid = occupant.GetComponent<GuidComponent>();

                        if (guid == null)
                            continue;

                        id = guid.guid;

                        if (IngameNetManager.instance.OwnedActors.Contains(id))
                            continue;

                        controller = occupant.controller as NetActorController;
                        if ((controller.Targets.Flags & (int)ActorStateFlags.AiControlled) == 0)
                            continue;

                        seat = potentialSeat;
                        return;
                    }

                    seat = null;
                }
            }
        }

        static void Postfix(Actor __instance, Seat seat, bool kickOutOccupant, bool __result)
        {
            if (!IngameNetManager.instance.IsClient)
                return;

            if (!__result)
                return;

            var actorGuid = __instance.GetComponent<GuidComponent>();
            if (actorGuid == null)
                return;

            var actorId = actorGuid.guid;

            if (!IngameNetManager.instance.OwnedActors.Contains(actorId))
                return;

            var vehicle = seat.vehicle;

            var vehicleGuid = vehicle.GetComponent<GuidComponent>();
            if (vehicleGuid == null)
                return;

            var targetVehicleId = vehicleGuid.guid;

            var seatId = vehicle.seats.IndexOf(seat);

            if (seatId == -1)
                return; // Is this possible?

            Plugin.logger.LogInfo($"Entering vehicle from: {__instance.name}. Seat ID: {seatId}");

            // Take control if we need it.
            if (!IngameNetManager.instance.IsHost && seat.IsDriverSeat())
            {
                IngameNetManager.instance.OwnedVehicles.Add(targetVehicleId);
                vehicle.isInvulnerable = false;
            }
                
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
