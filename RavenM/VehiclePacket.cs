using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

namespace RavenM
{
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

        public int NameHash;

        public ulong Mod;

        public Vector3 Position;

        public Quaternion Rotation;

        public float Health;

        public bool Dead;

        public bool IsTurret;

        public bool Active;

        public bool Invulnerable;

        public bool RamActive;
    }

    public class BulkVehicleUpdate
    {
        public List<VehiclePacket> Updates;
    }
}
