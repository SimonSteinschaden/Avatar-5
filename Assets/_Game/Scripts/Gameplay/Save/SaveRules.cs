using System;
using System.Collections.Generic;
using System.Globalization;
using Avatar5.Contracts;
using Avatar5.Gameplay.World;

namespace Avatar5.Gameplay.Save
{
    /// <summary>Checks a save against the schema and (optionally) the level it will be restored into.</summary>
    public static class SaveValidator
    {
        public static SaveLoadError Validate(SaveGameV1 save, LevelDefinition level, out string detail)
        {
            detail = null;
            if (save == null) { detail = "empty save"; return SaveLoadError.Corrupt; }
            if (save.schemaVersion != SaveGameV1.Schema) { detail = "schema " + save.schemaVersion; return SaveLoadError.UnsupportedSchema; }
            if (save.contentVersion != VersionContracts.ContentSchemaVersion)
            {
                detail = "content " + save.contentVersion + " != " + VersionContracts.ContentSchemaVersion;
                return SaveLoadError.ContentMismatch;
            }
            if (string.IsNullOrEmpty(save.saveId)) { detail = "saveId missing"; return SaveLoadError.Corrupt; }
            DateTime ignored;
            if (!DateTime.TryParse(save.savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out ignored))
            {
                detail = "savedAtUtc invalid";
                return SaveLoadError.Corrupt;
            }
            if (save.levelId <= 0 || save.worldObjects == null || save.playerRecords == null || save.completedRewardIds == null)
            {
                detail = "missing sections";
                return SaveLoadError.Corrupt;
            }

            var seen = new HashSet<int>();
            foreach (var o in save.worldObjects)
            {
                if (o == null || !seen.Add(o.id)) { detail = "duplicate or empty world object"; return SaveLoadError.Corrupt; }
            }

            if (level == null) return SaveLoadError.None;
            if (level.LevelId != save.levelId) { detail = "save is for level " + save.levelId + ", loaded level is " + level.LevelId; return SaveLoadError.ContentMismatch; }
            var known = new HashSet<int>();
            foreach (var o in level.Objects) known.Add(o.Id);
            foreach (var o in save.worldObjects)
            {
                if (!known.Contains(o.id)) { detail = "unknown world object id " + o.id; return SaveLoadError.UnknownContentId; }
            }
            return SaveLoadError.None;
        }
    }

    /// <summary>
    /// Remembers which rewards were granted in this campaign. Granting the same RewardGrantId twice (rejoin,
    /// replayed event) is a no-op, so progress can never be duplicated.
    /// </summary>
    public sealed class RewardLedger
    {
        private readonly HashSet<string> _granted = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _order = new List<string>();

        public int Count { get { return _order.Count; } }

        public bool TryGrant(string rewardGrantId)
        {
            if (string.IsNullOrEmpty(rewardGrantId) || !_granted.Add(rewardGrantId)) return false;
            _order.Add(rewardGrantId);
            return true;
        }

        public bool Contains(string rewardGrantId)
        {
            return _granted.Contains(rewardGrantId);
        }

        public List<string> ToList()
        {
            return new List<string>(_order);
        }

        public void Load(IEnumerable<string> ids)
        {
            _granted.Clear();
            _order.Clear();
            if (ids == null) return;
            foreach (var id in ids) TryGrant(id);
        }

        /// <summary>Stable id for completing a level once per campaign.</summary>
        public static string LevelCompleted(int levelId)
        {
            return "level:" + levelId.ToString(CultureInfo.InvariantCulture) + ":completed";
        }
    }

    /// <summary>Builds a save from the host's world state and restores the world from a validated save.</summary>
    public static class SaveSnapshots
    {
        public static SaveGameV1 Capture(string saveId, WorldStateService world, int checkpointId, RewardLedger rewards,
            IEnumerable<SavePlayerRecord> players, DateTime utcNow)
        {
            var save = new SaveGameV1
            {
                saveId = saveId,
                savedAtUtc = utcNow.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                levelId = world.LevelId,
                checkpointId = checkpointId,
                levelCompleted = world.IsCompleted
            };
            foreach (var pair in world.Snapshot()) save.worldObjects.Add(new SaveWorldObject { id = pair.Key, state = pair.Value });
            if (players != null) save.playerRecords.AddRange(players);
            if (rewards != null) save.completedRewardIds.AddRange(rewards.ToList());
            return save;
        }

        public static void Restore(SaveGameV1 save, WorldStateService world, RewardLedger rewards)
        {
            var snapshot = new List<KeyValuePair<int, int>>();
            foreach (var o in save.worldObjects) snapshot.Add(new KeyValuePair<int, int>(o.id, o.state));
            world.Restore(snapshot, save.levelCompleted);
            if (rewards != null) rewards.Load(save.completedRewardIds);
        }
    }
}
