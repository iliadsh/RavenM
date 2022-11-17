using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
namespace RavenM.RSPatch.Packets
{
    public class CustomObjectUpdatePacket
    {
        /// <summary>
        /// The actor that is leaving the seat.
        /// </summary>
        public int Id;

        public Vector3 Position;

        public Vector3 Rotation;

        public bool Active;

    }
    public class BulkCustomObjectUpdate
    {
        public List<CustomObjectUpdatePacket> Updates;
    }
}
