using ProtoBuf;

namespace RavenM
{
    [ProtoContract]
    public class BattleStatePacket
    {
        [ProtoMember(1)]
        public int[] RemainingBattalions;

        [ProtoMember(2)]
        public int[] Tickets;
    }
}
