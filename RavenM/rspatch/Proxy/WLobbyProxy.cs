using Lua.Proxy;
using MoonSharp.Interpreter;
using RavenM.RSPatch.Wrapper;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RavenM.RSPatch.Proxy
{
    [Proxy(typeof(WLobby))]
    public class WLobbyProxy : IProxy
    {
        public static string[] members
        {
            get
            {
                return WLobby.GetLobbyMembers();
            }
        }
        //public static Dic<GameObject> GetNetworkPrefabs()
        //{
        //    return WLobby.GetNetworkPrefabs();
        //}
        public static bool isHost
        {
            get
            {
                return IngameNetManager.instance.IsHost;
            }
        }
        public static bool isClient
        {
            get
            {
                return IngameNetManager.instance.IsClient;
            }
        }
        public static IList<Actor> players
        {
            get
            {
                return WLobby.GetPlayers();
            }
        }
        public static void SendServerMessage(string message, ColorProxy value)
        {
            WLobby.SendServerChatMessage(message, value._value);
        }
        public static GameObject GetNetworkPrefabByHash(string hash)
        {
            return WLobby.GetNetworkPrefabByHash(hash);
        }
        public static void AddNetworkPrefab(GameObjectProxy prefab)
        {
            if (prefab == null)
                throw new ScriptRuntimeException("argument 'prefab' is nil");
            WLobby.AddNetworkPrefab(prefab._value);
        }
        public static void PushNetworkPrefabs()
        {
            WLobby.SendNetworkGameObjectsHashesPacket();
        }
        public static void RemoveNetworkPrefab(GameObjectProxy prefab)
        {
            if (prefab == null)
                throw new ScriptRuntimeException("argument 'prefab' is nil");
            WLobby.RemoveNetworkPrefab(prefab._value);
        }
        [MoonSharpHidden]
        public object GetValue()
        {
            throw new InvalidOperationException("Proxied type is static.");
        }
    }
}
