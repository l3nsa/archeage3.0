using System.Collections.Generic;

using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Items;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCCofferContentsPacket : GamePacket
    {
        private readonly uint _doodadObjId;
        private readonly List<Item> _items;
        private readonly int _containerSize;

        public SCCofferContentsPacket(uint doodadObjId, List<Item> items, int containerSize)
            : base(SCOffsets.SCCofferContentsPacket, 5)
        {
            _doodadObjId = doodadObjId;
            _items = items;
            _containerSize = containerSize;
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.WriteBc(_doodadObjId);
            stream.Write((byte)_containerSize);
            stream.Write((byte)_items.Count);
            foreach (var item in _items)
            {
                stream.Write((byte)item.Slot);
                stream.Write(item);
            }

            return stream;
        }
    }
}
