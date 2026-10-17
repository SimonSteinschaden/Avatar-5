using System;
using System.Collections.Generic;
using Avatar5.Contracts;

namespace Avatar5.Gameplay.Progression
{
    public enum TalentId : byte
    {
        /// <summary>+12 % damage per rank.</summary>
        Power = 1,
        /// <summary>-10 % cooldowns per rank.</summary>
        Focus = 2,
        /// <summary>+15 max health per rank.</summary>
        Vigor = 3
    }

    /// <summary>Balancing of experience, levels and talents (code defaults until designers own an asset).</summary>
    public sealed class ProgressionRules
    {
        public int MaxLevel = 10;
        public int MaxRank = 3;
        public float DamagePerPowerRank = 0.12f;
        public float CooldownPerFocusRank = 0.10f;
        public float HealthPerVigorRank = 15f;
        public int EncounterXp = 60;
        public int BossXp = 150;
        public int LevelCompletedXp = 100;

        /// <summary>Total XP needed to reach <paramref name="level"/> (level 1 = 0 XP; each step costs 100 × previous level).</summary>
        public int XpForLevel(int level)
        {
            var total = 0;
            for (var l = 1; l < level; l++) total += 100 * l;
            return total;
        }

        public int LevelForXp(int xp)
        {
            var level = 1;
            while (level < MaxLevel && xp >= XpForLevel(level + 1)) level++;
            return level;
        }
    }

    /// <summary>Progress of one player slot in the host's campaign (v1: slot-based, host-owned).</summary>
    public sealed class PlayerProgress
    {
        public int Slot;
        public int Xp;
        public readonly Dictionary<TalentId, int> Ranks = new Dictionary<TalentId, int>();

        public int Rank(TalentId talent)
        {
            int r;
            return Ranks.TryGetValue(talent, out r) ? r : 0;
        }

        public int SpentPoints
        {
            get
            {
                var total = 0;
                foreach (var r in Ranks.Values) total += r;
                return total;
            }
        }
    }

    public enum TalentResult : byte { Accepted, NoPoints, MaxRank, UnknownTalent }

    /// <summary>
    /// Host-only progression: XP from rewards (each RewardGrantId once per player), levels, talent points and the
    /// resulting stats for the combat core. Rewards go to every player slot present when they are granted.
    /// </summary>
    public sealed class ProgressionService : IStatProvider
    {
        private readonly ProgressionRules _rules;
        private readonly Dictionary<int, PlayerProgress> _players = new Dictionary<int, PlayerProgress>();
        private readonly HashSet<string> _granted = new HashSet<string>(StringComparer.Ordinal);

        public event Action<int> Changed;

        public ProgressionService(ProgressionRules rules)
        {
            _rules = rules ?? new ProgressionRules();
        }

        public ProgressionRules Rules { get { return _rules; } }

        public PlayerProgress Get(int slot)
        {
            PlayerProgress p;
            if (!_players.TryGetValue(slot, out p))
            {
                p = new PlayerProgress { Slot = slot };
                _players.Add(slot, p);
            }
            return p;
        }

        public IEnumerable<PlayerProgress> All { get { return _players.Values; } }

        public int LevelOf(int slot) { return _rules.LevelForXp(Get(slot).Xp); }

        /// <summary>Points available = level - 1 - spent.</summary>
        public int FreePoints(int slot) { return LevelOf(slot) - 1 - Get(slot).SpentPoints; }

        /// <summary>Grants XP for a reward to the given slots; the same (reward, slot) pair never counts twice.</summary>
        public int Grant(string rewardGrantId, int xp, IEnumerable<int> slots)
        {
            var granted = 0;
            foreach (var slot in slots)
            {
                if (!_granted.Add(rewardGrantId + "@" + slot)) continue;
                Get(slot).Xp += xp;
                granted++;
                var handler = Changed;
                if (handler != null) handler(slot);
            }
            return granted;
        }

        public TalentResult ChooseTalent(int slot, TalentId talent)
        {
            if (!Enum.IsDefined(typeof(TalentId), talent)) return TalentResult.UnknownTalent;
            var p = Get(slot);
            if (p.Rank(talent) >= _rules.MaxRank) return TalentResult.MaxRank;
            if (FreePoints(slot) <= 0) return TalentResult.NoPoints;
            p.Ranks[talent] = p.Rank(talent) + 1;
            var handler = Changed;
            if (handler != null) handler(slot);
            return TalentResult.Accepted;
        }

        /// <summary>Stats for a player actor (actor id = slot + 1); enemies and unknown ids get neutral stats.</summary>
        public ActorStats GetStats(ulong actorId)
        {
            if (actorId == 0 || actorId > 1000) return ActorStats.Neutral;
            PlayerProgress p;
            if (!_players.TryGetValue((int)actorId - 1, out p)) return ActorStats.Neutral;
            return new ActorStats
            {
                DamageMultiplier = 1f + _rules.DamagePerPowerRank * p.Rank(TalentId.Power),
                CooldownMultiplier = Math.Max(0.5f, 1f - _rules.CooldownPerFocusRank * p.Rank(TalentId.Focus)),
                MaxHealthBonus = _rules.HealthPerVigorRank * p.Rank(TalentId.Vigor)
            };
        }

        // ---- save --------------------------------------------------------------------------

        public List<string> GrantedKeys() { return new List<string>(_granted); }

        public void Restore(IEnumerable<SavePlayerRecord> records, IEnumerable<string> grantedKeys)
        {
            _players.Clear();
            _granted.Clear();
            if (records != null)
            {
                foreach (var r in records)
                {
                    var p = Get(r.slot);
                    p.Xp = Math.Max(0, r.xp);
                    if (r.talents == null) continue;
                    foreach (var t in r.talents)
                    {
                        if (Enum.IsDefined(typeof(TalentId), (TalentId)t.talent)) p.Ranks[(TalentId)t.talent] = Math.Min(_rules.MaxRank, Math.Max(0, t.rank));
                    }
                }
            }
            if (grantedKeys != null) foreach (var k in grantedKeys) _granted.Add(k);
        }

        public void WriteInto(List<SavePlayerRecord> records)
        {
            foreach (var p in _players.Values)
            {
                SavePlayerRecord record = null;
                foreach (var r in records) if (r.slot == p.Slot) record = r;
                if (record == null)
                {
                    record = new SavePlayerRecord { slot = p.Slot };
                    records.Add(record);
                }
                record.xp = p.Xp;
                record.talents = new List<SaveTalentRank>();
                foreach (var pair in p.Ranks) record.talents.Add(new SaveTalentRank { talent = (int)pair.Key, rank = pair.Value });
            }
        }
    }
}
