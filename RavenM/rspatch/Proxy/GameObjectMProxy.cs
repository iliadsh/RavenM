using Lua.Proxy;
using Lua.Wrapper;
using MoonSharp.Interpreter;
using RavenM.rspatch;
using RavenM.rspatch.Proxy;
using RavenM.RSPatch.Wrapper;
using Steamworks;
using System.IO;
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
		public static GameObjectProxy Instantiate(GameObjectProxy prefab, GameObjectNetConfigProxy config)
		{
			if (!ActorManager.instance.player.TryGetComponent<GuidComponent>(out GuidComponent comp))
			{
				Plugin.logger.LogError($"Could not instantiate prefab {prefab.name} because player does not have a GuidComponent!");
				return null;
			}
			int gameObjectID = IngameNetManager.instance.RandomGen.Next(0, int.MaxValue);
			GameObject prefab2 = InstantiatePrefabWithPacket(prefab, Vector3.zero, Quaternion.identity, config, comp.guid, gameObjectID);
			GameObjectProxy gameObject = GameObjectProxy.New(WGameObject.Instantiate(prefab2, Vector3.zero, Quaternion.identity));
			AddNetworkTransformToObject(gameObject, null, comp.guid, gameObjectID);
			return gameObject;
		}
		public static GameObjectProxy Instantiate(GameObjectProxy prefab)
		{
			if (!ActorManager.instance.player.TryGetComponent<GuidComponent>(out GuidComponent comp))
			{
				Plugin.logger.LogError($"Could not instantiate prefab {prefab.name} because player does not have a GuidComponent!");
				return null;
			}
			int gameObjectID = IngameNetManager.instance.RandomGen.Next(0, int.MaxValue);
			GameObject prefab2 = InstantiatePrefabWithPacket(prefab, Vector3.zero, Quaternion.identity, null, comp.guid, gameObjectID);
			GameObjectProxy gameObject = GameObjectProxy.New(WGameObject.Instantiate(prefab2, Vector3.zero, Quaternion.identity));
			AddNetworkTransformToObject(gameObject, null, comp.guid, gameObjectID);
			return gameObject;
		}
		public static GameObjectProxy Instantiate(GameObjectProxy prefab, Vector3 pos, Quaternion rot)
		{
			if (!ActorManager.instance.player.TryGetComponent<GuidComponent>(out GuidComponent comp))
			{
				Plugin.logger.LogError($"Could not instantiate prefab {prefab.name} because player does not have a GuidComponent!");
				return null;
			}
			int gameObjectID = IngameNetManager.instance.RandomGen.Next(0, int.MaxValue);
			GameObject prefab2 = InstantiatePrefabWithPacket(prefab, pos, rot,null,comp.guid,gameObjectID);
			GameObjectProxy gameObject = GameObjectProxy.New(WGameObject.Instantiate(prefab2, pos, rot));
			AddNetworkTransformToObject(gameObject, null,comp.guid,gameObjectID);
			return gameObject;
		}
		[MoonSharpHidden]
		private static GameObjectNetConfig GetDefaultNetConfig()
		{
			return new GameObjectNetConfig
			{
				HostOnly = false,
				OnlySyncIfChanged = true,
				SyncPosition = true,
				SyncRotation = true,
				SyncScale = false,
				TickRate = 0.001f
			};
		}
		[MoonSharpHidden]
		public static void AddNetworkTransformToObject(GameObjectProxy gameObject, GameObjectNetConfigProxy config,int sourceID,int gameObjectID)
        {
			if (config == null)
				config = new GameObjectNetConfigProxy(GetDefaultNetConfig());
			
			NetworkTransform netTransform = gameObject._value.AddComponent<NetworkTransform>();
			netTransform.SourceID = sourceID;
			netTransform.GameObjectID = gameObjectID;
			netTransform.netConfig = config._value;
			RSPatch.OwnedObjects.Add(gameObjectID);
		}
		public static void Destroy(DynValue value)
        {
			//RSPatch.OwnedObjects.Remove(gameObjectID);
			//WGameObject.Destroy(value);
		}
		public GameObject _value;

        [MoonSharpHidden]
		private static GameObject InstantiatePrefabWithPacket(GameObjectProxy prefab, Vector3 pos,Quaternion rot,GameObjectNetConfigProxy config,int sourceID,int gameObjectID)
        {
			GameObject prefab2 = null;
			if (prefab != null)
			{
				prefab2 = prefab._value;
			}

			// Problem: NetworkTransform class is not usable by Unity
			using MemoryStream memoryStream = new MemoryStream();
			SpawnCustomGameObjectPacket spawnCustomGameObjectPacket = new SpawnCustomGameObjectPacket
			{
				SourceID = sourceID,
				GameObjectID = gameObjectID,
				PrefabHash = WLobby.GetNetworkPrefabByValue(prefab2),
				Position = pos,
				Rotation = rot
			};
			using (var writer = new ProtocolWriter(memoryStream))
			{
				writer.Write(spawnCustomGameObjectPacket);
			}
			Plugin.logger.LogInfo($"Sending custom game object packet for {prefab2.name} with GameObjectID {gameObjectID} SourceID {sourceID} PrefabHash {spawnCustomGameObjectPacket.PrefabHash}");
			byte[] data = memoryStream.ToArray();
			IngameNetManager.instance.SendPacketToServer(data, PacketType.CreateCustomGameObject, Constants.k_nSteamNetworkingSend_Reliable);
			return prefab2;
		}
	}
}
