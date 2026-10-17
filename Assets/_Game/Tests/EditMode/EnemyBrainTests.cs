using System.Collections.Generic;
using Avatar5.Contracts;
using Avatar5.Gameplay.Combat;
using Avatar5.Gameplay.Enemies;
using NUnit.Framework;

namespace Avatar5.Tests.EditMode
{
    public sealed class EnemyBrainTests
    {
        private static EnemyTargetCandidate Player(ulong id, float x, float z, bool alive = true, int chased = 0)
        {
            return new EnemyTargetCandidate { ActorId = id, Position = new Float2(x, z), Alive = alive, ChasedByOthers = chased };
        }

        private static List<EnemyTargetCandidate> Players(params EnemyTargetCandidate[] players)
        {
            return new List<EnemyTargetCandidate>(players);
        }

        [Test]
        public void Definition_RejectsUnreadableTelegraphAndMissingAttack()
        {
            var brute = StarterEnemies.Brute();
            Assert.IsEmpty(brute.Validate());
            brute.TelegraphDuration = 0.1f;
            brute.AttackAbilityId = 0;
            Assert.AreEqual(2, brute.Validate().Count);
            Assert.Throws<System.ArgumentException>(() => new EnemyBrain(brute, new Float2(0f, 0f)));
        }

        [Test]
        public void StaysIdleUntilAPlayerEntersAggroRange()
        {
            var brain = new EnemyBrain(StarterEnemies.Brute(), new Float2(0f, 0f));
            Assert.AreEqual(EnemyState.Idle, brain.Tick(0.05f, new Float2(0f, 0f), Players(Player(1, 15f, 0f))).State);
            var output = brain.Tick(0.05f, new Float2(0f, 0f), Players(Player(1, 8f, 0f)));
            Assert.AreEqual(EnemyState.Approach, output.State);
            Assert.AreEqual(1UL, output.TargetActorId);
        }

        [Test]
        public void ApproachTelegraphAttackRecover_WithLockedAim()
        {
            var definition = StarterEnemies.Brute();
            var brain = new EnemyBrain(definition, new Float2(0f, 0f));
            var self = new Float2(0f, 0f);
            brain.Tick(0.05f, self, Players(Player(1, 6f, 0f)));
            var approach = brain.Tick(0.05f, self, Players(Player(1, 6f, 0f)));
            Assert.IsTrue(approach.HasDestination);
            Assert.AreEqual(6f, approach.Destination.X, 1e-4f);

            var telegraph = brain.Tick(0.05f, new Float2(4.5f, 0f), Players(Player(1, 6f, 0f)));
            Assert.AreEqual(EnemyState.Telegraph, telegraph.State);
            Assert.IsTrue(telegraph.TelegraphStarted);
            Assert.IsFalse(telegraph.HasDestination, "stands still while telegraphing");

            var released = 0;
            var elapsed = 0f;
            EnemyBrainOutput attack = default(EnemyBrainOutput);
            while (elapsed < 3f)
            {
                elapsed += 0.05f;
                // The player walks away during the telegraph; the aim must stay where it was announced.
                var o = brain.Tick(0.05f, new Float2(4.5f, 0f), Players(Player(1, 6f + elapsed, 0f)));
                if (o.ReleaseAttack) { released++; attack = o; if (released == 1) Assert.That(elapsed, Is.InRange(definition.TelegraphDuration - 0.06f, definition.TelegraphDuration + 0.06f)); }
            }
            Assert.AreEqual(1, released, "one telegraph releases exactly one attack");
            Assert.AreEqual(EnemyState.Attack, attack.State);
            Assert.AreEqual(4.5f, attack.AttackOrigin.X, 1e-4f);
            Assert.AreEqual(6f, attack.AttackAimPoint.X, 1e-4f);
        }

        [Test]
        public void LeashesBackHomeAndForgetsTarget()
        {
            var brain = new EnemyBrain(StarterEnemies.Brute(), new Float2(0f, 0f));
            brain.Tick(0.05f, new Float2(0f, 0f), Players(Player(1, 5f, 0f)));
            var output = brain.Tick(0.05f, new Float2(19f, 0f), Players(Player(1, 25f, 0f)));
            Assert.AreEqual(EnemyState.Return, output.State);
            Assert.AreEqual(0UL, output.TargetActorId);
            output = brain.Tick(0.05f, new Float2(10f, 0f), Players(Player(1, 30f, 0f)));
            Assert.IsTrue(output.HasDestination);
            Assert.AreEqual(0f, output.Destination.X, 1e-4f);
            Assert.AreEqual(EnemyState.Idle, brain.Tick(0.05f, new Float2(0.2f, 0f), Players(Player(1, 30f, 0f))).State);
        }

        [Test]
        public void IgnoresDefeatedPlayersAndKeepsTargetWithHysteresis()
        {
            var brain = new EnemyBrain(StarterEnemies.Brute(), new Float2(0f, 0f));
            brain.Tick(0.05f, new Float2(0f, 0f), Players(Player(1, 6f, 0f), Player(2, 3f, 0f, false)));
            Assert.AreEqual(1UL, brain.Target, "defeated player 2 is not a target");
            brain.Tick(0.05f, new Float2(0f, 0f), Players(Player(1, 6f, 0f), Player(2, 4f, 0f)));
            Assert.AreEqual(1UL, brain.Target, "2 m closer is below the 3 m hysteresis");
            brain.Tick(0.05f, new Float2(0f, 0f), Players(Player(1, 6f, 0f), Player(2, 2.5f, 0f)));
            Assert.AreEqual(2UL, brain.Target, "clearly closer player takes the aggro");
        }

        [Test]
        public void AntiFocus_PrefersPlayersNobodyChases()
        {
            var brain = new EnemyBrain(StarterEnemies.Brute(), new Float2(0f, 0f));
            brain.Tick(0.05f, new Float2(0f, 0f), Players(Player(1, 4f, 0f, true, 2), Player(2, 6f, 0f)));
            Assert.AreEqual(2UL, brain.Target);
        }

        [Test]
        public void Dead_DoesNothingUntilRevived()
        {
            var brain = new EnemyBrain(StarterEnemies.Brute(), new Float2(0f, 0f));
            brain.Tick(0.05f, new Float2(0f, 0f), Players(Player(1, 1f, 0f)));
            brain.OnDefeated();
            var output = brain.Tick(0.05f, new Float2(0f, 0f), Players(Player(1, 1f, 0f)));
            Assert.AreEqual(EnemyState.Dead, output.State);
            Assert.IsFalse(output.ReleaseAttack || output.HasDestination || output.TelegraphStarted);
            brain.OnRevived();
            Assert.AreEqual(EnemyState.Approach, brain.Tick(0.05f, new Float2(0f, 0f), Players(Player(1, 3f, 0f))).State);
        }

        [Test]
        public void SlamThroughCombatCore_HitsPlayersAroundButNotOtherEnemies()
        {
            var positions = new Dictionary<ulong, Float2>
            {
                { 1, new Float2(1f, 0f) }, { 2, new Float2(-1.5f, 0.5f) }, { 3, new Float2(5f, 0f) }, { 50, new Float2(0f, 0f) }, { 51, new Float2(0f, 1f) }
            };
            var world = new PositionsWorld(positions);
            var sim = new CombatSimulation(new AbilityCatalog(new[] { StarterEnemies.BruteSlam() }), CombatRules.CreateDefault(), world);
            foreach (var id in new ulong[] { 1, 2, 3 }) sim.RegisterActor(id, TeamKind.Players, 100f, 0f, 0f, new int[0]);
            sim.RegisterActor(50, TeamKind.Enemies, 160f, 0f, 0f, new[] { StarterEnemies.BruteSlamAbilityId });
            sim.RegisterActor(51, TeamKind.Enemies, 160f, 0f, 0f, new int[0]);
            var events = new List<CombatEvent>();
            sim.EventRaised += e => events.Add(e);

            sim.RaiseTelegraph(50, StarterEnemies.BruteSlamAbilityId, StarterEnemies.BruteTelegraphCue, new Float2(0f, 0f), new Float2(1f, 0f), 0.9f);
            var result = sim.TryCast(new AbilityRequest { ActorId = 50, SequenceId = 1, AbilityId = StarterEnemies.BruteSlamAbilityId, TargetOnGround = new Float2(1f, 0f) });

            Assert.AreEqual(RequestStatus.Accepted, result.Status);
            Assert.AreEqual((int)CombatEventKind.TelegraphStarted, events[0].ResultKind, "telegraph is announced before any damage");
            ActorCombatState state;
            sim.TryGetState(1, out state); Assert.AreEqual(82f, state.Health, 1e-3f);
            sim.TryGetState(2, out state); Assert.AreEqual(82f, state.Health, 1e-3f, "behind the brute is hit too (full circle)");
            sim.TryGetState(3, out state); Assert.AreEqual(100f, state.Health, "outside the radius");
            sim.TryGetState(51, out state); Assert.AreEqual(160f, state.Health, "no friendly fire between enemies");
        }

        private sealed class PositionsWorld : ICombatWorld
        {
            private readonly Dictionary<ulong, Float2> _positions;
            public PositionsWorld(Dictionary<ulong, Float2> positions) { _positions = positions; }
            public bool TryGetPosition(ulong actorId, out Float2 position) { return _positions.TryGetValue(actorId, out position); }
            public float FreeFraction(Float2 from, Float2 to) { return 1f; }
            public void ApplyKnockback(ulong actorId, Float2 displacement) { }
        }
    }
}
