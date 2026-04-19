using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSHeroRankingListPacket : GamePacket
    {
        public CSHeroRankingListPacket() : base(CSOffsets.CSHeroRankingListPacket, 5)
        {
        }

        public override void Read(PacketStream stream)
        {
        }

        public override void Execute()
        {
            HeroManager.Instance.SendHeroRankingList(Connection.ActiveChar);
        }
    }
}
