using System.Collections.Generic;

using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCHeroCandidateListPacket : GamePacket
    {
        private readonly uint _factionId;
        private readonly List<HeroCandidateInfo> _candidates;

        public SCHeroCandidateListPacket(uint factionId, List<HeroCandidateInfo> candidates)
            : base(SCOffsets.SCHeroCandidateListPacket, 5)
        {
            _factionId = factionId;
            _candidates = candidates;
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.Write(_factionId);
            stream.Write((byte)_candidates.Count);
            foreach (var candidate in _candidates)
            {
                stream.Write(candidate.CharacterId);
                stream.Write(candidate.Name);
                stream.Write(candidate.VoteCount);
            }

            return stream;
        }
    }
}
