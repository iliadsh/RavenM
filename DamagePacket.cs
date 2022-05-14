using ProtoBuf;
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

            Serializer.Serialize(memoryStream, damage);
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

            Serializer.Serialize(memoryStream, damage);
            byte[] data = memoryStream.ToArray();

            IngameNetManager.instance.SendPacketToServer(data, PacketType.Death, Constants.k_nSteamNetworkingSend_Reliable);
        }
#pragma warning restore Harmony003
    }

    /// <summary>
    /// Sent for both Damage and Death. Essentially a DamageInfo struct.
    /// </summary>
    [ProtoContract]
    public class DamagePacket
    {
		[ProtoMember(1)]
        public DamageInfo.DamageSourceType Type;

		[ProtoMember(2)]
		public float HealthDamage;

		[ProtoMember(3)]
		public float BalanceDamage;

		[ProtoMember(4)]
		public bool IsSplashDamage;

		[ProtoMember(5)]
		public bool IsPiercing;

		[ProtoMember(6)]
		public bool IsCriticalHit;

		[ProtoMember(7)]
		public Vector3 Point;

		[ProtoMember(8)]
		public Vector3 Direction;

		[ProtoMember(9)]
		public Vector3 ImpactForce;

		[ProtoMember(10)]
		public int SourceActor;

		// [ProtoMember(11)]
		// public string SourceWeapon;

		[ProtoMember(12)]
		public int TargetActor;

        [ProtoMember(13)]
        public bool Silent;
	}
}
