using Lua.Proxy;
using MoonSharp.Interpreter;
using RavenM.RSPatch.Wrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public static GameObject GetNetworkPrefabByHash(string hash)
        {
            return WLobby.GetNetworkPrefabByHash(hash);
        }
        public static void AddNetworkPrefab(GameObject prefab)
        {
            if (prefab == null)
                throw new ScriptRuntimeException("argument 'prefab' is nil");
            WLobby.AddNetworkPrefab(prefab);
        }
        public static void RemoveNetworkPrefab(GameObject prefab)
        {
            if (prefab == null)
                throw new ScriptRuntimeException("argument 'prefab' is nil");
            WLobby.RemoveNetworkPrefab(prefab);
        }
        [MoonSharpHidden]
        public object GetValue()
        {
            throw new InvalidOperationException("Proxied type is static.");
        }
    }
}
