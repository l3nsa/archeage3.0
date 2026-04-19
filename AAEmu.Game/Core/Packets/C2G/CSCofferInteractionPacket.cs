using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSCofferInteractionPacket : GamePacket
    {
        private uint _cofferDoodadObjId;
        private bool _start;

        public CSCofferInteractionPacket() : base(CSOffsets.CSCofferInteractionPacket, 5)
        {
        }

        public override void Read(PacketStream stream)
        {
            _cofferDoodadObjId = stream.ReadBc();
            _start = stream.ReadBoolean();

            _log.Debug("CofferInteraction, doodadObjId: {0}, start: {1}", _cofferDoodadObjId, _start);
        }

        public override void Execute()
        {
            CofferManager.Instance.InteractCoffer(Connection.ActiveChar, _cofferDoodadObjId, _start);
        }
    }
}
