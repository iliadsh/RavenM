using UnityEngine;
using HarmonyLib;
using System.IO;
using Steamworks;
using System.Collections.Generic;
using System.Collections;
using System;

namespace RavenM
{
    [HarmonyPatch(typeof(Destructible), "Die")]
    public class DestructibleDiePatch
    {
        static void Prefix(Destructible __instance)
        {
            if (!IngameNetManager.instance.IsClient)
                return;

            var root = DestructiblePacket.Root(__instance);

            var id = root.GetComponent<GuidComponent>().guid;
            var index = Array.IndexOf(DestructiblePacket.GetDestructibles(root), __instance);

            using MemoryStream memoryStream = new MemoryStream();
            var destroyPacket = new DestructibleDiePacket
            {
                Id = id,
                Index = index,
            };

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(destroyPacket);
            }

            byte[] data = memoryStream.ToArray();
            IngameNetManager.instance.SendPacketToServer(data, PacketType.DestroyDestructible, Constants.k_nSteamNetworkingSend_Reliable);

            Plugin.logger.LogInfo($"Destroy {__instance.name} id {id} idx {index}");
        }
    }

    public class DestructiblePacket
    {
        public int Id;

        public bool FullUpdate;

        // Only sent if FullUpdate.
        public int NameHash;

        // Only sent if FullUpdate.
        public ulong Mod;

        // Only sent if FullUpdate.
        public Vector3 Position;

        // Only sent if FullUpdate.
        public Quaternion Rotation;

        public BitArray States;

        public static GameObject Root(Destructible destructible)
        {
            return destructible.transform.root.gameObject;
        }

        public static Destructible[] GetDestructibles(GameObject root)
        {
            return root.GetComponentsInChildren<Destructible>(includeInactive: true);
        }
    }

    public class BulkDestructiblePacket
    {
        public List<DestructiblePacket> Updates;
    }

    public class DestructibleDiePacket
    {
        public int Id;

        public int Index;
    }
}
