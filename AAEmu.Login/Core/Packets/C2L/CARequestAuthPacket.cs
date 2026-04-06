using AAEmu.Commons.Network;
using AAEmu.Login.Core.Controllers;
using AAEmu.Login.Core.Network.Login;

namespace AAEmu.Login.Core.Packets.C2L
{
    public class CARequestAuthPacket : LoginPacket
    {
        public CARequestAuthPacket() : base(0x01)
        {
        }

        public override void Read(PacketStream stream)
        {
            var pFrom = stream.ReadUInt32();
            var pTo = stream.ReadUInt32();
            var svc = stream.ReadByte();
            var dev = stream.ReadBoolean();
            var account = stream.ReadString();
            var mac = stream.ReadBytes();
            var mac2 = stream.ReadBytes();
            var cpu = stream.ReadUInt64();

            LoginController.Login(Connection, account);

            // TODO: Challenge-response authentication is disabled. The ACChallengePacket
            // handshake is not yet implemented and enabling it would break the current
            // direct-login flow. A full challenge-response protocol (server sends challenge,
            // client signs it, server verifies) should be implemented for production use.
            // Connection.SendPacket(new ACChallengePacket());
        }
    }
}