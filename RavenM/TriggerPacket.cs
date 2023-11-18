using System;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections;
using System.IO;
using System.Linq;
using Steamworks;
using Ravenfield.Trigger;
namespace RavenM
{
    /// <summary>
    /// Don't let the Vehicle die before the owner says it should.
    /// </summary>
    [HarmonyPatch(typeof(TriggerSend), nameof(TriggerSend.Send))]
    public class TriggerSendPatch
    {
        static bool Prefix(TriggerSend __instance)
        {
            if (!IngameNetManager.instance.IsClient && !IngameNetManager.instance.IsHost)
                return true;
            return false;
        }
    }
    [HarmonyPatch(typeof(TriggerReceiver), nameof(TriggerReceiver.ReceiveSignal))]
    public class TriggerReceivePatch
    {
        public static Type[] whitelistReceiver = new Type[] {
        typeof(TriggerActivation),
        typeof(TriggerRestartMap),
        typeof(TriggerEndMatch),
        typeof(TriggerEffect),
        typeof(TriggerCreateObjective),
        typeof(TriggerChangeMaterial),
        typeof(TriggerAnimatorController),
        typeof(TriggerAnimationPlayer),
        typeof(TriggerSaveCheckpoint),
        typeof(TriggerUpdateFog),
        typeof(TriggerUpdateObjective),
        typeof(TriggerMoveGameObject),
        typeof(TriggerChangeSpawnpointContainer),
        typeof(TriggerChangeScene),
        typeof(TriggerChangeGravity),
        typeof(TriggerHideUI),
        typeof(TriggerSpawnPlayer),
        typeof(TriggerSpawnPrefab), //pray that this prefab doesn't have physics or literally anything
        typeof(TriggerEquipWeapon),
        };
        static bool Prefix(TriggerReceiver __instance, TriggerSignal signal)
        {
            TriggerSignal signalInst = signal;
            if (!whitelistReceiver.Contains(__instance.GetType()))
                return true;
            if (!IngameNetManager.instance.IsClient)
                return false;
            //if (!TriggerManager.validReceivers.Contains(__instance))
                //return true;
            using MemoryStream memoryStream = new MemoryStream();

            var triggerPacket = new TriggerPacket
            {
                Id = __instance.GetHashCode(),
                SourceId = (signalInst.source as TriggerBaseComponent).GetHashCode(),
                ActorId = (signalInst.context.actor != null) ? IngameNetManager.instance.ClientActors.FirstOrDefault(a => a.Value == signalInst.context.actor).Key : -1,
                VehicleId = (signalInst.context.vehicle != null) ? IngameNetManager.instance.ClientVehicles.FirstOrDefault(a => a.Value == signalInst.context.vehicle).Key : -1,
            };
            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(triggerPacket);
            }

            byte[] data = memoryStream.ToArray();
            IngameNetManager.instance.SendPacketToServer(data, PacketType.Trigger, Constants.k_nSteamNetworkingSend_Reliable);
            return true;
        }
    }

    public class TriggerPacket
    {
        public int Id;

        public int SourceId;

        public int ActorId;

        public int VehicleId;
    }
}
