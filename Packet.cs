using System;

namespace RavenM
{
    /// <summary>
    /// A general packet to be sent over the wire.
    /// </summary>
    public class Packet
    {
        public PacketType Id;

        public Guid sender;

        public byte[] data;
    }
}
