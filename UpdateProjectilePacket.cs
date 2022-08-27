using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace RavenM
{
    [HarmonyPatch(typeof(WireGuidedMissile), "UpdatePosition")]
    public class WireGuidedMissileStartPatch
    {
        static bool Prefix(WireGuidedMissile __instance)
        {
            if (!IngameNetManager.instance.IsClient)
                return true;

            var guid = __instance.GetComponent<GuidComponent>();

            if (guid == null)
                return true;

            var id = guid.guid;

            if (IngameNetManager.instance.OwnedProjectiles.Contains(id))
                return true;

            typeof(ExplodingProjectile).GetMethod("Travel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, new object[] { __instance.velocity * Time.deltaTime });
            return false;
        }
    }

    [HarmonyPatch(typeof(Projectile), nameof(Projectile.OnReturnedToPool))]
    public class ReleaseProjectilePatch
    {
        public class Config
        {
            public float damage;

            public float balanceDamage;

            public bool autoAssignArmorDamage;

            public Vehicle.ArmorRating armorDamage;
        }

        public static readonly Dictionary<Projectile, Config> ConfigCache = new Dictionary<Projectile, Config>();

        static void Prefix(Projectile __instance)
        {
            if (!IngameNetManager.instance.IsClient)
                return;

            var guid = __instance.GetComponent<GuidComponent>();

            if (guid == null)
                return;

            var id = guid.guid;

            if (ConfigCache.ContainsKey(__instance))
            {
                var config = ConfigCache[__instance];

                __instance.configuration.damage = config.damage;
                __instance.configuration.balanceDamage = config.balanceDamage;
                __instance.autoAssignArmorDamage = config.autoAssignArmorDamage;
                __instance.armorDamage = config.armorDamage;

                ConfigCache.Remove(__instance);
            }

            if (IngameNetManager.instance.ClientProjectiles.ContainsKey(id) && IngameNetManager.instance.ClientProjectiles[id] == __instance)
            {
                IngameNetManager.instance.ClientProjectiles.Remove(id);
                IngameNetManager.instance.OwnedProjectiles.Remove(id);
            }
        }
    }

    public class UpdateProjectilePacket
    {
        public int Id;

        public Vector3 Position;

        public Vector3 Velocity;

        public bool Enabled;
    }

    public class BulkProjectileUpdate
    {
        public List<UpdateProjectilePacket> Updates;
    }
}
