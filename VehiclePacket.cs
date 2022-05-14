using System.Collections.Generic;
using ProtoBuf;
using UnityEngine;
using HarmonyLib;

namespace RavenM
{
    [HarmonyPatch(typeof(VehicleSpawner))]
    [HarmonyPatch(nameof(VehicleSpawner.SpawnVehicle))]
    public class SpawnVehicleSyncPatch
    {
        static void Postfix(VehicleSpawner __instance, Vehicle __result)
        {
            if (!IngameNetManager.instance.IsClient)
                return;

            if (!IngameNetManager.instance.IsHost)
                return; // This shouldn't happen anyway

            int id = IngameNetManager.instance.RandomGen.Next(0, int.MaxValue);

            __result.gameObject.AddComponent<GuidComponent>().guid = id;

            IngameNetManager.instance.ClientVehicles.Add(id, __result);
            IngameNetManager.instance.OwnedVehicles.Add(id);
        }
    }

    [ProtoContract]
    public class VehiclePacket
    {
        [ProtoMember(1)]
        public int Id;

        [ProtoMember(2)]
        public VehicleSpawner.VehicleSpawnType Type;

        [ProtoMember(3)]
        public Vector3 Position;

        [ProtoMember(4)]
        public Quaternion Rotation;

        [ProtoMember(5)]
        public int Team;
    }

    [ProtoContract]
    public class BulkVehicleUpdate
    {
        [ProtoMember(1)]
        public List<VehiclePacket> Updates;
    }
}
