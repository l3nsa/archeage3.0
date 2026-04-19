using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSHeroRequestRankDataPacket : GamePacket
    {
        public CSHeroRequestRankDataPacket() : base(CSOffsets.CSHeroRequestRankDataPacket, 5)
        {
        }

        public override void Read(PacketStream stream)
        {
        }

        public override void Execute()
        {
            HeroManager.Instance.SendRankData(Connection.ActiveChar);
        }
    }
}
