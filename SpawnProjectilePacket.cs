using HarmonyLib;
using System.IO;
using Steamworks;
using UnityEngine;

namespace RavenM
{
    [HarmonyPatch(typeof(Weapon), "SpawnProjectile")]
    public class SpawnProjectilePatch
    {
        static bool Prefix(Weapon __instance, ref Projectile __result)
        {
            if (!IngameNetManager.instance.IsClient)
                return true;

            var actor = __instance.user;

            if (actor == null)
                return true;

            var guid = actor.GetComponent<GuidComponent>();

            if (guid == null)
                return true;

            var id = guid.guid;

            if (IngameNetManager.instance.OwnedActors.Contains(id))
                return true;

            if (IngameNetManager.instance.ActorToSpawnProjectile == id)
                return true;

            __result = null;
            return false;
        }

        static void Postfix(Vector3 direction, Vector3 muzzlePosition, Projectile __result)
        {
            if (!IngameNetManager.instance.IsClient)
                return;

            if (__result == null)
                return;

            var source = __result.source;

            var actorId = source.GetComponent<GuidComponent>().guid;

            if (!IngameNetManager.instance.OwnedActors.Contains(actorId))
                return;

            var projectileId = IngameNetManager.instance.RandomGen.Next(0, int.MaxValue);

            __result.gameObject.AddComponent<GuidComponent>().guid = projectileId;

            IngameNetManager.instance.OwnedProjectiles.Add(projectileId);
            IngameNetManager.instance.ClientProjectiles[projectileId] = __result;

            using MemoryStream memoryStream = new MemoryStream();
            var spawnPacket = new SpawnProjectilePacket
            {
                SourceId = actorId,
                Direction = direction,
                MuzzlePosition = muzzlePosition,
                ProjectileId = projectileId,
            };

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(spawnPacket);
            }
            byte[] data = memoryStream.ToArray();

            IngameNetManager.instance.SendPacketToServer(data, PacketType.SpawnProjectile, Constants.k_nSteamNetworkingSend_Reliable);
        }
    }
    
    public class SpawnProjectilePacket
    {
        public int SourceId;

        public Vector3 Direction;

        public Vector3 MuzzlePosition;

        /// <summary>
        /// Used for potential updates to position/rotation.
        /// </summary>
        public int ProjectileId;
    }
}
