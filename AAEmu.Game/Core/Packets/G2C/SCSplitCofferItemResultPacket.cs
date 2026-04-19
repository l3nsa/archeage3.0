using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCSplitCofferItemResultPacket : GamePacket
    {
        private readonly bool _success;
        private readonly ulong _itemId;
        private readonly int _count;

        public SCSplitCofferItemResultPacket(bool success, ulong itemId, int count)
            : base(SCOffsets.SCSplitCofferItemResultPacket, 5)
        {
            _success = success;
            _itemId = itemId;
            _count = count;
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.Write(_success);
            stream.Write(_itemId);
            stream.Write(_count);
            return stream;
        }
    }
}
