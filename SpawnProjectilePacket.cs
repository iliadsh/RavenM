using UnityEngine;

namespace RavenM
{    
    public class SpawnProjectilePacket
    {
        public int SourceId;

        public int NameHash;

        public ulong Mod;

        public Vector3 Position;

        public Quaternion Rotation;

        public bool performInfantryInitialMuzzleTravel;

        public float initialMuzzleTravelDistance;

        /// <summary>
        /// Used for potential updates to position/rotation.
        /// </summary>
        public int ProjectileId;
    }
}
