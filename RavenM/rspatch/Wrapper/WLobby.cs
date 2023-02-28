using Lua;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RavenM.RSPatch.Wrapper
{
    public static class WLobby
    {
        private static Dictionary<string,GameObject> networkGameObjects = new Dictionary<string, GameObject>();
        public static bool setupVehicles = false;
        [Getter]
        public static string[] GetLobbyMembers()
        {
            int numPlayers = SteamMatchmaking.GetNumLobbyMembers(LobbySystem.instance.ActualLobbyID);

            string[] members = new string[numPlayers];

            for (int i = 0; i < numPlayers; i++)
            {
                members[i] = SteamFriends.GetFriendPersonaName(SteamMatchmaking.GetLobbyMemberByIndex(LobbySystem.instance.ActualLobbyID, i));
            }
            return members;
        }
        public static IList<Actor> GetPlayers()
        {
            List<Actor> actors = new List<Actor>();
            foreach (var kv in IngameNetManager.instance.ClientActors)
            {
                var id = kv.Key;
                var actor = kv.Value;

                if (IngameNetManager.instance.OwnedActors.Contains(id))
                    continue;

                var controller = actor.controller as NetActorController;

                if ((controller.Flags & (int)ActorStateFlags.AiControlled) != 0)
                    continue;
                actors.Add(actor);
            }
            actors.Add(ActorManager.instance.player);
            return actors;
        }
        public static void SendServerChatMessage(string message, Color color)
        {
            if (!IngameNetManager.instance.IsHost || !LobbySystem.instance.IsLobbyOwner)
            {
                return;
            }
            string input = $"<b>Server</b> <color=#{ColorUtility.ToHtmlStringRGB(color)}>{message}</color>";
            IngameNetManager.instance.PushChatMessage(null, input, true, -1);
            
            using MemoryStream memoryStream = new MemoryStream();
            var chatPacket = new ChatPacket
            {
                Id = -1,
                Message = input,
                TeamOnly = false,
            };

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(chatPacket);
            }
            byte[] data = memoryStream.ToArray();

            IngameNetManager.instance.SendPacketToServer(data, PacketType.Chat, Constants.k_nSteamNetworkingSend_Reliable);
        }
        public static Dictionary<string, GameObject> GetNetworkPrefabs()
        {
            return networkGameObjects;
        }
        public static void AddNetworkPrefab(GameObject prefab)
        {
            if (networkGameObjects == null)
            {
                Plugin.logger.LogInfo("networkGameObject was null");
            }
            string newGUID;
            newGUID = prefab.GetHashCode().ToString();
            if (!networkGameObjects.ContainsValue(prefab))
            {
                networkGameObjects.Add(newGUID, prefab);
            }
            Plugin.logger.LogInfo("Added network prefab " + prefab.name + " with GUID " + newGUID);
        }
        // Refresh prefab hashes on the client to be able to correctly look up the prefabs by hash
        public static void RefreshHashes(string hash)
        {
            string[] hashes = hash.Split(',');
            List<GameObject> tempList = new List<GameObject>();
            foreach(KeyValuePair<string, GameObject> pair in networkGameObjects)
            {
                tempList.Add(pair.Value);
            }
            try
            {
                networkGameObjects.Clear();
                for (int i = 0; i < hashes.Length; i++)
                {
                    Plugin.logger.LogInfo("Hash for " + hashes[i] + " and GO " + tempList[i].name);
                    networkGameObjects.Add(hashes[i], tempList[i]);
                }
            }catch(IndexOutOfRangeException exception)
            {
                Plugin.logger.LogError("IndexOutOfRangeException in RefreshHashes(): " + exception.Message);
            }
            tempList.Clear();
        }

        // Sends Packet from host to clients to send prefab hashes
        public static void SendNetworkGameObjectsHashesPacket()
        {
            if (!IngameNetManager.instance.IsHost || !LobbySystem.instance.IsLobbyOwner)
            {
                return;
            }
            /*
            if (!WLobby.setupVehicles)
            {
                WLobby.AddVehiclesToNetworkPrefab();
            }
            */
            string hashes = "";
            foreach (KeyValuePair<string, GameObject> pair in networkGameObjects)
            {
                hashes += pair.Key + ",";
            }
            if(hashes.Length <= 0)
            {
                Plugin.logger.LogInfo("No NetworkGameObjectsHashes Packet send!");
                return;
            }
            hashes = hashes.Substring(0, hashes.Length - 1);
            using MemoryStream memoryStream = new MemoryStream();
            NetworkGameObjectsHashesPacket networkGameObjectsHashesPacket = new NetworkGameObjectsHashesPacket
            {
                Id = IngameNetManager.instance.RandomGen.Next(0,65565),
                NetworkGameObjectHashes = hashes
            };
            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(networkGameObjectsHashesPacket);
            }
            Plugin.logger.LogInfo("Send hashes: " + hashes);
            byte[] data = memoryStream.ToArray();
            IngameNetManager.instance.SendPacketToServer(data, PacketType.NetworkGameObjectsHashes, Constants.k_nSteamNetworkingSend_Reliable);
        }
        public static void RemoveNetworkPrefab(GameObject prefab)
        {
            KeyValuePair<string, GameObject> dictionaryPrefab = networkGameObjects

                   .FirstOrDefault(c => c.Value == prefab);
            networkGameObjects.Remove(dictionaryPrefab.Key);
            Plugin.logger.LogInfo("Removed network prefab " + prefab.name);
        }
        public static GameObject GetNetworkPrefabByHash(string guid)
        {
            Plugin.logger.LogInfo("GetNetworkPrefabHash() " + guid);
            GameObject result = networkGameObjects[guid];
            if(result == null)
            {
                Plugin.logger.LogError("The GameObject with GUID " + guid + " could not be found.");
                return null;
            }
            return result;

        }
        public static string GetNetworkPrefabByValue(GameObject prefab)
        {
            Plugin.logger.LogInfo("GetNetworkPrefabByValue() " + prefab.name);
            string output = "null";
            foreach (KeyValuePair<string, GameObject> kvp in networkGameObjects)
            {
                if(kvp.Value == prefab)
                {
                    output = kvp.Key;
                    break;
                }
            }
            if(output == "null")
            {
                Plugin.logger.LogError("GetNetworkPrefabByValue no value found in dictionary for prefab name " + prefab.name);
            }
            //KeyValuePair<string, GameObject> dictionaryPrefab = networkGameObjects
                  //.FirstOrDefault(c => c.Value == prefab);
            return output;
        }
        public static void AddVehiclesToNetworkPrefab()
        {
            foreach (VehicleSpawner.VehicleSpawnType vehicleType in VehicleSpawner.ALL_VEHICLE_TYPES) {
            
                GameObject vehiclePrefab = VehicleSpawner.GetPrefab(0, vehicleType);
                networkGameObjects.Add(vehiclePrefab.GetHashCode().ToString(), vehiclePrefab);
            }
            setupVehicles = true;
        }
    }
}
