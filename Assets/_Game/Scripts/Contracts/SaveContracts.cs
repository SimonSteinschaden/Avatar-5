using System;
using System.Collections.Generic;

namespace Avatar5.Contracts
{
    /// <summary>
    /// Campaign save, version 1. Host-owned (v1 rule): it belongs to the person who hosted; clients keep no
    /// campaign progress. Plain public fields so the Unity JSON serializer can read and write it.
    /// </summary>
    [Serializable]
    public sealed class SaveGameV1
    {
        public const int Schema = 1;

        public int schemaVersion = Schema;
        public int contentVersion = VersionContracts.ContentSchemaVersion;
        public string saveId;
        /// <summary>ISO 8601 UTC, e.g. 2026-10-17T12:00:00Z.</summary>
        public string savedAtUtc;
        public int levelId;
        /// <summary>World object id of the checkpoint that triggered the save (a gate, the goal), 0 = manual.</summary>
        public int checkpointId;
        public bool levelCompleted;
        public List<SaveWorldObject> worldObjects = new List<SaveWorldObject>();
        public List<SavePlayerRecord> playerRecords = new List<SavePlayerRecord>();
        public List<string> completedRewardIds = new List<string>();
        /// <summary>Added 17.10.2026 (S06-02): "rewardId@slot" pairs already credited as XP; missing in older v1 files = none.</summary>
        public List<string> progressionGrants = new List<string>();
    }

    [Serializable]
    public sealed class SaveWorldObject
    {
        public int id;
        public int state;
    }

    /// <summary>Who took part at save time (session slot, not an account) and that slot's progress.</summary>
    [Serializable]
    public sealed class SavePlayerRecord
    {
        public int slot;
        public float health;
        /// <summary>Added 17.10.2026 (S06-02); missing in older v1 files = 0.</summary>
        public int xp;
        public List<SaveTalentRank> talents = new List<SaveTalentRank>();
    }

    [Serializable]
    public sealed class SaveTalentRank
    {
        public int talent;
        public int rank;
    }

    /// <summary>Readable progression of one player for HUD/menus (host-written, replicated).</summary>
    [Serializable]
    public struct PlayerProgressView
    {
        public int Level;
        public int Xp;
        public int XpForNextLevel;
        public int FreePoints;
        public int PowerRank;
        public int FocusRank;
        public int VigorRank;
    }

    public interface IProgressionSource
    {
        PlayerProgressView Progress { get; }
    }

    public enum SaveLoadError : byte
    {
        None,
        NotFound,
        Corrupt,
        UnsupportedSchema,
        ContentMismatch,
        UnknownContentId,
        IoError
    }

    [Serializable]
    public struct SaveLoadResult
    {
        public SaveLoadError Error;
        public string Detail;
        public bool UsedBackup;
        public SaveGameV1 Save;

        public bool Success { get { return Error == SaveLoadError.None && Save != null; } }
    }
}
