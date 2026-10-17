using System;
using System.Collections.Generic;
using Avatar5.Contracts;

namespace Avatar5.Gameplay.Combat
{
    /// <summary>
    /// Validated runtime ability data. Authored as ScriptableObject on the Unity side; all balancing values
    /// live here, never as code constants in the resolver.
    /// </summary>
    public sealed class AbilityDefinition
    {
        public int Id;
        public string Key;
        public ElementKind Element;
        public AbilityTargeting Targeting;
        public float EnergyCost;
        public float Cooldown;
        /// <summary>Projectile: max travel distance. Cone: radius.</summary>
        public float Range;
        public float ConeHalfAngleDegrees;
        public float ProjectileSpeed;
        public float ProjectileRadius;
        public float Damage;
        /// <summary>Push distance in metres applied to each hit target.</summary>
        public float Knockback;
        public StatusFlags AppliesStatus;
        public float StatusDuration;
        /// <summary>Damage per second while the applied status lasts (e.g. Burning); 0 for none.</summary>
        public float StatusDamagePerSecond;
        /// <summary>Presentation cue for the cast (projectile/wave visuals).</summary>
        public int CueId;

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (Id <= 0) errors.Add("Id must be > 0");
            if (string.IsNullOrEmpty(Key)) errors.Add("Key missing");
            if (EnergyCost < 0f) errors.Add(Key + ": EnergyCost < 0");
            if (Cooldown < 0f) errors.Add(Key + ": Cooldown < 0");
            if (!(Range > 0f)) errors.Add(Key + ": Range must be > 0");
            if (Damage < 0f) errors.Add(Key + ": Damage < 0");
            if (Knockback < 0f) errors.Add(Key + ": Knockback < 0");
            if (Targeting == AbilityTargeting.Projectile)
            {
                if (!(ProjectileSpeed > 0f)) errors.Add(Key + ": ProjectileSpeed must be > 0");
                if (ProjectileRadius < 0f) errors.Add(Key + ": ProjectileRadius < 0");
            }
            if (Targeting == AbilityTargeting.Cone && !(ConeHalfAngleDegrees > 0f && ConeHalfAngleDegrees <= 180f))
                errors.Add(Key + ": ConeHalfAngleDegrees must be in (0, 180]");
            if (AppliesStatus != StatusFlags.None && !(StatusDuration > 0f)) errors.Add(Key + ": status needs a duration");
            if (StatusDamagePerSecond < 0f) errors.Add(Key + ": StatusDamagePerSecond < 0");
            return errors;
        }
    }

    /// <summary>Immutable set of validated abilities. Construction throws on invalid or duplicate data.</summary>
    public sealed class AbilityCatalog : IAbilityCatalog
    {
        private readonly Dictionary<int, AbilityDefinition> _byId = new Dictionary<int, AbilityDefinition>();
        private readonly int[] _ids;

        public AbilityCatalog(IEnumerable<AbilityDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException("definitions");
            var ids = new List<int>();
            var errors = new List<string>();
            foreach (var definition in definitions)
            {
                if (definition == null) { errors.Add("null definition"); continue; }
                errors.AddRange(definition.Validate());
                if (_byId.ContainsKey(definition.Id)) { errors.Add("duplicate ability id " + definition.Id); continue; }
                _byId.Add(definition.Id, definition);
                ids.Add(definition.Id);
            }
            if (errors.Count > 0) throw new ArgumentException("Invalid ability catalog: " + string.Join("; ", errors.ToArray()));
            _ids = ids.ToArray();
        }

        public int[] Ids { get { return (int[])_ids.Clone(); } }

        public bool Contains(int abilityId)
        {
            return _byId.ContainsKey(abilityId);
        }

        public bool TryGet(int abilityId, out AbilityDefinition definition)
        {
            return _byId.TryGetValue(abilityId, out definition);
        }
    }

    /// <summary>
    /// One declarative reaction: when an ability of <see cref="Incoming"/> element hits a target that carries
    /// <see cref="Requires"/>, apply the listed changes. Rules are checked in order; the first match wins.
    /// </summary>
    public sealed class ReactionRule
    {
        public ReactionKind Kind;
        public StatusFlags Requires;
        public ElementKind Incoming;
        public float BonusDamage;
        public StatusFlags Removes;
        public StatusFlags Adds;
        /// <summary>True: the ability's own status is not applied (e.g. Steam: fire does not ignite a wet target).</summary>
        public bool SuppressAbilityStatus;
    }

    /// <summary>Balancing and reaction configuration shared by host and tests.</summary>
    public sealed class CombatRules
    {
        public float ActorHitRadius = 0.5f;
        /// <summary>Damage-over-time is applied in steps of this length (fewer events than per frame).</summary>
        public float DamageTickInterval = 0.5f;
        public float SimulationTickRate = 20f;
        public readonly List<ReactionRule> Reactions = new List<ReactionRule>();

        public static CombatRules CreateDefault()
        {
            var rules = new CombatRules();
            rules.Reactions.Add(new ReactionRule
            {
                Kind = ReactionKind.Steam, Requires = StatusFlags.Wet, Incoming = ElementKind.Fire,
                BonusDamage = 15f, Removes = StatusFlags.Wet, SuppressAbilityStatus = true
            });
            rules.Reactions.Add(new ReactionRule
            {
                Kind = ReactionKind.Extinguish, Requires = StatusFlags.Burning, Incoming = ElementKind.Water,
                BonusDamage = 5f, Removes = StatusFlags.Burning
            });
            return rules;
        }

        public ReactionRule Find(StatusFlags targetStatus, ElementKind incoming)
        {
            foreach (var rule in Reactions)
            {
                if (rule.Incoming == incoming && (targetStatus & rule.Requires) == rule.Requires && rule.Requires != StatusFlags.None)
                    return rule;
            }
            return null;
        }
    }

    /// <summary>The two abilities of the first vertical slice; values mirror the authored assets.</summary>
    public static class StarterAbilities
    {
        public const int FireballId = 101;
        public const int WaterWaveId = 201;
        public const int FireballCue = 1001;
        public const int WaterWaveCue = 2001;

        public static AbilityDefinition Fireball()
        {
            return new AbilityDefinition
            {
                Id = FireballId, Key = "fireball", Element = ElementKind.Fire, Targeting = AbilityTargeting.Projectile,
                EnergyCost = 20f, Cooldown = 0.8f, Range = 14f, ProjectileSpeed = 16f, ProjectileRadius = 0.3f,
                Damage = 20f, AppliesStatus = StatusFlags.Burning, StatusDuration = 4f, StatusDamagePerSecond = 5f,
                CueId = FireballCue
            };
        }

        public static AbilityDefinition WaterWave()
        {
            return new AbilityDefinition
            {
                Id = WaterWaveId, Key = "water_wave", Element = ElementKind.Water, Targeting = AbilityTargeting.Cone,
                EnergyCost = 30f, Cooldown = 2f, Range = 5f, ConeHalfAngleDegrees = 45f, Damage = 10f, Knockback = 2.5f,
                AppliesStatus = StatusFlags.Wet, StatusDuration = 6f, CueId = WaterWaveCue
            };
        }
    }
}
