using System.Collections.Generic;
using Avatar5.Contracts;
using Avatar5.Gameplay.Combat;
using NUnit.Framework;

namespace Avatar5.Tests.EditMode
{
    public sealed class CombatSimulationTests
    {
        private sealed class FakeWorld : ICombatWorld
        {
            public readonly Dictionary<ulong, Float2> Positions = new Dictionary<ulong, Float2>();
            public readonly Dictionary<ulong, Float2> Pushed = new Dictionary<ulong, Float2>();
            /// <summary>Wall along x = WallX (if set) blocking segments that cross it.</summary>
            public float? WallX;

            public bool TryGetPosition(ulong actorId, out Float2 position) { return Positions.TryGetValue(actorId, out position); }

            public float FreeFraction(Float2 from, Float2 to)
            {
                if (!WallX.HasValue) return 1f;
                var w = WallX.Value;
                if ((from.X - w) * (to.X - w) > 0f || Mathf(from.X - to.X) < 1e-6f) return 1f;
                return (w - from.X) / (to.X - from.X);
            }

            public void ApplyKnockback(ulong actorId, Float2 displacement)
            {
                Pushed[actorId] = displacement;
                var p = Positions[actorId];
                Positions[actorId] = new Float2(p.X + displacement.X, p.Y + displacement.Y);
            }

            private static float Mathf(float v) { return v < 0f ? -v : v; }
        }

        private const ulong Caster = 1;
        private const ulong Ally = 2;
        private const ulong Dummy = 100;
        private static readonly int[] Loadout = { StarterAbilities.FireballId, StarterAbilities.WaterWaveId };

        private FakeWorld _world;
        private CombatSimulation _sim;
        private List<CombatEvent> _events;
        private uint _sequence;

        [SetUp]
        public void SetUp()
        {
            _world = new FakeWorld();
            var catalog = new AbilityCatalog(new[] { StarterAbilities.Fireball(), StarterAbilities.WaterWave() });
            _sim = new CombatSimulation(catalog, CombatRules.CreateDefault(), _world);
            _events = new List<CombatEvent>();
            _sim.EventRaised += e => _events.Add(e);
            _sequence = 0;

            _sim.RegisterActor(Caster, TeamKind.Players, 100f, 100f, 0f, Loadout);
            _sim.RegisterActor(Ally, TeamKind.Players, 100f, 100f, 0f, Loadout);
            _sim.RegisterActor(Dummy, TeamKind.Enemies, 200f, 0f, 0f, new int[0]);
            _world.Positions[Caster] = new Float2(0f, 0f);
            _world.Positions[Ally] = new Float2(-3f, 0f);
            _world.Positions[Dummy] = new Float2(4f, 0f);
        }

        private AbilityCastResult Cast(ulong actor, int ability, float targetX = 10f, float targetZ = 0f, uint? sequence = null)
        {
            var seq = sequence ?? ++_sequence;
            if (seq > _sequence) _sequence = seq;
            return _sim.TryCast(new AbilityRequest { ActorId = actor, SequenceId = seq, AbilityId = ability, TargetOnGround = new Float2(targetX, targetZ) });
        }

        private void Run(float seconds)
        {
            for (var t = 0f; t < seconds; t += 0.05f) _sim.Tick(0.05f);
        }

        private float Health(ulong id)
        {
            ActorCombatState state;
            Assert.IsTrue(_sim.TryGetState(id, out state));
            return state.Health;
        }

        private StatusFlags Status(ulong id)
        {
            ActorCombatState state;
            _sim.TryGetState(id, out state);
            return state.Status;
        }

        private int Count(CombatEventKind kind)
        {
            return _events.FindAll(e => e.ResultKind == (int)kind).Count;
        }

        [Test]
        public void Catalog_RejectsInvalidAndDuplicateDefinitions()
        {
            var broken = StarterAbilities.Fireball();
            broken.ProjectileSpeed = 0f;
            Assert.Throws<System.ArgumentException>(() => new AbilityCatalog(new[] { broken }));
            Assert.Throws<System.ArgumentException>(() => new AbilityCatalog(new[] { StarterAbilities.Fireball(), StarterAbilities.Fireball() }));
            Assert.IsEmpty(StarterAbilities.WaterWave().Validate());
        }

        [Test]
        public void Fireball_TravelsHitsOnceAndIgnites()
        {
            Assert.AreEqual(RequestStatus.Accepted, Cast(Caster, StarterAbilities.FireballId).Status);
            Assert.AreEqual(100f - 20f, StateOf(Caster).Energy, 1e-3f);
            Assert.AreEqual(200f, Health(Dummy), "projectile needs travel time");
            Run(0.4f);
            Assert.AreEqual(200f - 20f, Health(Dummy), 1e-3f);
            Assert.IsTrue((Status(Dummy) & StatusFlags.Burning) != 0);
            Assert.AreEqual(0, _sim.ActiveProjectiles);
            Assert.AreEqual(1, Count(CombatEventKind.ProjectileEnded));
        }

        [Test]
        public void Burning_DealsDamageOverTimeThenExpires()
        {
            Cast(Caster, StarterAbilities.FireballId);
            Run(0.4f);
            var afterHit = Health(Dummy);
            Run(5f);
            Assert.AreEqual(afterHit - 4f * 5f, Health(Dummy), 0.01f, "5 dps for 4 s in 0.5 s ticks");
            Assert.AreEqual(StatusFlags.None, Status(Dummy));
        }

        [Test]
        public void WaterWave_HitsConeOnlyWetsAndPushes()
        {
            _world.Positions[Dummy] = new Float2(3f, 0f);
            _sim.RegisterActor(101, TeamKind.Enemies, 50f, 0f, 0f, new int[0]);
            _world.Positions[101] = new Float2(0f, 3f); // 90° off the aim direction
            Assert.AreEqual(RequestStatus.Accepted, Cast(Caster, StarterAbilities.WaterWaveId).Status);
            Assert.AreEqual(190f, Health(Dummy), 1e-3f);
            Assert.AreEqual(50f, Health(101), 1e-3f);
            Assert.IsTrue((Status(Dummy) & StatusFlags.Wet) != 0);
            Assert.AreEqual(2.5f, _world.Pushed[Dummy].X, 1e-3f);
            Assert.AreEqual(100f, Health(Ally), "no friendly fire");
        }

        [Test]
        public void Reaction_SteamOnWetTargetAddsBonusAndDoesNotIgnite()
        {
            _world.Positions[Dummy] = new Float2(3f, 0f);
            Cast(Caster, StarterAbilities.WaterWaveId);
            Cast(Ally, StarterAbilities.FireballId, 10f, 0f);
            Run(0.8f);
            var steam = _events.Find(e => e.ResultKind == (int)CombatEventKind.ReactionTriggered);
            Assert.AreEqual((int)ReactionKind.Steam, steam.EffectId);
            Assert.AreEqual(Ally, steam.SourceActorId);
            Assert.AreEqual(200f - 10f - (20f + 15f), Health(Dummy), 1e-3f);
            Assert.AreEqual(StatusFlags.None, Status(Dummy), "Wet consumed, Burning suppressed");
        }

        [Test]
        public void Reaction_ExtinguishOnBurningTargetLeavesItWet()
        {
            Cast(Caster, StarterAbilities.FireballId);
            Run(0.3f);
            Assert.IsTrue((Status(Dummy) & StatusFlags.Burning) != 0);
            _world.Positions[Ally] = new Float2(1.5f, 0f);
            Assert.AreEqual(RequestStatus.Accepted, Cast(Ally, StarterAbilities.WaterWaveId, 10f, 0f).Status);
            var reaction = _events.Find(e => e.ResultKind == (int)CombatEventKind.ReactionTriggered);
            Assert.AreEqual((int)ReactionKind.Extinguish, reaction.EffectId);
            Assert.AreEqual(StatusFlags.Wet, Status(Dummy));
        }

        [Test]
        public void Cooldown_EnergyAndDuplicatesAreRejected()
        {
            Assert.AreEqual(RequestStatus.Accepted, Cast(Caster, StarterAbilities.FireballId, sequence: 5).Status);
            Assert.AreEqual(RequestStatus.Duplicate, Cast(Caster, StarterAbilities.FireballId, sequence: 5).Status);
            Assert.AreEqual(RequestStatus.Duplicate, Cast(Caster, StarterAbilities.FireballId, sequence: 4).Status);
            Assert.AreEqual(RequestStatus.RejectedCooldown, Cast(Caster, StarterAbilities.FireballId, sequence: 6).Status);
            Run(0.85f);
            Assert.AreEqual(RequestStatus.Accepted, Cast(Caster, StarterAbilities.FireballId).Status);
            Run(0.85f);
            Assert.AreEqual(RequestStatus.Accepted, Cast(Caster, StarterAbilities.WaterWaveId).Status); // 100-20-20-30 = 30 left
            Run(2.05f);
            Assert.AreEqual(RequestStatus.Accepted, Cast(Caster, StarterAbilities.WaterWaveId).Status); // 0 left
            Run(2.05f);
            Assert.AreEqual(RequestStatus.RejectedResource, Cast(Caster, StarterAbilities.WaterWaveId).Status);
        }

        [Test]
        public void Requests_FromUnknownDefeatedOrWithoutDirection_AreRejected()
        {
            Assert.AreEqual(RequestStatus.RejectedInvalidState, Cast(999, StarterAbilities.FireballId).Status);
            Assert.AreEqual(RequestStatus.RejectedInvalidState, Cast(Caster, 4242).Status, "ability not in loadout");
            Assert.AreEqual(RequestStatus.RejectedInvalidState, Cast(Dummy, StarterAbilities.FireballId).Status, "dummy has no loadout");
            Assert.AreEqual(RequestStatus.RejectedRange, Cast(Caster, StarterAbilities.FireballId, 0f, 0f).Status);
        }

        [Test]
        public void Projectile_StopsAtWall()
        {
            _world.WallX = 2f;
            Cast(Caster, StarterAbilities.FireballId);
            Run(1f);
            Assert.AreEqual(200f, Health(Dummy));
            Assert.AreEqual(1, Count(CombatEventKind.ProjectileEnded));
        }

        [Test]
        public void Defeat_IsRaisedExactlyOnceAndBlocksFurtherDamage()
        {
            _sim.RegisterActor(Dummy, TeamKind.Enemies, 25f, 0f, 0f, new int[0]);
            Cast(Caster, StarterAbilities.FireballId);
            Run(0.4f);
            Run(3f);
            Assert.AreEqual(0f, Health(Dummy));
            Assert.AreEqual(1, Count(CombatEventKind.ActorDefeated));
            Run(0.85f);
            Cast(Caster, StarterAbilities.FireballId);
            Run(1f);
            Assert.AreEqual(1, Count(CombatEventKind.ActorDefeated));
            _sim.Revive(Dummy);
            Assert.AreEqual(25f, Health(Dummy));
        }

        [Test]
        public void ManyCasts_ProduceUniqueOrderedEventsAndConsistentHealth()
        {
            _sim.RegisterActor(Caster, TeamKind.Players, 100f, 1000f, 1000f, Loadout);
            _sim.RegisterActor(Dummy, TeamKind.Enemies, 100000f, 0f, 0f, new int[0]);
            _world.Positions[Dummy] = new Float2(3f, 0f);
            for (var i = 0; i < 120; i++)
            {
                Cast(Caster, i % 2 == 0 ? StarterAbilities.FireballId : StarterAbilities.WaterWaveId);
                Cast(Caster, StarterAbilities.FireballId, sequence: _sequence); // retransmission of the same request
                Run(2.1f);
            }

            Assert.Greater(_events.Count, 100);
            var seen = new HashSet<ulong>();
            ulong previous = 0;
            float damage = 0f;
            foreach (var e in _events)
            {
                Assert.IsTrue(seen.Add(e.EventId), "duplicate EventId " + e.EventId);
                Assert.Greater(e.EventId, previous);
                previous = e.EventId;
                if (e.ResultKind == (int)CombatEventKind.DamageApplied && e.TargetActorId == Dummy) damage += e.Amount;
            }
            Assert.AreEqual(120, Count(CombatEventKind.AbilityCastAccepted), "retransmissions never cast twice");
            Assert.AreEqual(100000f - damage, Health(Dummy), 0.05f, "health equals the sum of damage events");
        }

        [Test]
        public void SameInputs_GiveIdenticalEventLogs()
        {
            var first = Simulate();
            var second = Simulate();
            Assert.AreEqual(first.Count, second.Count);
            for (var i = 0; i < first.Count; i++) Assert.AreEqual(first[i], second[i]);
        }

        private List<CombatEvent> Simulate()
        {
            SetUp();
            _world.Positions[Dummy] = new Float2(3f, 0f);
            for (var i = 0; i < 30; i++)
            {
                Cast(i % 3 == 0 ? Ally : Caster, i % 2 == 0 ? StarterAbilities.WaterWaveId : StarterAbilities.FireballId, 10f, 0f);
                Run(1.1f);
            }
            return new List<CombatEvent>(_events);
        }

        private ActorCombatState StateOf(ulong id)
        {
            ActorCombatState state;
            _sim.TryGetState(id, out state);
            return state;
        }
    }
}
