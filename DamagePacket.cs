using UnityEngine;
using HarmonyLib;
using System.IO;
using Steamworks;

namespace RavenM
{
    [HarmonyPatch(typeof(Actor), nameof(Actor.Damage))]
    public class DamagePatch
    {
#pragma warning disable Harmony003
        static void Postfix(Actor __instance, DamageInfo info, bool __result)
        {
            if (!IngameNetManager.instance.IsClient)
                return;

            if (!__result)
                return;

            var sourceActorId = info.sourceActor == null ? -1 : info.sourceActor.GetComponent<GuidComponent>().guid;

            var targetActorId = __instance.GetComponent<GuidComponent>().guid;

            if ((sourceActorId == -1 && !IngameNetManager.instance.OwnedActors.Contains(targetActorId)) || !IngameNetManager.instance.OwnedActors.Contains(sourceActorId))
                return;

            Plugin.logger.LogInfo($"Sending damage from: {__instance.name}");

            using MemoryStream memoryStream = new MemoryStream();
            var damage = new DamagePacket
            {
                Type = info.type,
                HealthDamage = info.healthDamage,
                BalanceDamage = info.balanceDamage,
                IsSplashDamage = info.isSplashDamage,
                IsPiercing = info.isPiercing,
                IsCriticalHit = info.isCriticalHit,
                Point = info.point,
                Direction = info.direction,
                ImpactForce = info.impactForce,
                SourceActor = sourceActorId,
                TargetActor = targetActorId,
                Silent = false,
            };

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(damage);
            }
            byte[] data = memoryStream.ToArray();

            IngameNetManager.instance.SendPacketToServer(data, PacketType.Damage, Constants.k_nSteamNetworkingSend_Reliable);
        }
#pragma warning restore Harmony003
    }

    [HarmonyPatch(typeof(Actor), "Die")]
    public class DiePatch
    {
#pragma warning disable Harmony003
        static void Postfix(Actor __instance, DamageInfo info, bool isSilentKill)
        {
            if (!IngameNetManager.instance.IsClient)
                return;

            var sourceActorId = info.sourceActor == null ? -1 : info.sourceActor.GetComponent<GuidComponent>().guid;

            var targetActorId = __instance.GetComponent<GuidComponent>().guid;

            if ((sourceActorId == -1 && !IngameNetManager.instance.OwnedActors.Contains(targetActorId)) || !IngameNetManager.instance.OwnedActors.Contains(sourceActorId))
                return;

            Plugin.logger.LogInfo($"Sending death from: {__instance.name}");

            using MemoryStream memoryStream = new MemoryStream();
            var damage = new DamagePacket
            {
                Type = info.type,
                HealthDamage = info.healthDamage,
                BalanceDamage = info.balanceDamage,
                IsSplashDamage = info.isSplashDamage,
                IsPiercing = info.isPiercing,
                IsCriticalHit = info.isCriticalHit,
                Point = info.point,
                Direction = info.direction,
                ImpactForce = info.impactForce,
                SourceActor = sourceActorId,
                TargetActor = targetActorId,
                Silent = isSilentKill,
            };

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(damage);
            }
            byte[] data = memoryStream.ToArray();

            IngameNetManager.instance.SendPacketToServer(data, PacketType.Death, Constants.k_nSteamNetworkingSend_Reliable);
        }
#pragma warning restore Harmony003
    }

    /// <summary>
    /// Sent for both Damage and Death. Essentially a DamageInfo struct.
    /// </summary>
    public class DamagePacket
    {
        public DamageInfo.DamageSourceType Type;

		public float HealthDamage;

		public float BalanceDamage;

		public bool IsSplashDamage;

		public bool IsPiercing;

		public bool IsCriticalHit;

		public Vector3 Point;

		public Vector3 Direction;

		public Vector3 ImpactForce;

		public int SourceActor;

		// [ProtoMember(11)]
		// public string SourceWeapon;

		public int TargetActor;

        public bool Silent;
	}
}
