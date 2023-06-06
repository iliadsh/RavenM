using Steamworks;
using System.IO;
using UnityEngine;

namespace RavenM.rspatch
{
    public class NetworkTransform : MonoBehaviour
    {
        
        public GameObjectNetConfig netConfig;

        private int gameObjectID = -1;
        private int sourceID = -1;

        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private Vector3 lastScale;

        public int GameObjectID { get => gameObjectID; set => gameObjectID = value; }
        public int SourceID { get => sourceID; set => sourceID = value; }

        private float lastSentPacket;

        private bool hasAuthority = false;

        void Start()
        {
            if(netConfig.TickRate < 0)
            {
                netConfig.TickRate = 0f;
            }
            if (!ActorManager.instance.player.TryGetComponent<GuidComponent>(out GuidComponent comp))
            {
                return;
            }
            if (sourceID == comp.guid)
            {
                hasAuthority = true;
            }
        }
        void SendUpdatePacket()
        {
            using MemoryStream memoryStream = new MemoryStream();
            var objectSpawnPacket = new NetworkGameObjectPacket
            {
                SourceID = SourceID,
                GameObjectID = GameObjectID,
                Position = this.gameObject.transform.position,
                Rotation = this.gameObject.transform.rotation,
                Scale = this.gameObject.transform.localScale,
                Speed = 0,
            };

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(objectSpawnPacket);
            }
            byte[] data = memoryStream.ToArray();
            IngameNetManager.instance.SendPacketToServer(data, PacketType.NetworkGameObject, Constants.k_nSteamNetworkingSend_Unreliable);
            lastSentPacket = Time.time;
        }
        void Update()
        {
            if(SourceID == -1 || GameObjectID == -1 || !hasAuthority)
            {
                return;
            }

            var moved = (netConfig.OnlySyncIfChanged) ? Vector3.Distance(lastPosition, this.transform.position) > .01f : true;

            if (moved)
            {
                if(netConfig.TickRate == 0)
                    SendUpdatePacket();
                else
                    if((Time.time - lastSentPacket) > netConfig.TickRate)
                        SendUpdatePacket();


            }

            lastPosition = this.transform.position;
        }


    }
}
