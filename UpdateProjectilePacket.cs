using ProtoBuf;
using HarmonyLib;
using System.IO;
using Steamworks;
using UnityEngine;
using System.Collections.Generic;

namespace RavenM
{
    [ProtoContract]
    public class UpdateProjectilePacket
    {
        [ProtoMember(1)]
        public int Id;

        [ProtoMember(2)]
        public Vector3 Position;

        [ProtoMember(3)]
        public Vector3 Velocity;
    }

    [ProtoContract]
    public class BulkProjectileUpdate
    {
        [ProtoMember(1)]
        public List<UpdateProjectilePacket> Updates;
    }
}
