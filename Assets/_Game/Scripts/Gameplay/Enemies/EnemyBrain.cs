using System.Collections.Generic;
using Avatar5.Contracts;
using Avatar5.Gameplay.Movement;

namespace Avatar5.Gameplay.Enemies
{
    public enum EnemyState : byte { Idle, Approach, Telegraph, Attack, Recover, Return, Dead }

    /// <summary>Boss phase: entered once health drops to <see cref="HealthFraction"/> or below.</summary>
    public sealed class EnemyPhase
    {
        public float HealthFraction;
        public float TelegraphDuration;
        public float RecoverDuration;
        public int AttackAbilityId;
        public int TelegraphCueId;
        public float MoveSpeedMultiplier = 1f;
        /// <summary>Attack radius used for the telegraph ring; informational for presentation.</summary>
        public float AttackStartRange;
    }

    /// <summary>Balancing data of one enemy archetype; authored as asset, validated here.</summary>
    public sealed class EnemyDefinition
    {
        /// <summary>Ranged enemies back off when a player is closer than this (0 = never).</summary>
        public float PreferredMinRange;
        /// <summary>Optional later phases (bosses), ordered by decreasing health fraction.</summary>
        public readonly List<EnemyPhase> Phases = new List<EnemyPhase>();
        public int EnemyTypeId;
        public string Key;
        public float MaxHealth = 100f;
        public float MoveSpeed = 3.5f;
        /// <summary>Players closer than this to the enemy are noticed.</summary>
        public float AggroRadius = 10f;
        /// <summary>The enemy gives up and walks home when it is farther than this from its home point.</summary>
        public float LeashRadius = 18f;
        /// <summary>Distance at which the enemy stops and starts telegraphing.</summary>
        public float AttackStartRange = 2f;
        public float TelegraphDuration = 0.9f;
        public float RecoverDuration = 1.2f;
        /// <summary>A new target must be this much closer than the current one before the enemy switches.</summary>
        public float RetargetHysteresis = 3f;
        public int AttackAbilityId;
        public int TelegraphCueId;

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (EnemyTypeId <= 0) errors.Add("EnemyTypeId must be > 0");
            if (!(MaxHealth > 0f)) errors.Add(Key + ": MaxHealth must be > 0");
            if (!(MoveSpeed > 0f)) errors.Add(Key + ": MoveSpeed must be > 0");
            if (!(AggroRadius > 0f) || LeashRadius < AggroRadius) errors.Add(Key + ": need 0 < AggroRadius <= LeashRadius");
            if (!(AttackStartRange > 0f)) errors.Add(Key + ": AttackStartRange must be > 0");
            if (TelegraphDuration < 0.3f) errors.Add(Key + ": telegraph shorter than 0.3 s is not readable");
            if (RecoverDuration < 0f) errors.Add(Key + ": RecoverDuration < 0");
            if (AttackAbilityId <= 0) errors.Add(Key + ": AttackAbilityId missing");
            if (PreferredMinRange < 0f || (PreferredMinRange > 0f && PreferredMinRange >= AttackStartRange)) errors.Add(Key + ": PreferredMinRange must be below AttackStartRange");
            var previous = 1f;
            foreach (var phase in Phases)
            {
                if (!(phase.HealthFraction > 0f && phase.HealthFraction < previous)) errors.Add(Key + ": phases need decreasing health fractions in (0,1)");
                if (phase.TelegraphDuration < 0.3f) errors.Add(Key + ": phase telegraph shorter than 0.3 s is not readable");
                if (phase.AttackAbilityId <= 0) errors.Add(Key + ": phase without attack");
                previous = phase.HealthFraction;
            }
            return errors;
        }
    }

    /// <summary>A player the brain may chase (built by the host each tick).</summary>
    public struct EnemyTargetCandidate
    {
        public ulong ActorId;
        public Float2 Position;
        public bool Alive;
        /// <summary>How many other enemies already chase this player (anti-focus).</summary>
        public int ChasedByOthers;
    }

    /// <summary>What the host should do for this enemy after a tick.</summary>
    public struct EnemyBrainOutput
    {
        public EnemyState State;
        public bool StateChanged;
        public ulong TargetActorId;
        /// <summary>Where to walk; only meaningful when <see cref="HasDestination"/>.</summary>
        public Float2 Destination;
        public bool HasDestination;
        /// <summary>True on the tick the telegraph starts (show the marker now).</summary>
        public bool TelegraphStarted;
        /// <summary>True on the tick the attack must be resolved through the combat core.</summary>
        public bool ReleaseAttack;
        /// <summary>Attack centre/direction locked at telegraph start, so players can step out of it.</summary>
        public Float2 AttackOrigin;
        public Float2 AttackAimPoint;
    }

    /// <summary>
    /// Engine-free enemy decision state machine (host only). It never touches health: the host turns
    /// <see cref="EnemyBrainOutput.ReleaseAttack"/> into an AbilityRequest for the combat core.
    /// Idle → Approach → Telegraph → Attack → Recover → (Approach | Return); Dead until revived.
    /// </summary>
    public sealed class EnemyBrain
    {
        private readonly EnemyDefinition _definition;
        private readonly Float2 _home;
        private EnemyState _state = EnemyState.Idle;
        private float _timer;
        private ulong _target;
        private Float2 _lockedOrigin;
        private Float2 _lockedAim;
        private int _phase;

        public EnemyBrain(EnemyDefinition definition, Float2 home)
        {
            var errors = definition.Validate();
            if (errors.Count > 0) throw new System.ArgumentException(string.Join("; ", errors.ToArray()));
            _definition = definition;
            _home = home;
        }

        public EnemyState State { get { return _state; } }
        public ulong Target { get { return _target; } }
        public EnemyDefinition Definition { get { return _definition; } }

        /// <summary>0 = base phase, 1.. = index+1 into <see cref="EnemyDefinition.Phases"/>.</summary>
        public int Phase { get { return _phase; } }
        public int CurrentAttackAbilityId { get { return _phase == 0 ? _definition.AttackAbilityId : _definition.Phases[_phase - 1].AttackAbilityId; } }
        public int CurrentTelegraphCueId { get { return _phase == 0 ? _definition.TelegraphCueId : _definition.Phases[_phase - 1].TelegraphCueId; } }
        public float CurrentTelegraphDuration { get { return _phase == 0 ? _definition.TelegraphDuration : _definition.Phases[_phase - 1].TelegraphDuration; } }
        private float CurrentRecoverDuration { get { return _phase == 0 ? _definition.RecoverDuration : _definition.Phases[_phase - 1].RecoverDuration; } }
        private float CurrentAttackStartRange
        {
            get
            {
                if (_phase == 0) return _definition.AttackStartRange;
                var r = _definition.Phases[_phase - 1].AttackStartRange;
                return r > 0f ? r : _definition.AttackStartRange;
            }
        }
        public float CurrentMoveSpeed { get { return _definition.MoveSpeed * (_phase == 0 ? 1f : _definition.Phases[_phase - 1].MoveSpeedMultiplier); } }

        /// <summary>Advances boss phases from the current health fraction. Phases never go back. Returns true on a change.</summary>
        public bool UpdatePhase(float healthFraction)
        {
            var changed = false;
            while (_phase < _definition.Phases.Count && healthFraction <= _definition.Phases[_phase].HealthFraction)
            {
                _phase++;
                changed = true;
            }
            return changed;
        }

        public void OnDefeated()
        {
            _state = EnemyState.Dead;
            _target = 0;
        }

        /// <summary>
        /// Interrupts whatever the enemy does (frozen, stunned): a running telegraph is cancelled without an attack
        /// and the enemy recovers for at least <paramref name="seconds"/>. Returns true if a telegraph was cancelled.
        /// </summary>
        public bool Stagger(float seconds)
        {
            if (_state == EnemyState.Dead) return false;
            var cancelled = _state == EnemyState.Telegraph;
            _state = EnemyState.Recover;
            _timer = seconds > _timer || cancelled ? seconds : _timer;
            return cancelled;
        }

        public void OnRevived()
        {
            _state = EnemyState.Idle;
            _target = 0;
            _timer = 0f;
        }

        public EnemyBrainOutput Tick(float dt, Float2 self, IList<EnemyTargetCandidate> candidates)
        {
            var output = new EnemyBrainOutput();
            var previous = _state;
            if (_state == EnemyState.Dead)
            {
                output.State = _state;
                return output;
            }

            var hasTarget = TryResolveTarget(self, candidates, out var targetPosition);
            var awayFromHome = Distance(self, _home);

            switch (_state)
            {
                case EnemyState.Idle:
                    if (hasTarget) _state = EnemyState.Approach;
                    break;

                case EnemyState.Approach:
                    if (!hasTarget || awayFromHome > _definition.LeashRadius)
                    {
                        _target = 0;
                        _state = EnemyState.Return;
                        break;
                    }
                    var toTarget = Distance(self, targetPosition);
                    if (_definition.PreferredMinRange > 0f && toTarget < _definition.PreferredMinRange)
                    {
                        // Ranged enemies keep their distance instead of attacking point-blank.
                        var away = new Float2(self.X - targetPosition.X, self.Y - targetPosition.Y);
                        var length = PlanarMath.Length(away);
                        away = length > 1e-3f ? PlanarMath.Scale(away, 3f / length) : new Float2(3f, 0f);
                        output.HasDestination = true;
                        output.Destination = PlanarMath.Add(self, away);
                        break;
                    }
                    if (toTarget <= CurrentAttackStartRange)
                    {
                        _state = EnemyState.Telegraph;
                        _timer = CurrentTelegraphDuration;
                        _lockedOrigin = self;
                        _lockedAim = targetPosition;
                        output.TelegraphStarted = true;
                        break;
                    }
                    output.HasDestination = true;
                    output.Destination = targetPosition;
                    break;

                case EnemyState.Telegraph:
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        _state = EnemyState.Recover;
                        _timer = CurrentRecoverDuration;
                        output.ReleaseAttack = true;
                        output.State = EnemyState.Attack;
                    }
                    break;

                case EnemyState.Recover:
                    _timer -= dt;
                    if (_timer <= 0f) _state = hasTarget ? EnemyState.Approach : EnemyState.Return;
                    break;

                case EnemyState.Return:
                    if (hasTarget && awayFromHome < _definition.LeashRadius * 0.5f)
                    {
                        _state = EnemyState.Approach;
                        break;
                    }
                    if (awayFromHome < 0.5f)
                    {
                        _state = EnemyState.Idle;
                        break;
                    }
                    output.HasDestination = true;
                    output.Destination = _home;
                    break;
            }

            if (!output.ReleaseAttack) output.State = _state;
            output.StateChanged = previous != _state || output.ReleaseAttack;
            output.TargetActorId = _target;
            output.AttackOrigin = _lockedOrigin;
            output.AttackAimPoint = _lockedAim;
            return output;
        }

        /// <summary>
        /// Keeps the current target while it stays valid; otherwise picks the closest player in aggro range,
        /// preferring players nobody else is chasing. A clearly closer player steals the aggro (hysteresis).
        /// </summary>
        private bool TryResolveTarget(Float2 self, IList<EnemyTargetCandidate> candidates, out Float2 targetPosition)
        {
            targetPosition = default(Float2);
            var currentValid = false;
            var currentDistance = float.MaxValue;
            ulong best = 0;
            var bestScore = float.MaxValue;
            var bestPosition = default(Float2);

            for (var i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (!c.Alive) continue;
                var d = Distance(self, c.Position);
                if (c.ActorId == _target && d <= _definition.LeashRadius)
                {
                    currentValid = true;
                    currentDistance = d;
                    targetPosition = c.Position;
                }
                if (d > _definition.AggroRadius) continue;
                // Anti-focus: each enemy already chasing this player counts as 4 m extra distance.
                var score = d + 4f * c.ChasedByOthers;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = c.ActorId;
                    bestPosition = c.Position;
                }
            }

            if (currentValid && (best == 0 || best == _target || bestScore > currentDistance - _definition.RetargetHysteresis))
                return true;
            if (best == 0)
            {
                _target = 0;
                return false;
            }
            _target = best;
            targetPosition = bestPosition;
            return true;
        }

        private static float Distance(Float2 a, Float2 b)
        {
            return PlanarMath.Length(new Float2(a.X - b.X, a.Y - b.Y));
        }
    }

    /// <summary>The vertical-slice archetypes (brute, skirmisher, caster) and the mini-boss, with their attacks.</summary>
    public static class StarterEnemies
    {
        public const int BruteTypeId = 1, SkirmisherTypeId = 2, CasterTypeId = 3, WardenTypeId = 10;
        public const int BruteSlamAbilityId = 901, SkirmisherSlashId = 902, CasterBoltId = 903, WardenSlamId = 910, WardenShockwaveId = 911;
        public const int BruteSlamCue = 9001;
        public const int BruteTelegraphCue = 9000;
        public const int SkirmisherTelegraphCue = 9010, SkirmisherSlashCue = 9011;
        public const int CasterTelegraphCue = 9020, CasterBoltCue = 9021;
        public const int WardenTelegraphCue = 9100, WardenSlamCue = 9101, WardenPhase2TelegraphCue = 9102, WardenShockwaveCue = 9103;

        public static EnemyDefinition Get(int enemyTypeId)
        {
            switch (enemyTypeId)
            {
                case BruteTypeId: return Brute();
                case SkirmisherTypeId: return Skirmisher();
                case CasterTypeId: return Caster();
                case WardenTypeId: return Warden();
                default: throw new System.ArgumentException("Unknown enemy type " + enemyTypeId);
            }
        }

        public static Combat.AbilityDefinition[] AllAttacks()
        {
            return new[] { BruteSlam(), SkirmisherSlash(), CasterBolt(), WardenSlam(), WardenShockwave() };
        }

        public static EnemyDefinition Skirmisher()
        {
            return new EnemyDefinition
            {
                EnemyTypeId = SkirmisherTypeId, Key = "skirmisher", MaxHealth = 70f, MoveSpeed = 5f, AggroRadius = 11f, LeashRadius = 18f,
                AttackStartRange = 1.5f, TelegraphDuration = 0.55f, RecoverDuration = 0.7f, AttackAbilityId = SkirmisherSlashId,
                TelegraphCueId = SkirmisherTelegraphCue
            };
        }

        public static EnemyDefinition Caster()
        {
            return new EnemyDefinition
            {
                EnemyTypeId = CasterTypeId, Key = "caster", MaxHealth = 60f, MoveSpeed = 3f, AggroRadius = 14f, LeashRadius = 20f,
                AttackStartRange = 9f, PreferredMinRange = 4f, TelegraphDuration = 0.8f, RecoverDuration = 1.4f,
                AttackAbilityId = CasterBoltId, TelegraphCueId = CasterTelegraphCue
            };
        }

        public static EnemyDefinition Warden()
        {
            var warden = new EnemyDefinition
            {
                EnemyTypeId = WardenTypeId, Key = "warden", MaxHealth = 600f, MoveSpeed = 2.6f, AggroRadius = 12f, LeashRadius = 16f,
                AttackStartRange = 2.6f, TelegraphDuration = 1.1f, RecoverDuration = 1.4f, AttackAbilityId = WardenSlamId,
                TelegraphCueId = WardenTelegraphCue
            };
            warden.Phases.Add(new EnemyPhase
            {
                HealthFraction = 0.5f, TelegraphDuration = 0.85f, RecoverDuration = 0.9f, AttackAbilityId = WardenShockwaveId,
                TelegraphCueId = WardenPhase2TelegraphCue, MoveSpeedMultiplier = 1.3f, AttackStartRange = 3.5f
            });
            return warden;
        }

        public static Combat.AbilityDefinition SkirmisherSlash()
        {
            return new Combat.AbilityDefinition
            {
                Id = SkirmisherSlashId, Key = "skirmisher_slash", Element = ElementKind.Air, Targeting = AbilityTargeting.Cone,
                Range = 2f, ConeHalfAngleDegrees = 60f, Damage = 9f, Knockback = 0.5f, CueId = SkirmisherSlashCue
            };
        }

        public static Combat.AbilityDefinition CasterBolt()
        {
            return new Combat.AbilityDefinition
            {
                Id = CasterBoltId, Key = "caster_bolt", Element = ElementKind.Fire, Targeting = AbilityTargeting.Projectile,
                Range = 14f, ProjectileSpeed = 11f, ProjectileRadius = 0.3f, Damage = 12f, CueId = CasterBoltCue
            };
        }

        public static Combat.AbilityDefinition WardenSlam()
        {
            return new Combat.AbilityDefinition
            {
                Id = WardenSlamId, Key = "warden_slam", Element = ElementKind.Earth, Targeting = AbilityTargeting.Cone,
                Range = 3.5f, ConeHalfAngleDegrees = 180f, Damage = 22f, Knockback = 2.5f, CueId = WardenSlamCue
            };
        }

        public static Combat.AbilityDefinition WardenShockwave()
        {
            return new Combat.AbilityDefinition
            {
                Id = WardenShockwaveId, Key = "warden_shockwave", Element = ElementKind.Earth, Targeting = AbilityTargeting.Cone,
                Range = 5f, ConeHalfAngleDegrees = 180f, Damage = 16f, Knockback = 3f, CueId = WardenShockwaveCue
            };
        }

        public static EnemyDefinition Brute()
        {
            return new EnemyDefinition
            {
                EnemyTypeId = BruteTypeId, Key = "brute", MaxHealth = 160f, MoveSpeed = 3.2f, AggroRadius = 10f,
                LeashRadius = 18f, AttackStartRange = 1.8f, TelegraphDuration = 0.9f, RecoverDuration = 1.2f,
                AttackAbilityId = BruteSlamAbilityId, TelegraphCueId = BruteTelegraphCue
            };
        }

        /// <summary>Slam: full circle (cone 180°) around the brute, telegraphed by the brain before release.</summary>
        public static Combat.AbilityDefinition BruteSlam()
        {
            return new Combat.AbilityDefinition
            {
                Id = BruteSlamAbilityId, Key = "brute_slam", Element = ElementKind.Earth, Targeting = AbilityTargeting.Cone,
                EnergyCost = 0f, Cooldown = 0f, Range = 2.4f, ConeHalfAngleDegrees = 180f, Damage = 18f, Knockback = 1.5f,
                CueId = BruteSlamCue
            };
        }
    }
}
