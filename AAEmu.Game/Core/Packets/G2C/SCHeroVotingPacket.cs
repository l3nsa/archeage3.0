using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCHeroVotingPacket : GamePacket
    {
        private readonly uint _candidateId;
        private readonly bool _success;

        public SCHeroVotingPacket(uint candidateId, bool success)
            : base(SCOffsets.SCHeroVotingPacket, 5)
        {
            _candidateId = candidateId;
            _success = success;
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.Write(_candidateId);
            stream.Write(_success);
            return stream;
        }
    }
}
