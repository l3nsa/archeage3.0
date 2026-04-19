using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSHeroVotingPacket : GamePacket
    {
        private uint _candidateCharacterId;

        public CSHeroVotingPacket() : base(CSOffsets.CSHeroVotingPacket, 5)
        {
        }

        public override void Read(PacketStream stream)
        {
            _candidateCharacterId = stream.ReadUInt32();
        }

        public override void Execute()
        {
            HeroManager.Instance.ProcessVote(Connection.ActiveChar, _candidateCharacterId);
        }
    }
}
