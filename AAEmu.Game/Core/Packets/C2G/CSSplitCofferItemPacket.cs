using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Items;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSSplitCofferItemPacket : GamePacket
    {
        private uint _count;
        private ulong _srcId;
        private ulong _dstId;
        private SlotType _srcSlotType;
        private byte _srcSlot;
        private SlotType _dstSlotType;
        private byte _dstSlot;
        private long _dbDoodadId;

        public CSSplitCofferItemPacket() : base(CSOffsets.CSSplitCofferItemPacket, 5)
        {
        }

        public override void Read(PacketStream stream)
        {
            _count = stream.ReadUInt32();
            _srcId = stream.ReadUInt64();
            _dstId = stream.ReadUInt64();

            stream.ReadByte();
            _srcSlotType = (SlotType)stream.ReadByte();
            stream.ReadByte();
            _srcSlot = stream.ReadByte();

            stream.ReadByte();
            _dstSlotType = (SlotType)stream.ReadByte();
            stream.ReadByte();
            _dstSlot = stream.ReadByte();

            _dbDoodadId = stream.ReadInt64();

            _log.Debug("SplitCofferItem, srcId: {0}, dstId: {1}, count: {2}", _srcId, _dstId, _count);
        }

        public override void Execute()
        {
            CofferManager.Instance.SplitCofferItem(
                Connection.ActiveChar, _count, _srcId, _dstId,
                _srcSlotType, _srcSlot, _dstSlotType, _dstSlot, _dbDoodadId);
        }
    }
}
