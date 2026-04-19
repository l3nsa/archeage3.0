using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSHeroAbstainPacket : GamePacket
    {
        public CSHeroAbstainPacket() : base(CSOffsets.CSHeroAbstainPacket, 5)
        {
        }

        public override void Read(PacketStream stream)
        {
        }

        public override void Execute()
        {
            HeroManager.Instance.ProcessAbstain(Connection.ActiveChar);
        }
    }
}
