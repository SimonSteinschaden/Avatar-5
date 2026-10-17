using System.Collections.Generic;
using Avatar5.Contracts;
using Avatar5.Gameplay.World;
using NUnit.Framework;

namespace Avatar5.Tests.EditMode
{
    public sealed class WorldStateServiceTests
    {
        private const int Lever = 10, GateA = 11, Brazier = 20, GateB = 21, Goal = 99;

        private static LevelDefinition Level()
        {
            var level = new LevelDefinition { LevelId = 1, Key = "level01" };
            level.Objects.Add(new WorldObjectDefinition { Id = Lever, Kind = WorldObjectKind.Lever, Position = new Float2(0f, 5f), Radius = 2f });
            level.Objects.Add(new WorldObjectDefinition { Id = GateA, Kind = WorldObjectKind.Gate, Position = new Float2(0f, 8f), Requires = new[] { Lever } });
            level.Objects.Add(new WorldObjectDefinition { Id = Brazier, Kind = WorldObjectKind.Brazier, Position = new Float2(5f, 20f), Radius = 1.2f, Element = ElementKind.Fire });
            level.Objects.Add(new WorldObjectDefinition { Id = GateB, Kind = WorldObjectKind.Gate, Position = new Float2(0f, 25f), Requires = new[] { Brazier, GateA } });
            level.Objects.Add(new WorldObjectDefinition { Id = Goal, Kind = WorldObjectKind.Goal, Position = new Float2(0f, 30f), Radius = 2f, Requires = new[] { GateB } });
            return level;
        }

        private WorldStateService _world;
        private List<WorldEvent> _events;

        [SetUp]
        public void SetUp()
        {
            _world = new WorldStateService(Level());
            _events = new List<WorldEvent>();
            _world.Changed += e => _events.Add(e);
        }

        [Test]
        public void Validation_CatchesDuplicateIdsBrokenLinksAndMissingGoal()
        {
            var level = Level();
            level.Objects.Add(new WorldObjectDefinition { Id = Lever, Kind = WorldObjectKind.Lever });
            level.Objects.Add(new WorldObjectDefinition { Id = 50, Kind = WorldObjectKind.Gate, Requires = new[] { 404 } });
            level.Objects.Add(new WorldObjectDefinition { Id = 51, Kind = WorldObjectKind.Gate });
            level.Objects.RemoveAll(o => o.Kind == WorldObjectKind.Goal);
            var errors = level.Validate();
            Assert.That(errors, Has.Some.Contains("duplicate"));
            Assert.That(errors, Has.Some.Contains("unknown id 404"));
            Assert.That(errors, Has.Some.Contains("no requirements"));
            Assert.That(errors, Has.Some.Contains("exactly one goal"));
            Assert.Throws<System.ArgumentException>(() => new WorldStateService(level));
        }

        [Test]
        public void Lever_NeedsToBeInRangeAndOpensItsGateOnce()
        {
            Assert.AreEqual(RequestStatus.RejectedRange, _world.TryInteract(1, Lever, new Float2(0f, 0f)));
            Assert.AreEqual(RequestStatus.Accepted, _world.TryInteract(1, Lever, new Float2(0f, 4f)));
            Assert.AreEqual(RequestStatus.Duplicate, _world.TryInteract(2, Lever, new Float2(0f, 4f)), "second player pulling again changes nothing");
            Assert.AreEqual(WorldStateCodes.On, _world.GetState(GateA));
            Assert.AreEqual(2, _events.Count, "lever on + gate open");
            Assert.AreEqual(GateA, _events[1].WorldObjectId);
        }

        [Test]
        public void OnlyLeversCanBeOperatedDirectly()
        {
            Assert.AreEqual(RequestStatus.RejectedInvalidState, _world.TryInteract(1, GateA, new Float2(0f, 8f)));
            Assert.AreEqual(RequestStatus.RejectedInvalidState, _world.TryInteract(1, 12345, new Float2(0f, 8f)));
            Assert.IsEmpty(_events);
        }

        [Test]
        public void FireImpactLightsBrazierOtherElementsDoNot()
        {
            _world.ApplyImpact(new ElementImpact { Element = ElementKind.Water, Position = new Float2(5f, 20f), Radius = 1f });
            Assert.AreEqual(WorldStateCodes.Off, _world.GetState(Brazier));
            _world.ApplyImpact(new ElementImpact { Element = ElementKind.Fire, Position = new Float2(7.5f, 20f), Radius = 0.6f });
            Assert.AreEqual(WorldStateCodes.Off, _world.GetState(Brazier), "too far away");
            _world.ApplyImpact(new ElementImpact { Element = ElementKind.Fire, Position = new Float2(5.8f, 20f), Radius = 0.6f });
            Assert.AreEqual(WorldStateCodes.On, _world.GetState(Brazier));
            Assert.AreEqual(WorldStateCodes.Off, _world.GetState(GateB), "gate B also needs gate A");
        }

        [Test]
        public void ChainedGatesOpenInOrderAndGoalCompletesExactlyOnce()
        {
            _world.CheckGoal(1, new Float2(0f, 30f));
            Assert.IsFalse(_world.IsCompleted, "goal is locked behind gate B");

            _world.ApplyImpact(new ElementImpact { Element = ElementKind.Fire, Position = new Float2(5f, 20f), Radius = 0.5f });
            _world.TryInteract(3, Lever, new Float2(0f, 5f));
            Assert.AreEqual(WorldStateCodes.On, _world.GetState(GateB));

            _world.CheckGoal(2, new Float2(0f, 29f));
            _world.CheckGoal(1, new Float2(0f, 30f));
            Assert.IsTrue(_world.IsCompleted);
            Assert.AreEqual(2UL, _world.CompletedBy);
            Assert.AreEqual(1, _events.FindAll(e => e.StateCode == WorldStateCodes.LevelCompletedCode).Count);

            var ids = new HashSet<ulong>();
            ulong previous = 0;
            foreach (var e in _events)
            {
                Assert.IsTrue(ids.Add(e.EventId));
                Assert.Greater(e.EventId, previous);
                previous = e.EventId;
            }
        }

        [Test]
        public void SnapshotRestore_ReproducesStateWithoutEvents()
        {
            _world.TryInteract(1, Lever, new Float2(0f, 5f));
            var snapshot = _world.Snapshot();

            var restored = new WorldStateService(Level());
            var restoredEvents = 0;
            restored.Changed += e => restoredEvents++;
            restored.Restore(snapshot, false);
            Assert.AreEqual(WorldStateCodes.On, restored.GetState(Lever));
            Assert.AreEqual(WorldStateCodes.On, restored.GetState(GateA));
            Assert.AreEqual(0, restoredEvents);
            Assert.AreEqual(RequestStatus.Duplicate, restored.TryInteract(1, Lever, new Float2(0f, 5f)));
        }

        [Test]
        public void FindInteractable_PicksNearestUnusedLever()
        {
            Assert.AreEqual(0, _world.FindInteractable(new Float2(0f, 0f)));
            Assert.AreEqual(Lever, _world.FindInteractable(new Float2(0.5f, 4f)));
            _world.TryInteract(1, Lever, new Float2(0.5f, 4f));
            Assert.AreEqual(0, _world.FindInteractable(new Float2(0.5f, 4f)));
        }
    }
}
