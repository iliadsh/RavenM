using System;
using ProtoBuf;

namespace RavenM
{
    /// <summary>
    /// A general packet to be sent over the wire.
    /// </summary>
    [ProtoContract]
    class Packet
    {
        [ProtoMember(1)]
        public PacketType Id;

        [ProtoMember(2)]
        public Guid sender;

        [ProtoMember(3)]
        public byte[] data;
    }
}
