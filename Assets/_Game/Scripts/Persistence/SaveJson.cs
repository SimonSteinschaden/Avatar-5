using System;
using System.Collections.Generic;
using Avatar5.Contracts;
using UnityEngine;

namespace Avatar5.Persistence
{
    /// <summary>
    /// JSON (de)serialisation of saves with schema detection and migration. Older test schemas are upgraded;
    /// newer or unknown schemas are refused with <see cref="SaveLoadError.UnsupportedSchema"/>, never guessed.
    /// </summary>
    public static class SaveJson
    {
        [Serializable]
        private sealed class Header
        {
            public int schemaVersion = -1;
            /// <summary>Schema 0 (development builds before 17.10.2026) stored its version here.</summary>
            public int version = -1;
        }

        /// <summary>Schema 0: early test format, migrated on load.</summary>
        [Serializable]
        public sealed class SaveGameV0
        {
            public int version = 0;
            public string id;
            public int level;
            public string time;
            public List<V0Object> objects = new List<V0Object>();
        }

        [Serializable]
        public sealed class V0Object
        {
            public int objectId;
            public bool on;
        }

        public static string Write(SaveGameV1 save)
        {
            return JsonUtility.ToJson(save, true);
        }

        public static SaveLoadResult Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Fail(SaveLoadError.Corrupt, "empty file");
            Header header;
            try
            {
                header = JsonUtility.FromJson<Header>(json);
            }
            catch (ArgumentException e)
            {
                return Fail(SaveLoadError.Corrupt, e.Message);
            }
            if (header == null) return Fail(SaveLoadError.Corrupt, "no header");

            try
            {
                if (header.schemaVersion == SaveGameV1.Schema)
                {
                    var save = JsonUtility.FromJson<SaveGameV1>(json);
                    return save != null ? new SaveLoadResult { Save = save } : Fail(SaveLoadError.Corrupt, "unreadable v1");
                }
                if (header.schemaVersion < 0 && header.version == 0)
                {
                    return new SaveLoadResult { Save = Migrate(JsonUtility.FromJson<SaveGameV0>(json)), Detail = "migrated from schema 0" };
                }
            }
            catch (ArgumentException e)
            {
                return Fail(SaveLoadError.Corrupt, e.Message);
            }
            if (header.schemaVersion < 0 && header.version < 0) return Fail(SaveLoadError.Corrupt, "no schema version");
            var found = header.schemaVersion >= 0 ? header.schemaVersion : header.version;
            return Fail(SaveLoadError.UnsupportedSchema, "schema " + found + " is not supported by this build");
        }

        public static SaveGameV1 Migrate(SaveGameV0 old)
        {
            var save = new SaveGameV1
            {
                saveId = old.id,
                savedAtUtc = string.IsNullOrEmpty(old.time) ? "2026-01-01T00:00:00Z" : old.time,
                levelId = old.level,
                checkpointId = 0,
                levelCompleted = false
            };
            if (old.objects != null)
            {
                foreach (var o in old.objects)
                    save.worldObjects.Add(new SaveWorldObject { id = o.objectId, state = o.on ? WorldStateCodes.On : WorldStateCodes.Off });
            }
            return save;
        }

        private static SaveLoadResult Fail(SaveLoadError error, string detail)
        {
            return new SaveLoadResult { Error = error, Detail = detail };
        }
    }
}
