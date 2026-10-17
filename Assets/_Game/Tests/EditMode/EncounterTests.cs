using System.Collections.Generic;
using Avatar5.Contracts;
using Avatar5.Gameplay.Combat;
using Avatar5.Gameplay.Enemies;
using Avatar5.Gameplay.World;
using NUnit.Framework;

namespace Avatar5.Tests.EditMode
{
    public sealed class EncounterTests
    {
        private static EncounterDefinition Arena()
        {
            var e = new EncounterDefinition { Id = 3001, Center = new Float2(0f, 20f), ActivationRadius = 8f };
            e.Spawns.Add(new EncounterSpawn { EnemyTypeId = StarterEnemies.BruteTypeId, Position = new Float2(0f, 22f) });
            e.Spawns.Add(new EncounterSpawn { EnemyTypeId = StarterEnemies.CasterTypeId, Position = new Float2(4f, 24f), MinPlayers = 2 });
            e.Spawns.Add(new EncounterSpawn { EnemyTypeId = StarterEnemies.SkirmisherTypeId, Position = new Float2(-3f, 24f), MinPlayers = 3 });
            return e;
        }

        [Test]
        public void AllArchetypesAndAttacksAreValid()
        {
            foreach (var id in new[] { StarterEnemies.BruteTypeId, StarterEnemies.SkirmisherTypeId, StarterEnemies.CasterTypeId, StarterEnemies.WardenTypeId })
                Assert.IsEmpty(StarterEnemies.Get(id).Validate(), "type " + id);
            Assert.DoesNotThrow(() => new AbilityCatalog(StarterEnemies.AllAttacks()));
        }

        [Test]
        public void Encounter_StaysDormantUntilAPlayerEnters()
        {
            var director = new EncounterDirector(new[] { Arena() });
            Assert.IsEmpty(director.Tick(new[] { new Float2(0f, 0f) }, 1));
            Assert.AreEqual(EncounterState.Dormant, director.StateOf(3001));
            var orders = director.Tick(new[] { new Float2(0f, 13f) }, 1);
            Assert.AreEqual(1, orders.Count, "solo: only the brute");
            Assert.AreEqual(1f, orders[0].HealthMultiplier, 1e-4f);
            Assert.AreEqual(EncounterState.Active, director.StateOf(3001));
            Assert.IsEmpty(director.Tick(new[] { new Float2(0f, 20f) }, 1), "never activates twice");
        }

        [Test]
        public void Encounter_ScalesWithPlayersButNotTimesFour()
        {
            var director = new EncounterDirector(new[] { Arena() });
            var orders = director.Tick(new[] { new Float2(0f, 20f) }, 4);
            Assert.AreEqual(3, orders.Count, "extra spawns unlock at 2 and 3 players");
            Assert.AreEqual(1f + 0.35f * 3f, orders[0].HealthMultiplier, 1e-4f);
            Assert.Less(orders[0].HealthMultiplier, 4f);
        }

        [Test]
        public void Encounter_CompletesExactlyOnceWhenAllAreDefeated()
        {
            var director = new EncounterDirector(new[] { Arena() });
            var completed = 0;
            director.Completed += id => completed++;
            director.Tick(new[] { new Float2(0f, 20f) }, 2);
            director.RegisterSpawned(3001, 1001);
            director.RegisterSpawned(3001, 1002);
            director.OnActorDefeated(1001);
            director.OnActorDefeated(1001);
            Assert.AreEqual(0, completed);
            director.OnActorDefeated(4242);
            director.OnActorDefeated(1002);
            director.OnActorDefeated(1002);
            Assert.AreEqual(1, completed);
            Assert.AreEqual(EncounterState.Completed, director.StateOf(3001));
        }

        [Test]
        public void RestoredCompletedEncounter_NeverSpawnsAgain()
        {
            var director = new EncounterDirector(new[] { Arena() });
            director.MarkCompleted(3001);
            Assert.IsEmpty(director.Tick(new[] { new Float2(0f, 20f) }, 1));
        }

        [Test]
        public void Validation_RejectsEncountersEmptyForSoloPlayers()
        {
            var e = new EncounterDefinition { Id = 1, ActivationRadius = 5f };
            e.Spawns.Add(new EncounterSpawn { EnemyTypeId = 1, MinPlayers = 2 });
            Assert.Throws<System.ArgumentException>(() => new EncounterDirector(new[] { e }));
        }

        [Test]
        public void Warden_ChangesPhaseOnceAtHalfHealth()
        {
            var brain = new EnemyBrain(StarterEnemies.Warden(), new Float2(0f, 0f));
            Assert.AreEqual(StarterEnemies.WardenSlamId, brain.CurrentAttackAbilityId);
            Assert.IsFalse(brain.UpdatePhase(0.8f));
            Assert.IsTrue(brain.UpdatePhase(0.45f));
            Assert.AreEqual(1, brain.Phase);
            Assert.AreEqual(StarterEnemies.WardenShockwaveId, brain.CurrentAttackAbilityId);
            Assert.Greater(brain.CurrentMoveSpeed, StarterEnemies.Warden().MoveSpeed);
            Assert.IsFalse(brain.UpdatePhase(0.9f), "phases never go back");
            Assert.AreEqual(1, brain.Phase);
        }

        [Test]
        public void Caster_BacksOffWhenPlayerTooCloseAndShootsFromRange()
        {
            var brain = new EnemyBrain(StarterEnemies.Caster(), new Float2(0f, 0f));
            var near = new List<EnemyTargetCandidate> { new EnemyTargetCandidate { ActorId = 1, Position = new Float2(2f, 0f), Alive = true } };
            brain.Tick(0.05f, new Float2(0f, 0f), near);
            var output = brain.Tick(0.05f, new Float2(0f, 0f), near);
            Assert.IsTrue(output.HasDestination);
            Assert.Less(output.Destination.X, 0f, "moves away from the player");
            Assert.IsFalse(output.TelegraphStarted);

            var far = new List<EnemyTargetCandidate> { new EnemyTargetCandidate { ActorId = 1, Position = new Float2(7f, 0f), Alive = true } };
            Assert.IsTrue(brain.Tick(0.05f, new Float2(0f, 0f), far).TelegraphStarted, "attacks from 7 m");
        }

        [Test]
        public void World_EncounterCompletionOpensGate()
        {
            var level = new LevelDefinition { LevelId = 1, Key = "t" };
            level.Objects.Add(new WorldObjectDefinition { Id = 3001, Kind = WorldObjectKind.Encounter, Radius = 8f });
            level.Objects.Add(new WorldObjectDefinition { Id = 2101, Kind = WorldObjectKind.Gate, Requires = new[] { 3001 } });
            level.Objects.Add(new WorldObjectDefinition { Id = 9001, Kind = WorldObjectKind.Goal, Requires = new[] { 2101 } });
            var world = new WorldStateService(level);
            Assert.IsTrue(world.CompleteEncounter(3001));
            Assert.IsFalse(world.CompleteEncounter(3001));
            Assert.IsFalse(world.CompleteEncounter(2101), "only encounter objects");
            Assert.AreEqual(WorldStateCodes.On, world.GetState(2101));
        }
    }
}
