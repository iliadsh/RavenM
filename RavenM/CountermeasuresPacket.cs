using HarmonyLib;
using System.IO;
using Steamworks;

namespace RavenM
{
    [HarmonyPatch(typeof(Vehicle), "PopCountermeasures")]
    public class CountermeasuresPatch 
    {
        static void Prefix(Vehicle __instance) 
        {
            if (!IngameNetManager.instance.IsClient)
                return;

            var vehicleGuid = __instance.GetComponent<GuidComponent>();
            if (vehicleGuid == null)
                return;

            var targetVehicleId = vehicleGuid.guid;

            if (!IngameNetManager.instance.OwnedVehicles.Contains(targetVehicleId))
                return;

            using MemoryStream memoryStream = new MemoryStream();
            var countermeasuresPacket = new CountermeasuresPacket
            {
                VehicleId = targetVehicleId,
            };

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(countermeasuresPacket);
            }
            byte[] data = memoryStream.ToArray();

            IngameNetManager.instance.SendPacketToServer(data, PacketType.Countermeasures, Constants.k_nSteamNetworkingSend_Reliable);
        }
    }

    public class CountermeasuresPacket
    {
        public int VehicleId;
    }
}
