using System.Collections.Generic;
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

    public class VehiclePacket
    {
        public int Id;

        public VehicleSpawner.VehicleSpawnType Type;

        public Vector3 Position;

        public Quaternion Rotation;

        public int Team;

        public float Health;

        public bool Dead;

        public bool IsTurret;

        public TurretSpawner.TurretSpawnType TurretType;

        public bool Active;
    }

    public class BulkVehicleUpdate
    {
        public List<VehiclePacket> Updates;
    }
}
