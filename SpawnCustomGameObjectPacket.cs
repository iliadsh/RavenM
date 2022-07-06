using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RavenM
{
    public class SpawnCustomGameObjectPacket
    {
        public int SourceID;
        public string PrefabHash;
        public Vector3 Position;
        public Vector3 Rotation;
    }
}
