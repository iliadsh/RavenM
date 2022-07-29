using Lua.Proxy;
using Lua.Wrapper;
using MoonSharp.Interpreter;
using RavenM.RSPatch.Wrapper;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RavenM.RSPatch.Proxy
{
    [Proxy(typeof(GameObjectProxy))]
    public class GameObjectMProxy : IProxy
    {
		[MoonSharpHidden]
		public GameObjectMProxy(GameObject value)
		{
			this._value = value;
		}
		public GameObjectMProxy()
		{
			this._value = WGameObject.Constructor();
		}
		[MoonSharpHidden]
		public object GetValue()
		{
			return this._value;
		}
		public static GameObjectProxy Instantiate(GameObjectProxy prefab)
		{
			GameObject prefab2 = InstantiatePrefabWithPacket(prefab, Vector3.zero, Vector3.zero);
            return GameObjectProxy.New(WGameObject.Instantiate(prefab2));
		}
		public static GameObjectProxy Instantiate(GameObjectProxy prefab,Vector3 pos, Vector3 rot)
		{
			GameObject prefab2 = InstantiatePrefabWithPacket(prefab, pos, rot);
			return GameObjectProxy.New(WGameObject.Instantiate(prefab2,pos,Quaternion.Euler(rot)));
		}

		public GameObject _value;

        [MoonSharpHidden]
		private static GameObject InstantiatePrefabWithPacket(GameObjectProxy prefab, Vector3 pos,Vector3 rot)
        {
			GameObject prefab2 = null;
			if (prefab != null)
			{
				prefab2 = prefab._value;
			}
            if (!WLobby.setupVehicles)
            {
				WLobby.AddVehiclesToNetworkPrefab();
            }
			Plugin.logger.LogInfo("Instantiated vehicle prefab " + prefab2.name + " on server");

			using MemoryStream memoryStream = new MemoryStream();
			SpawnCustomGameObjectPacket spawnCustomGameObjectPacket = new SpawnCustomGameObjectPacket
			{
				SourceID = 0,
				PrefabHash = WLobby.GetNetworkPrefabByValue(prefab2),
				Position = pos,
				Rotation = rot
			};
			using (var writer = new ProtocolWriter(memoryStream))
			{
				writer.Write(spawnCustomGameObjectPacket);
			}

			Plugin.logger.LogInfo("Instantiate() prefab name " + prefab.name + " with GUID " + WLobby.GetNetworkPrefabByValue(prefab2));
			byte[] data = memoryStream.ToArray();
			IngameNetManager.instance.SendPacketToServer(data, PacketType.CreateCustomGameObject, Constants.k_nSteamNetworkingSend_Reliable);
			return prefab2;
			//var bulkVehicleUpdate = new BulkVehicleUpdate
			//{
			//	Updates = new List<VehiclePacket>(),
			//};



			//if (bulkVehicleUpdate.Updates.Count == 0)
			//	return;

			//using MemoryStream memoryStream = new MemoryStream();

			//using (var writer = new ProtocolWriter(memoryStream))
			//{
			//	writer.Write(bulkVehicleUpdate);
			//}
			//byte[] data = memoryStream.ToArray();

			//SendPacketToServer(data, PacketType.VehicleUpdate, Constants.k_nSteamNetworkingSend_Unreliable);
		}
	}
}
