using System.Collections.Generic;
using Avatar5.Contracts;
using Avatar5.Gameplay.Combat;
using Avatar5.Gameplay.Progression;
using NUnit.Framework;

namespace Avatar5.Tests.EditMode
{
    public sealed class ProgressionTests
    {
        [Test]
        public void XpCurve_LevelsUpAtIncreasingThresholds()
        {
            var rules = new ProgressionRules();
            Assert.AreEqual(0, rules.XpForLevel(1));
            Assert.AreEqual(100, rules.XpForLevel(2));
            Assert.AreEqual(300, rules.XpForLevel(3));
            Assert.AreEqual(1, rules.LevelForXp(99));
            Assert.AreEqual(2, rules.LevelForXp(100));
            Assert.AreEqual(rules.MaxLevel, rules.LevelForXp(1000000));
        }

        [Test]
        public void Reward_IsCreditedOncePerSlotEvenWhenReplayed()
        {
            var p = new ProgressionService(new ProgressionRules());
            Assert.AreEqual(2, p.Grant("encounter:3001", 60, new[] { 0, 1 }));
            Assert.AreEqual(0, p.Grant("encounter:3001", 60, new[] { 0, 1 }), "rejoin/replay must not add XP again");
            Assert.AreEqual(1, p.Grant("encounter:3001", 60, new[] { 2 }), "a player who was not credited yet gets it");
            Assert.AreEqual(60, p.Get(0).Xp);
        }

        [Test]
        public void Talents_NeedFreePointsAndRespectMaxRank()
        {
            var p = new ProgressionService(new ProgressionRules());
            Assert.AreEqual(TalentResult.NoPoints, p.ChooseTalent(0, TalentId.Power));
            p.Grant("a", 300, new[] { 0 }); // level 3 → 2 points
            Assert.AreEqual(2, p.FreePoints(0));
            Assert.AreEqual(TalentResult.Accepted, p.ChooseTalent(0, TalentId.Power));
            Assert.AreEqual(TalentResult.Accepted, p.ChooseTalent(0, TalentId.Power));
            Assert.AreEqual(TalentResult.NoPoints, p.ChooseTalent(0, TalentId.Focus));
            p.Grant("b", 10000, new[] { 0 });
            Assert.AreEqual(TalentResult.Accepted, p.ChooseTalent(0, TalentId.Power));
            Assert.AreEqual(TalentResult.MaxRank, p.ChooseTalent(0, TalentId.Power));
            Assert.AreEqual(TalentResult.UnknownTalent, p.ChooseTalent(0, (TalentId)99));
        }

        [Test]
        public void Stats_FollowTalentsAndAreNeutralForEnemies()
        {
            var p = new ProgressionService(new ProgressionRules());
            p.Grant("a", 10000, new[] { 0 });
            p.ChooseTalent(0, TalentId.Power);
            p.ChooseTalent(0, TalentId.Focus);
            p.ChooseTalent(0, TalentId.Vigor);
            var s = p.GetStats(1);
            Assert.AreEqual(1.12f, s.DamageMultiplier, 1e-4f);
            Assert.AreEqual(0.9f, s.CooldownMultiplier, 1e-4f);
            Assert.AreEqual(15f, s.MaxHealthBonus, 1e-4f);
            Assert.AreEqual(1f, p.GetStats(1001).DamageMultiplier);
            Assert.AreEqual(1f, p.GetStats(2).DamageMultiplier, "slot without progress");
        }

        [Test]
        public void CombatCore_AppliesDamageAndCooldownModifiers()
        {
            var world = new FixedWorld();
            var sim = new CombatSimulation(new AbilityCatalog(StarterAbilities.All()), CombatRules.CreateDefault(), world);
            var progression = new ProgressionService(new ProgressionRules());
            progression.Grant("a", 10000, new[] { 0 });
            progression.ChooseTalent(0, TalentId.Power);
            progression.ChooseTalent(0, TalentId.Focus);
            sim.Stats = progression;
            sim.RegisterActor(1, TeamKind.Players, 100f, 100f, 0f, StarterAbilities.LoadoutFor(ElementKind.Earth));
            sim.RegisterActor(50, TeamKind.Enemies, 500f, 0f, 0f, new int[0]);
            sim.TryCast(new AbilityRequest { ActorId = 1, SequenceId = 1, AbilityId = StarterAbilities.StoneSpikeId, TargetOnGround = new Float2(10f, 0f) });
            for (var i = 0; i < 10; i++) sim.Tick(0.05f);
            ActorCombatState s;
            sim.TryGetState(50, out s);
            Assert.AreEqual(500f - 26f * 1.12f, s.Health, 0.01f);
            Assert.AreEqual(StarterAbilities.StoneSpike().Cooldown * 0.9f - 0.5f, sim.CooldownRemaining(1, StarterAbilities.StoneSpikeId), 0.06f);
        }

        [Test]
        public void SaveRoundTrip_KeepsXpTalentsAndGrants()
        {
            var p = new ProgressionService(new ProgressionRules());
            p.Grant("encounter:3001", 400, new[] { 0, 1 });
            p.ChooseTalent(1, TalentId.Vigor);
            var records = new List<SavePlayerRecord>();
            p.WriteInto(records);

            var restored = new ProgressionService(new ProgressionRules());
            restored.Restore(records, p.GrantedKeys());
            Assert.AreEqual(400, restored.Get(0).Xp);
            Assert.AreEqual(1, restored.Get(1).Rank(TalentId.Vigor));
            Assert.AreEqual(0, restored.Grant("encounter:3001", 400, new[] { 0, 1 }));
        }

        private sealed class FixedWorld : ICombatWorld
        {
            public bool TryGetPosition(ulong actorId, out Float2 position)
            {
                position = actorId == 1 ? new Float2(0f, 0f) : new Float2(3f, 0f);
                return true;
            }
            public float FreeFraction(Float2 from, Float2 to) { return 1f; }
            public void ApplyKnockback(ulong actorId, Float2 displacement) { }
        }
    }
}
