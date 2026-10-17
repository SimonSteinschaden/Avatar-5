using System;
using System.IO;
using Avatar5.Contracts;
using Avatar5.Gameplay.Save;
using Avatar5.Gameplay.World;

namespace Avatar5.Persistence
{
    /// <summary>
    /// Host-local save files: "id.json" plus "id.bak.json". A save is written to a temp file, read back and
    /// validated, and only then swapped in; the previous good file becomes the backup. A crash at any point leaves
    /// at least one valid file. Loading falls back to the backup when the main file is broken.
    /// </summary>
    public sealed class AtomicSaveStore
    {
        private readonly string _directory;

        public AtomicSaveStore(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("directory");
            _directory = directory;
        }

        public string Directory { get { return _directory; } }

        public string PathFor(string saveId) { return Path.Combine(_directory, Sanitize(saveId) + ".json"); }
        public string BackupPathFor(string saveId) { return Path.Combine(_directory, Sanitize(saveId) + ".bak.json"); }
        public string TempPathFor(string saveId) { return Path.Combine(_directory, Sanitize(saveId) + ".tmp"); }

        public SaveLoadResult Save(SaveGameV1 save)
        {
            string detail;
            var error = SaveValidator.Validate(save, null, out detail);
            if (error != SaveLoadError.None) return new SaveLoadResult { Error = error, Detail = "refusing to write: " + detail };
            try
            {
                System.IO.Directory.CreateDirectory(_directory);
                var target = PathFor(save.saveId);
                var temp = TempPathFor(save.saveId);
                var json = SaveJson.Write(save);
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                // Read back before touching the last good file.
                var check = SaveJson.Read(File.ReadAllText(temp));
                if (!check.Success) return new SaveLoadResult { Error = SaveLoadError.IoError, Detail = "verification failed: " + check.Detail };

                if (File.Exists(target)) File.Replace(temp, target, BackupPathFor(save.saveId), true);
                else File.Move(temp, target);
                return new SaveLoadResult { Save = save };
            }
            catch (IOException e) { return new SaveLoadResult { Error = SaveLoadError.IoError, Detail = e.Message }; }
            catch (UnauthorizedAccessException e) { return new SaveLoadResult { Error = SaveLoadError.IoError, Detail = e.Message }; }
        }

        /// <summary>Loads and validates (against <paramref name="level"/> when given); uses the backup if needed.</summary>
        public SaveLoadResult Load(string saveId, LevelDefinition level)
        {
            var main = TryLoad(PathFor(saveId), level);
            // A save from another build is a version question, not a damaged file: don't silently fall back.
            if (main.Success || main.Error == SaveLoadError.UnsupportedSchema || main.Error == SaveLoadError.ContentMismatch) return main;
            var backup = TryLoad(BackupPathFor(saveId), level);
            if (backup.Success)
            {
                backup.UsedBackup = true;
                backup.Detail = "main save unusable (" + main.Error + ": " + main.Detail + "), loaded backup";
                return backup;
            }
            return main.Error == SaveLoadError.NotFound && backup.Error != SaveLoadError.NotFound ? backup : main;
        }

        public bool Exists(string saveId)
        {
            return File.Exists(PathFor(saveId)) || File.Exists(BackupPathFor(saveId));
        }

        private static SaveLoadResult TryLoad(string path, LevelDefinition level)
        {
            if (!File.Exists(path)) return new SaveLoadResult { Error = SaveLoadError.NotFound, Detail = Path.GetFileName(path) };
            string json;
            try { json = File.ReadAllText(path); }
            catch (IOException e) { return new SaveLoadResult { Error = SaveLoadError.IoError, Detail = e.Message }; }
            var result = SaveJson.Read(json);
            if (!result.Success) return result;
            string detail;
            var error = SaveValidator.Validate(result.Save, level, out detail);
            if (error != SaveLoadError.None) return new SaveLoadResult { Error = error, Detail = detail };
            return result;
        }

        private static string Sanitize(string saveId)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) saveId = saveId.Replace(c, '_');
            return saveId;
        }
    }
}
