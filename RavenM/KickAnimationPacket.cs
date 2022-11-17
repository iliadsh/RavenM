using System.Collections;
using System.IO;
using HarmonyLib;
using Steamworks;
using UnityEngine;

namespace RavenM
{
    /// <summary>
    /// Piggyback on the Kick() method so we can send our packet whenever someone kicks
    /// </summary>
    [HarmonyPatch(typeof(FpsActorController), "Kick")]
    public class KickPatch 
    {
        static void Prefix(FpsActorController __instance)
        {
            if (!IngameNetManager.instance.IsClient)
                return;
            
            int actorId = __instance.actor.GetComponent<GuidComponent>().guid;
            
            using MemoryStream memoryStream = new MemoryStream();
            var kickPacket = new KickAnimationPacket
            {
                Id = actorId,
            };
            
            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(kickPacket);
            }
            
            byte[] data = memoryStream.ToArray();
            IngameNetManager.instance.SendPacketToServer(data, PacketType.KickAnimation, Constants.k_nSteamNetworkingSend_Reliable);
            
            Plugin.logger.LogInfo($"Sending kick from {__instance.actor.name}");
        }
    }

    public static class KickAnimation
    {
        public static RuntimeAnimatorController KickController;
        
        public static RuntimeAnimatorController OldKickController;
        
        public static AudioClip KickSound;
        /// <summary>
        /// Change Temporarily the animator controller for one that contains the kick animation and reproduce a sound
        /// </summary>
        public static IEnumerator PerformKick(Actor actor)
        {
            if (KickController == null)
            {
                Plugin.logger.LogError("Kick Animation Failed!");
                yield break;
            }
            var actorAnimator = actor.animator;
            
            if (OldKickController == null)
            {
                OldKickController = actorAnimator.runtimeAnimatorController;
            }
            
            actorAnimator.runtimeAnimatorController = KickController;
            actorAnimator.SetTrigger("kick");

            AudioSource.PlayClipAtPoint(KickSound, actor.transform.position);

            yield return new WaitForSeconds(1);

            actorAnimator.runtimeAnimatorController = OldKickController;
        }
    }

    /// <summary>
    /// Send when an actor kicks.
    /// </summary>
    public class KickAnimationPacket
    {
        /// <summary>
        /// The actor that is kicking.
        /// </summary>
        public int Id;
    }
}