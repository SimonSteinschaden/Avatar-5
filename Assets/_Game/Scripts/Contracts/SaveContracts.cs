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
    }

    [Serializable]
    public sealed class SaveWorldObject
    {
        public int id;
        public int state;
    }

    /// <summary>Who took part at save time (session slot, not an account).</summary>
    [Serializable]
    public sealed class SavePlayerRecord
    {
        public int slot;
        public float health;
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
