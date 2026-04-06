using System;
using System.Security.Cryptography;

using AAEmu.Commons.Network;
using AAEmu.Login.Core.Network.Login;

namespace AAEmu.Login.Core.Packets.L2C
{
    public class ACAuthResponsePacket : LoginPacket
    {
        private readonly ulong _accountId;
        private readonly string _wsk;
        private readonly byte _slotCount;

        public ACAuthResponsePacket(ulong accountId, byte slotCount) : base(0x03)
        {
            _accountId = accountId;
            var sessionKeyBytes = RandomNumberGenerator.GetBytes(16);
            _wsk = BitConverter.ToString(sessionKeyBytes).Replace("-", "").ToUpperInvariant();
            _slotCount = slotCount;
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.Write(_accountId);
            stream.Write(_wsk, true);
            stream.Write(_slotCount);
            //stream.Write((short)0); //add for 5.1

            return stream;
        }
    }
}
