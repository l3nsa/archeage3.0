using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCHeroSeasonInfoPacket : GamePacket
    {
        private readonly HeroSeasonInfo _info;

        public SCHeroSeasonInfoPacket(HeroSeasonInfo info)
            : base(SCOffsets.SCHeroSeasonInfoPacket, 5)
        {
            _info = info;
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.Write(_info.SeasonId);
            stream.Write(_info.StartTime);
            stream.Write(_info.EndTime);
            stream.Write(_info.NominationStartTime);
            stream.Write(_info.VotingStartTime);
            stream.Write(_info.IsActive);
            return stream;
        }
    }
}
