using UnityEngine;

namespace RavenM.rspatch
{
    public class NetworkGameObjectPacket
    {
        public int SourceID;
        public int GameObjectID;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public float Speed;
    }
}
