using System.Collections.Generic;

using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCHeroRankingListPacket : GamePacket
    {
        private readonly uint _factionId;
        private readonly List<HeroInfo> _heroes;

        public SCHeroRankingListPacket(uint factionId, List<HeroInfo> heroes)
            : base(SCOffsets.SCHeroRankingListPacket, 5)
        {
            _factionId = factionId;
            _heroes = heroes;
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.Write(_factionId);
            stream.Write((byte)_heroes.Count);
            foreach (var hero in _heroes)
            {
                stream.Write(hero.CharacterId);
                stream.Write(hero.Name);
                stream.Write(hero.Rank);
                stream.Write(hero.Score);
            }

            return stream;
        }
    }
}
