using System;
using System.Collections.Generic;
using System.IO;
using Avatar5.Contracts;
using Avatar5.Gameplay.Save;
using Avatar5.Gameplay.World;
using Avatar5.Persistence;
using NUnit.Framework;

namespace Avatar5.Tests.EditMode
{
    public sealed class SaveSystemTests
    {
        private string _dir;
        private AtomicSaveStore _store;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "avatar5-save-tests-" + Guid.NewGuid().ToString("N"));
            _store = new AtomicSaveStore(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static LevelDefinition Level()
        {
            var level = new LevelDefinition { LevelId = 1, Key = "level01" };
            level.Objects.Add(new WorldObjectDefinition { Id = 10, Kind = WorldObjectKind.Lever, Position = new Float2(0f, 0f) });
            level.Objects.Add(new WorldObjectDefinition { Id = 11, Kind = WorldObjectKind.Gate, Requires = new[] { 10 } });
            level.Objects.Add(new WorldObjectDefinition { Id = 99, Kind = WorldObjectKind.Goal, Position = new Float2(0f, 20f), Requires = new[] { 11 } });
            return level;
        }

        private static SaveGameV1 SaveOf(WorldStateService world, RewardLedger rewards = null, string id = "campaign")
        {
            return SaveSnapshots.Capture(id, world, 11, rewards, new[] { new SavePlayerRecord { slot = 0, health = 100f } },
                new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc));
        }

        [Test]
        public void SaveLoad_RoundTripRestoresWorldAndRewards()
        {
            var world = new WorldStateService(Level());
            world.TryInteract(1, 10, new Float2(0f, 0f));
            var rewards = new RewardLedger();
            rewards.TryGrant("level:1:completed");
            Assert.IsTrue(_store.Save(SaveOf(world, rewards)).Success);

            var loaded = _store.Load("campaign", Level());
            Assert.IsTrue(loaded.Success, loaded.Detail);
            Assert.IsFalse(loaded.UsedBackup);
            Assert.AreEqual("2026-10-17T12:00:00Z", loaded.Save.savedAtUtc);
            Assert.AreEqual(11, loaded.Save.checkpointId);

            var restored = new WorldStateService(Level());
            var restoredRewards = new RewardLedger();
            SaveSnapshots.Restore(loaded.Save, restored, restoredRewards);
            Assert.AreEqual(WorldStateCodes.On, restored.GetState(10));
            Assert.AreEqual(WorldStateCodes.On, restored.GetState(11));
            Assert.IsFalse(restoredRewards.TryGrant("level:1:completed"), "reward must not be granted again after load");
        }

        [Test]
        public void SecondSave_KeepsPreviousAsBackup_CorruptMainFallsBack()
        {
            var world = new WorldStateService(Level());
            Assert.IsTrue(_store.Save(SaveOf(world)).Success);
            world.TryInteract(1, 10, new Float2(0f, 0f));
            Assert.IsTrue(_store.Save(SaveOf(world)).Success);
            Assert.IsTrue(File.Exists(_store.BackupPathFor("campaign")));

            File.WriteAllText(_store.PathFor("campaign"), "{ \"schemaVersion\": 1, broken");
            var loaded = _store.Load("campaign", Level());
            Assert.IsTrue(loaded.Success, loaded.Detail);
            Assert.IsTrue(loaded.UsedBackup);
            Assert.AreEqual(WorldStateCodes.Off, loaded.Save.worldObjects.Find(o => o.id == 10).state, "backup is the older save");
        }

        [Test]
        public void CrashBeforeSwap_LeavesLastGoodSaveAndNextSaveStillWorks()
        {
            var world = new WorldStateService(Level());
            Assert.IsTrue(_store.Save(SaveOf(world)).Success);
            // Simulate a crash mid-write: half a temp file exists, the swap never happened.
            File.WriteAllText(_store.TempPathFor("campaign"), "{ \"schemaVersion\": 1, \"saveId\": \"camp");
            var loaded = _store.Load("campaign", Level());
            Assert.IsTrue(loaded.Success, loaded.Detail);
            Assert.IsFalse(loaded.UsedBackup);

            world.TryInteract(1, 10, new Float2(0f, 0f));
            Assert.IsTrue(_store.Save(SaveOf(world)).Success, "a stale temp file must not block saving");
            Assert.AreEqual(WorldStateCodes.On, _store.Load("campaign", Level()).Save.worldObjects.Find(o => o.id == 10).state);
        }

        [Test]
        public void MissingSave_IsNotFound()
        {
            Assert.AreEqual(SaveLoadError.NotFound, _store.Load("nothing", Level()).Error);
            Assert.IsFalse(_store.Exists("nothing"));
        }

        [Test]
        public void UnknownSchema_IsRefusedWithoutUsingBackup()
        {
            var world = new WorldStateService(Level());
            _store.Save(SaveOf(world));
            _store.Save(SaveOf(world));
            File.WriteAllText(_store.PathFor("campaign"), "{ \"schemaVersion\": 7, \"saveId\": \"campaign\" }");
            var loaded = _store.Load("campaign", Level());
            Assert.AreEqual(SaveLoadError.UnsupportedSchema, loaded.Error);
            StringAssert.Contains("schema 7", loaded.Detail);
            Assert.IsFalse(loaded.UsedBackup);
        }

        [Test]
        public void SchemaZero_IsMigrated()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_store.PathFor("old"),
                "{ \"version\": 0, \"id\": \"old\", \"level\": 1, \"time\": \"2026-09-01T10:00:00Z\", \"objects\": [ { \"objectId\": 10, \"on\": true }, { \"objectId\": 11, \"on\": true } ] }");
            var loaded = _store.Load("old", Level());
            Assert.IsTrue(loaded.Success, loaded.Detail);
            Assert.AreEqual(SaveGameV1.Schema, loaded.Save.schemaVersion);
            Assert.AreEqual(WorldStateCodes.On, loaded.Save.worldObjects.Find(o => o.id == 11).state);
            Assert.IsNotNull(loaded.Save.completedRewardIds);
        }

        [Test]
        public void ContentMismatchAndUnknownIds_AreReportedPrecisely()
        {
            var world = new WorldStateService(Level());
            var save = SaveOf(world);
            save.contentVersion = 99;
            string detail;
            Assert.AreEqual(SaveLoadError.ContentMismatch, SaveValidator.Validate(save, Level(), out detail));

            save = SaveOf(world);
            save.worldObjects.Add(new SaveWorldObject { id = 4242, state = 1 });
            Assert.AreEqual(SaveLoadError.UnknownContentId, SaveValidator.Validate(save, Level(), out detail));
            StringAssert.Contains("4242", detail);

            save = SaveOf(world);
            save.levelId = 2;
            Assert.AreEqual(SaveLoadError.ContentMismatch, SaveValidator.Validate(save, Level(), out detail));
        }

        [Test]
        public void InvalidSave_IsNeverWritten()
        {
            var world = new WorldStateService(Level());
            var save = SaveOf(world);
            save.saveId = null;
            Assert.AreEqual(SaveLoadError.Corrupt, _store.Save(save).Error);
            Assert.IsFalse(Directory.Exists(_dir) && Directory.GetFiles(_dir).Length > 0);
        }

        [Test]
        public void RewardLedger_GrantsEachIdOnce()
        {
            var ledger = new RewardLedger();
            Assert.IsTrue(ledger.TryGrant(RewardLedger.LevelCompleted(1)));
            Assert.IsFalse(ledger.TryGrant(RewardLedger.LevelCompleted(1)));
            Assert.IsFalse(ledger.TryGrant(""));
            ledger.Load(new List<string> { "a", "a", "b" });
            Assert.AreEqual(2, ledger.Count);
        }
    }
}
