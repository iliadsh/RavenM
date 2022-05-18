using System.Collections.Generic;
using ProtoBuf;
using UnityEngine;
using HarmonyLib;

namespace RavenM
{
    [HarmonyPatch(typeof(VehicleSpawner), nameof(VehicleSpawner.SpawnVehicle))]
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

    /// <summary>
    /// Don't let the Vehicle die before the owner says it should.
    /// </summary>
    [HarmonyPatch(typeof(Vehicle), nameof(Vehicle.Die))]
    public class VehicleDiePatch
    {
        static bool Prefix(Vehicle __instance, DamageInfo info)
        {
            if (!IngameNetManager.instance.IsClient)
                return true;

            var guidComponent = __instance.GetComponent<GuidComponent>();

            if (guidComponent == null)
            {
                Plugin.logger.LogError($"Vehicle dead without GUID? name: {__instance.name}");
                return true;
            }

            var id = guidComponent.guid;

            if (IngameNetManager.instance.OwnedVehicles.Contains(id))
                return true;

            if (!IngameNetManager.instance.RemoteDeadVehicles.Contains(id))
                return false;

            return true;
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

        [ProtoMember(6)]
        public float Health;

        [ProtoMember(7)]
        public bool Dead;
    }

    [ProtoContract]
    public class BulkVehicleUpdate
    {
        [ProtoMember(1)]
        public List<VehiclePacket> Updates;
    }
}
