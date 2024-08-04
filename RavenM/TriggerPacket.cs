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
    [HarmonyPatch(typeof(TriggerSend), nameof(TriggerSend.Send))]
    public class TriggerSendPatch
    {
        static bool Prefix(TriggerSend __instance)
        {
            if (IngameNetManager.instance.IsClient && !IngameNetManager.instance.IsHost)
                return false;
            return true;
        }
    }
    
    [HarmonyPatch(typeof(TriggerSpawnSquad), "SpawnBot")]
    public class TriggerSpawnBot
    {
        static void Postfix(TriggerSpawnSquad __instance, AiActorController __result, TriggerSpawnSquad.SpawnInfo info, TriggerSpawnSquad.AiInfo aiInfo)
        {
            if (IngameNetManager.instance.IsClient && !IngameNetManager.instance.IsHost)
                return;

            __instance.StartCoroutine(SendTrigger(__instance, __result, info, aiInfo));
        }

        static IEnumerator SendTrigger(TriggerSpawnSquad __instance, AiActorController __result, TriggerSpawnSquad.SpawnInfo info, TriggerSpawnSquad.AiInfo aiInfo)
        {
            yield return new WaitForSeconds(2);
            using MemoryStream memoryStream = new MemoryStream();
            var packet = new TriggerSpawnActorPacket
            {
                Id = TriggerReceivePatch.GetTriggerComponentHash(__instance),
                ActorId = IngameNetManager.instance.ClientActors.FirstOrDefault(a => a.Value == __result.actor).Key,
                SpawnInfo = Array.IndexOf(__instance.squadMemberInfo, info)
            };
            using (var writer = new ProtocolWriter(memoryStream))
                writer.Write(packet);
            byte[] data = memoryStream.ToArray();
            IngameNetManager.instance.SendPacketToServer(data, PacketType.TriggerSpawnActor, Constants.k_nSteamNetworkingSend_Reliable);
            yield break;
        }
    }



    //prevent odd bugs from happening for the clients

    [HarmonyPatch(typeof(TriggerOnDestructibleDamage), "Update")]
    public class TriggerDestructiblePatch
    {
        static bool Prefix(TriggerOnDestructibleDamage __instance)
        {
            if (IngameNetManager.instance.IsClient && !IngameNetManager.instance.IsHost)
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(TriggerSpawnPlayer), "SpawnPlayer")]
    public class CloseLoadoutPatch
    {
        static void Postfix()
        {
            LoadoutUi.Hide();
        }
    }

    //just teleport all the clients to the host without killing them. should align with what most modders intend
    [HarmonyPatch(typeof(TriggerPlayerTakeOverActor), "OnTriggered")]
    public class TriggerPlayerTakeOver
    {
        static bool Prefix(TriggerPlayerTakeOverActor __instance, TriggerSignal signal)
        {
            if (IngameNetManager.instance.IsClient && !IngameNetManager.instance.IsHost)
            {
                Actor actor = FpsActorController.instance.actor;
                TriggerSignal signalInst = signal;
                Actor targetActor = signalInst.context.actor;
                WeaponManager.LoadoutSet forcedLoadout = ActorManager.CopyLoadoutOfActor(targetActor);
                actor.SpawnAt(targetActor.transform.position, Quaternion.LookRotation(targetActor.controller.FacingDirection()), forcedLoadout);
                int num = -1;
                for (int i = 0; i < 5; i++)
                {
                    if (actor.weapons[i] != null && targetActor.weapons[i] != null)
                    {
                        actor.weapons[i].ammo = targetActor.weapons[i].ammo;
                        actor.weapons[i].spareAmmo = targetActor.weapons[i].spareAmmo;
                        if (targetActor.weapons[i] == targetActor.activeWeapon)
                        {
                            num = i;
                        }
                    }
                }
                if (targetActor.IsSeated())
                    actor.EnterVehicle(targetActor.seat.vehicle);
                if (targetActor.IsOnLadder())
                    actor.GetOnLadderAtHeight(targetActor.ladder, targetActor.ladderHeight);
                if (targetActor.parachuteDeployed)
                    actor.DeployParachute();
                if (num != -1)
                    actor.SwitchWeapon(num);

                actor.AmmoChanged();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(TriggerReceiver), nameof(TriggerReceiver.ReceiveSignal))]
    public class TriggerReceivePatch
    {
    public static Type[] whitelistReceiver = new Type[] {
        typeof(TriggerActivation),
        typeof(TriggerAddActorAccessory),
        typeof(TriggerAnimationPlayer),
        typeof(TriggerAnimatorController),
        typeof(TriggerAudioSnapshot),
        typeof(TriggerChangeGravity),
        typeof(TriggerChangeMaterial),
        typeof(TriggerChangeScene),
        typeof(TriggerChangeSpawnpointContainer),
        typeof(TriggerCreateObjective),
        typeof(TriggerCrossfadeAudio),
        typeof(TriggerDeployParachute),
        typeof(TriggerDisablePlayerInput),
        typeof(TriggerEffect),
        typeof(TriggerEndMatch),
        typeof(TriggerEquipWeapon), //here, all players get the same weapon as what should be the host
        typeof(TriggerHideUI),
        typeof(TriggerMoveGameObject),
        typeof(TriggerPlayerTakeOverActor), //see above
        typeof(TriggerRestartMap),
        typeof(TriggerSaveCheckpoint),
        typeof(TriggerScriptedFunction), //pray that this ravenscript function doesn't break stuff
        typeof(TriggerSpawnBloodParticles),
        typeof(TriggerSpawnPlayer),
        typeof(TriggerSpawnPrefab), //pray that this prefab doesn't have physics or literally anything
        typeof(TriggerUpdateActorInfo), // hopefully this doesn't break anything
        typeof(TriggerUpdateFog),
        typeof(TriggerUpdateAmbientLighting),
        typeof(TriggerUpdateCapturePoint),
        typeof(TriggerUpdateObjective),
        typeof(TriggerUpdateVehicleInfo), // hopefully this doesn't break anything part 2
        };
        static bool Prefix(TriggerReceiver __instance, TriggerSignal signal)
        {
            if (!whitelistReceiver.Contains(__instance.GetType()) || (IngameNetManager.instance.IsClient && !IngameNetManager.instance.IsHost))
                return true;
            TriggerSignal signalInst = signal;
            using MemoryStream memoryStream = new MemoryStream();
            var triggerPacket = new TriggerPacket
            {
                Id = GetTriggerComponentHash(__instance),
                SourceId = GetTriggerComponentHash(signalInst.source as TriggerBaseComponent),
                ActorId = (signalInst.context.actor != null) ? IngameNetManager.instance.ClientActors.FirstOrDefault(a => a.Value == signalInst.context.actor).Key : -1,
                VehicleId = (signalInst.context.vehicle != null) ? IngameNetManager.instance.ClientVehicles.FirstOrDefault(a => a.Value == signalInst.context.vehicle).Key : -1,
            };
            using (var writer = new ProtocolWriter(memoryStream))
                writer.Write(triggerPacket);
            byte[] data = memoryStream.ToArray();
            IngameNetManager.instance.SendPacketToServer(data, PacketType.Trigger, Constants.k_nSteamNetworkingSend_Reliable);
            return true;
        }

        public static int GetTriggerComponentHash(TriggerBaseComponent component)
        {
            //the hash of components doesn't seem to be the same between clients, so instead just hash the name of the gameobject with all the parents' names and the index of the component
            //this in super exact situations still breaks if the modder just so happened to have two hierarchies of gameobjects with the exact same names all the way through
            //and if somehow somewhere the hierarchies of objects just to happen to desync due to switching of parents
            //and if a trigger gameobject is instantiated and ends up having the same hierarchy and names
            //and if components move around on a gameobject
            //(imagine anything above happening hahaha)

            //in all, this solution is shit

            int index = Array.IndexOf(component.GetComponents<Component>(), component); // get index of component
            System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder();

            //loop through parents
            Transform position = component.transform;
            while (position != null)
            {
                stringBuilder.Insert(0, position.gameObject.name);
                position = position.parent;
            }
            // hash all that
            return (component.gameObject.name + index + stringBuilder.ToString()).GetHashCode();
        }
    }


    public class TriggerPacket
    {
        public int Id;

        public int SourceId;

        public int ActorId;

        public int VehicleId;
    }

    public class TriggerSpawnActorPacket
    {
        public int Id;

        public int ActorId;

        public int SpawnInfo;
    }
}
