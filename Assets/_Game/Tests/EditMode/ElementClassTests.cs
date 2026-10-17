using System.Collections.Generic;
using Avatar5.Contracts;
using Avatar5.Gameplay.Combat;
using Avatar5.Gameplay.Enemies;
using Avatar5.Gameplay.World;
using NUnit.Framework;

namespace Avatar5.Tests.EditMode
{
    public sealed class ElementClassTests
    {
        private sealed class World : ICombatWorld
        {
            public readonly Dictionary<ulong, Float2> Positions = new Dictionary<ulong, Float2>();
            public bool TryGetPosition(ulong actorId, out Float2 position) { return Positions.TryGetValue(actorId, out position); }
            public float FreeFraction(Float2 from, Float2 to) { return 1f; }
            public void ApplyKnockback(ulong actorId, Float2 displacement) { }
        }

        private World _world;
        private CombatSimulation _sim;
        private List<CombatEvent> _events;
        private uint _seq;

        [SetUp]
        public void SetUp()
        {
            _world = new World();
            var all = new List<AbilityDefinition>(StarterAbilities.All()) { StarterEnemies.BruteSlam() };
            _sim = new CombatSimulation(new AbilityCatalog(all), CombatRules.CreateDefault(), _world);
            _events = new List<CombatEvent>();
            _sim.EventRaised += e => _events.Add(e);
            _sim.RegisterActor(1, TeamKind.Players, 100f, 500f, 100f, StarterAbilities.LoadoutFor(ElementKind.Water));
            _sim.RegisterActor(2, TeamKind.Players, 100f, 500f, 100f, StarterAbilities.LoadoutFor(ElementKind.Air));
            _sim.RegisterActor(50, TeamKind.Enemies, 1000f, 0f, 0f, new[] { StarterEnemies.BruteSlamAbilityId });
            _world.Positions[1] = new Float2(0f, 0f);
            _world.Positions[2] = new Float2(-1f, 0f);
            _world.Positions[50] = new Float2(3f, 0f);
        }

        private RequestStatus Cast(ulong actor, int ability)
        {
            return _sim.TryCast(new AbilityRequest { ActorId = actor, SequenceId = ++_seq, AbilityId = ability, TargetOnGround = new Float2(10f, 0f) }).Status;
        }

        private void Run(float seconds) { for (var t = 0f; t < seconds; t += 0.05f) _sim.Tick(0.05f); }

        private StatusFlags Status(ulong id) { ActorCombatState s; _sim.TryGetState(id, out s); return s.Status; }

        [Test]
        public void AllEightAbilitiesAreValidAndEachClassHasProjectileThenArea()
        {
            var catalog = new AbilityCatalog(StarterAbilities.All());
            Assert.AreEqual(8, catalog.Ids.Length);
            foreach (ElementKind element in System.Enum.GetValues(typeof(ElementKind)))
            {
                var loadout = StarterAbilities.LoadoutFor(element);
                AbilityDefinition first, second;
                Assert.IsTrue(catalog.TryGet(loadout[0], out first));
                Assert.IsTrue(catalog.TryGet(loadout[1], out second));
                Assert.AreEqual(element, first.Element);
                Assert.AreEqual(element, second.Element);
                Assert.AreEqual(AbilityTargeting.Projectile, first.Targeting, element + " slot 0 must reach puzzles at range");
                Assert.AreEqual(AbilityTargeting.Cone, second.Targeting);
            }
        }

        [Test]
        public void IceShard_WetsThenFreezes_FrozenBlocksCastingAndExpires()
        {
            Cast(1, StarterAbilities.IceShardId);
            Run(0.3f);
            Assert.AreEqual(StatusFlags.Wet, Status(50));
            Run(0.6f);
            Cast(1, StarterAbilities.IceShardId);
            Run(0.3f);
            Assert.AreEqual(StatusFlags.Frozen, Status(50), "second shard on a wet target freezes it");
            Assert.AreEqual((int)ReactionKind.Freeze, _events.Find(e => e.ResultKind == (int)CombatEventKind.ReactionTriggered).EffectId);

            var slam = _sim.TryCast(new AbilityRequest { ActorId = 50, SequenceId = 1, AbilityId = StarterEnemies.BruteSlamAbilityId, TargetOnGround = new Float2(0f, 0f) });
            Assert.AreEqual(RequestStatus.RejectedInvalidState, slam.Status, "frozen enemies cannot attack");
            Run(1.7f);
            Assert.AreEqual(StatusFlags.None, Status(50));
        }

        [Test]
        public void WindOnBurning_Flares()
        {
            _sim.SetLoadout(1, StarterAbilities.LoadoutFor(ElementKind.Fire));
            Cast(1, StarterAbilities.FireballId);
            Run(0.3f);
            Assert.IsTrue((Status(50) & StatusFlags.Burning) != 0);
            Cast(2, StarterAbilities.WindBladeId);
            Run(0.3f);
            var flare = _events.Find(e => e.ResultKind == (int)CombatEventKind.ReactionTriggered);
            Assert.AreEqual((int)ReactionKind.Flare, flare.EffectId);
            Assert.AreEqual(2UL, flare.SourceActorId);
            Assert.IsTrue((Status(50) & StatusFlags.Burning) != 0, "fire keeps burning after flaring up");
        }

        [Test]
        public void SetLoadout_SwapsAbilitiesAndRejectsUnknownIds()
        {
            Assert.AreEqual(RequestStatus.RejectedInvalidState, Cast(1, StarterAbilities.StoneSpikeId), "water class has no stone spike");
            Assert.IsTrue(_sim.SetLoadout(1, StarterAbilities.LoadoutFor(ElementKind.Earth)));
            Assert.AreEqual(RequestStatus.Accepted, Cast(1, StarterAbilities.StoneSpikeId));
            Assert.IsFalse(_sim.SetLoadout(1, new[] { 12345 }));
        }

        [Test]
        public void Stagger_CancelsRunningTelegraphWithoutAttack()
        {
            var brain = new EnemyBrain(StarterEnemies.Brute(), new Float2(0f, 0f));
            var players = new List<EnemyTargetCandidate> { new EnemyTargetCandidate { ActorId = 1, Position = new Float2(1f, 0f), Alive = true } };
            brain.Tick(0.05f, new Float2(0f, 0f), players);
            Assert.IsTrue(brain.Tick(0.05f, new Float2(0f, 0f), players).TelegraphStarted);
            Assert.IsTrue(brain.Stagger(0.6f));
            var released = false;
            for (var i = 0; i < 10; i++) released |= brain.Tick(0.05f, new Float2(0f, 0f), players).ReleaseAttack;
            Assert.IsFalse(released, "interrupted telegraph never lands");
        }

        [Test]
        public void ElementShrine_AcceptsEveryElement()
        {
            foreach (ElementKind element in System.Enum.GetValues(typeof(ElementKind)))
            {
                var level = new LevelDefinition { LevelId = 1, Key = "t" };
                level.Objects.Add(new WorldObjectDefinition { Id = 1, Kind = WorldObjectKind.Brazier, Position = new Float2(0f, 0f), Radius = 1f, AnyElement = true });
                level.Objects.Add(new WorldObjectDefinition { Id = 2, Kind = WorldObjectKind.Goal, Requires = new[] { 1 } });
                var world = new WorldStateService(level);
                world.ApplyImpact(new ElementImpact { Element = element, Position = new Float2(0.5f, 0f), Radius = 0.3f });
                Assert.AreEqual(WorldStateCodes.On, world.GetState(1), element.ToString());
            }
        }
    }
}
