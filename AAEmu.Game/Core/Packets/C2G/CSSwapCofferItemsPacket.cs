using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Items;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSSwapCofferItemsPacket : GamePacket
    {
        private ulong _fromItemId;
        private ulong _toItemId;
        private SlotType _fromSlotType;
        private byte _fromSlot;
        private SlotType _toSlotType;
        private byte _toSlot;
        private long _dbDoodadId;

        public CSSwapCofferItemsPacket() : base(CSOffsets.CSSwapCofferItemsPacket, 5)
        {
        }

        public override void Read(PacketStream stream)
        {
            _fromItemId = stream.ReadUInt64();
            _toItemId = stream.ReadUInt64();

            stream.ReadByte();
            _fromSlotType = (SlotType)stream.ReadByte();
            stream.ReadByte();
            _fromSlot = stream.ReadByte();

            stream.ReadByte();
            _toSlotType = (SlotType)stream.ReadByte();
            stream.ReadByte();
            _toSlot = stream.ReadByte();

            _dbDoodadId = stream.ReadInt64();

            _log.Debug(
                "SwapCofferItems, Item: {0} -> {1}, SlotType: {2} -> {3}, Slot: {4} -> {5}",
                _fromItemId, _toItemId, _fromSlotType, _toSlotType, _fromSlot, _toSlot);
        }

        public override void Execute()
        {
            CofferManager.Instance.SwapCofferItems(
                Connection.ActiveChar, _fromItemId, _toItemId,
                _fromSlotType, _fromSlot, _toSlotType, _toSlot, _dbDoodadId);
        }
    }
}
