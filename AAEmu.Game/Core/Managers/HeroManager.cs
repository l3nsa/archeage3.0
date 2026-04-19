using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Faction;

using NLog;

namespace AAEmu.Game.Core.Managers
{
    public class HeroManager : Singleton<HeroManager>
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        // Current heroes per faction (factionId -> list of HeroInfo)
        private readonly ConcurrentDictionary<uint, List<HeroInfo>> _heroes = new();

        // Hero candidates per faction (factionId -> list of HeroCandidateInfo)
        private readonly ConcurrentDictionary<uint, List<HeroCandidateInfo>> _candidates = new();

        // Current season info
        private HeroSeasonInfo _seasonInfo;

        // Known faction IDs for hero system
        public const uint FactionNuia = 148;
        public const uint FactionHaranya = 149;
        public const uint FactionPirate = 114;

        public void Load()
        {
            _seasonInfo = new HeroSeasonInfo
            {
                SeasonId = 1,
                StartTime = DateTime.UtcNow.AddDays(-30),
                EndTime = DateTime.UtcNow.AddDays(30),
                NominationStartTime = DateTime.UtcNow.AddDays(-7),
                VotingStartTime = DateTime.UtcNow.AddDays(20),
                IsActive = true
            };

            // Initialize empty hero lists for the main factions
            _heroes.TryAdd(FactionNuia, new List<HeroInfo>());
            _heroes.TryAdd(FactionHaranya, new List<HeroInfo>());
            _heroes.TryAdd(FactionPirate, new List<HeroInfo>());

            _candidates.TryAdd(FactionNuia, new List<HeroCandidateInfo>());
            _candidates.TryAdd(FactionHaranya, new List<HeroCandidateInfo>());
            _candidates.TryAdd(FactionPirate, new List<HeroCandidateInfo>());

            _log.Info("HeroManager initialized (season {0})", _seasonInfo.SeasonId);
        }

        /// <summary>
        /// Send hero season info to a character on login
        /// </summary>
        public void OnCharacterLogin(Character character)
        {
            if (_seasonInfo != null && _seasonInfo.IsActive)
            {
                character.SendPacket(new SCHeroSeasonInfoPacket(_seasonInfo));
            }
        }

        /// <summary>
        /// Handle request for hero ranking list
        /// </summary>
        public void SendHeroRankingList(Character character)
        {
            var factionId = character.Faction?.Id ?? 0;
            var heroes = GetHeroesForFaction(factionId);
            character.SendPacket(new SCHeroRankingListPacket(factionId, heroes));
        }

        /// <summary>
        /// Handle request for hero candidate list
        /// </summary>
        public void SendHeroCandidateList(Character character)
        {
            var factionId = character.Faction?.Id ?? 0;
            var candidates = GetCandidatesForFaction(factionId);
            character.SendPacket(new SCHeroCandidateListPacket(factionId, candidates));
        }

        /// <summary>
        /// Handle a hero vote
        /// </summary>
        public void ProcessVote(Character character, uint candidateCharacterId)
        {
            var factionId = character.Faction?.Id ?? 0;
            if (!_candidates.TryGetValue(factionId, out var candidates))
                return;

            var candidate = candidates.FirstOrDefault(c => c.CharacterId == candidateCharacterId);
            if (candidate != null)
            {
                candidate.VoteCount++;
                _log.Info("Character {0} voted for hero candidate {1} (votes: {2})",
                    character.Name, candidateCharacterId, candidate.VoteCount);
            }

            character.SendPacket(new SCHeroVotingPacket(candidateCharacterId, true));
        }

        /// <summary>
        /// Handle hero abstain (character declines hero candidacy)
        /// </summary>
        public void ProcessAbstain(Character character)
        {
            var factionId = character.Faction?.Id ?? 0;
            if (!_candidates.TryGetValue(factionId, out var candidates))
                return;

            var candidate = candidates.FirstOrDefault(c => c.CharacterId == character.Id);
            if (candidate != null)
            {
                candidates.Remove(candidate);
                _log.Info("Character {0} abstained from hero candidacy", character.Name);
            }
        }

        /// <summary>
        /// Handle rank data request
        /// </summary>
        public void SendRankData(Character character)
        {
            var factionId = character.Faction?.Id ?? 0;
            var heroes = GetHeroesForFaction(factionId);
            character.SendPacket(new SCHeroRankingListPacket(factionId, heroes));
        }

        private List<HeroInfo> GetHeroesForFaction(uint factionId)
        {
            return _heroes.TryGetValue(factionId, out var heroes) ? heroes : new List<HeroInfo>();
        }

        private List<HeroCandidateInfo> GetCandidatesForFaction(uint factionId)
        {
            return _candidates.TryGetValue(factionId, out var candidates) ? candidates : new List<HeroCandidateInfo>();
        }
    }

    public class HeroSeasonInfo
    {
        public uint SeasonId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime NominationStartTime { get; set; }
        public DateTime VotingStartTime { get; set; }
        public bool IsActive { get; set; }
    }

    public class HeroInfo
    {
        public uint CharacterId { get; set; }
        public string Name { get; set; }
        public uint FactionId { get; set; }
        public int Rank { get; set; }
        public int Score { get; set; }
    }

    public class HeroCandidateInfo
    {
        public uint CharacterId { get; set; }
        public string Name { get; set; }
        public uint FactionId { get; set; }
        public int VoteCount { get; set; }
    }
}
