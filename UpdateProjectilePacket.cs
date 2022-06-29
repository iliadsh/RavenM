using HarmonyLib;
using System.IO;
using Steamworks;
using UnityEngine;
using System.Collections.Generic;

namespace RavenM
{
    public class UpdateProjectilePacket
    {
        public int Id;

        public Vector3 Position;

        public Vector3 Velocity;

        public bool Boom;
    }

    public class BulkProjectileUpdate
    {
        public List<UpdateProjectilePacket> Updates;
    }
}
