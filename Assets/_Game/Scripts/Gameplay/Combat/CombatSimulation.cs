using System;
using System.Collections.Generic;
using Avatar5.Contracts;
using Avatar5.Gameplay.Movement;

namespace Avatar5.Gameplay.Combat
{
    /// <summary>Engine adapter the host simulation asks about positions, walls and pushes.</summary>
    public interface ICombatWorld
    {
        bool TryGetPosition(ulong actorId, out Float2 position);
        /// <summary>Fraction 0..1 of the segment that is free before a wall; 1 = nothing in the way.</summary>
        float FreeFraction(Float2 from, Float2 to);
        void ApplyKnockback(ulong actorId, Float2 displacement);
    }

    /// <summary>
    /// Host-only combat core: validates casts, simulates projectiles logically, resolves hits, statuses and
    /// reactions, and publishes every outcome as a <see cref="CombatEvent"/> with a unique, increasing EventId.
    /// Each (cast, target) pair is applied at most once, so retransmitted requests or events never double damage.
    /// </summary>
    public sealed class CombatSimulation : ICombatService, ICombatEventStream
    {
        private sealed class Actor
        {
            public ActorCombatState State;
            public float EnergyRegen;
            public int[] Loadout;
            public readonly Dictionary<int, double> ReadyAt = new Dictionary<int, double>();
            public double BurningUntil;
            public double WetUntil;
            public double FrozenUntil;
            public float BurningDps;
            public ulong BurningSource;
            public double NextBurnTick;
        }

        private sealed class Projectile
        {
            public ulong CastId;
            public ulong Owner;
            public TeamKind OwnerTeam;
            public AbilityDefinition Ability;
            public Float2 Position;
            public Float2 Direction;
            public float Travelled;
        }

        private readonly AbilityCatalog _catalog;
        private readonly CombatRules _rules;
        private readonly ICombatWorld _world;
        private readonly Dictionary<ulong, Actor> _actors = new Dictionary<ulong, Actor>();
        private readonly List<ulong> _actorOrder = new List<ulong>();
        private readonly List<Projectile> _projectiles = new List<Projectile>();
        private readonly HashSet<(ulong cast, ulong target)> _appliedHits = new HashSet<(ulong cast, ulong target)>();
        private readonly SequenceDeduplicator _requests = new SequenceDeduplicator();
        private ulong _nextEventId = 1;
        private uint _visualSeed = 0x5EED;

        public event Action<CombatEvent> EventRaised;
        public event Action<PresentationCue> CueRaised;
        /// <summary>Where an element touched the world (projectile end, cone area) — for element puzzles (P05).</summary>
        public event Action<ElementImpact> ImpactRaised;

        public CombatSimulation(AbilityCatalog catalog, CombatRules rules, ICombatWorld world)
        {
            if (catalog == null) throw new ArgumentNullException("catalog");
            if (world == null) throw new ArgumentNullException("world");
            _catalog = catalog;
            _rules = rules ?? CombatRules.CreateDefault();
            _world = world;
        }

        public double Time { get; private set; }
        public uint ServerTick { get { return (uint)(Time * _rules.SimulationTickRate); } }
        public int ActiveProjectiles { get { return _projectiles.Count; } }
        public AbilityCatalog Catalog { get { return _catalog; } }

        /// <summary>Optional P06 stats (talents). Null = everyone neutral.</summary>
        public IStatProvider Stats { get; set; }

        private ActorStats StatsOf(ulong actorId)
        {
            return Stats != null ? Stats.GetStats(actorId) : ActorStats.Neutral;
        }

        /// <summary>Sets an actor's maximum health (talent changes); current health keeps its missing amount.</summary>
        public void SetMaxHealth(ulong actorId, float maxHealth)
        {
            Actor actor;
            if (!_actors.TryGetValue(actorId, out actor) || !(maxHealth > 0f)) return;
            var missing = actor.State.MaxHealth - actor.State.Health;
            actor.State.MaxHealth = maxHealth;
            if (!actor.State.Defeated) actor.State.Health = Math.Max(1f, maxHealth - missing);
        }

        // ---- actors ----------------------------------------------------------------------------

        public void RegisterActor(ulong actorId, TeamKind team, float maxHealth, float maxEnergy, float energyRegen, int[] loadout)
        {
            if (_actors.ContainsKey(actorId)) RemoveActor(actorId);
            var actor = new Actor
            {
                State = new ActorCombatState
                {
                    ActorId = actorId, Team = team, Health = maxHealth, MaxHealth = maxHealth,
                    Energy = maxEnergy, MaxEnergy = maxEnergy
                },
                EnergyRegen = energyRegen,
                Loadout = loadout ?? new int[0]
            };
            _actors.Add(actorId, actor);
            _actorOrder.Add(actorId);
        }

        public bool RemoveActor(ulong actorId)
        {
            _requests.RemoveActor(actorId);
            _actorOrder.Remove(actorId);
            return _actors.Remove(actorId);
        }

        /// <summary>Changes an actor's abilities (class choice). Cooldowns of removed abilities are dropped.</summary>
        public bool SetLoadout(ulong actorId, int[] loadout)
        {
            Actor actor;
            if (!_actors.TryGetValue(actorId, out actor) || loadout == null) return false;
            foreach (var id in loadout) if (!_catalog.Contains(id)) return false;
            actor.Loadout = (int[])loadout.Clone();
            actor.ReadyAt.Clear();
            return true;
        }

        /// <summary>Restores a defeated actor to full health (dummies, later checkpoints).</summary>
        public void Revive(ulong actorId)
        {
            Actor actor;
            if (!_actors.TryGetValue(actorId, out actor) || !actor.State.Defeated) return;
            actor.State.Defeated = false;
            actor.State.Health = actor.State.MaxHealth;
            actor.State.Status = StatusFlags.None;
            Raise(CombatEventKind.ActorRevived, 0, actorId, 0, 0, actor.State.Health);
        }

        public bool TryGetState(ulong actorId, out ActorCombatState state)
        {
            Actor actor;
            if (_actors.TryGetValue(actorId, out actor))
            {
                state = actor.State;
                return true;
            }
            state = default(ActorCombatState);
            return false;
        }

        /// <summary>Seconds until the ability is ready again for this actor (0 = ready).</summary>
        public float CooldownRemaining(ulong actorId, int abilityId)
        {
            Actor actor;
            double readyAt;
            if (!_actors.TryGetValue(actorId, out actor) || !actor.ReadyAt.TryGetValue(abilityId, out readyAt)) return 0f;
            return readyAt > Time ? (float)(readyAt - Time) : 0f;
        }

        // ---- casting ---------------------------------------------------------------------------

        public AbilityCastResult TryCast(in AbilityRequest request)
        {
            var status = Validate(request);
            if (status != RequestStatus.Accepted)
            {
                if (status != RequestStatus.Duplicate) Raise(CombatEventKind.AbilityRejected, request.ActorId, 0, request.AbilityId, (int)status, 0f);
                return new AbilityCastResult { Status = status };
            }

            var actor = _actors[request.ActorId];
            AbilityDefinition ability;
            _catalog.TryGet(request.AbilityId, out ability);
            Float2 origin;
            _world.TryGetPosition(request.ActorId, out origin);
            var direction = PlanarMath.Normalize(new Float2(request.TargetOnGround.X - origin.X, request.TargetOnGround.Y - origin.Y));

            actor.State.Energy -= ability.EnergyCost;
            actor.ReadyAt[ability.Id] = Time + ability.Cooldown * StatsOf(request.ActorId).CooldownMultiplier;
            var castId = Raise(CombatEventKind.AbilityCastAccepted, request.ActorId, 0, ability.Id, 0, ability.EnergyCost);
            RaiseCue(ability.CueId, origin, direction);

            if (ability.Targeting == AbilityTargeting.Projectile)
            {
                _projectiles.Add(new Projectile
                {
                    CastId = castId, Owner = request.ActorId, OwnerTeam = actor.State.Team, Ability = ability,
                    Position = origin, Direction = direction
                });
            }
            else
            {
                ResolveCone(castId, actor, ability, origin, direction);
                RaiseImpact(request.ActorId, ability.Element, PlanarMath.Add(origin, PlanarMath.Scale(direction, ability.Range * 0.5f)), ability.Range * 0.5f);
            }
            return new AbilityCastResult { Status = RequestStatus.Accepted, CastId = castId };
        }

        /// <summary>
        /// Announces an upcoming attack so every peer can show it before damage happens. Pure information:
        /// the attack itself still goes through <see cref="TryCast"/> when the telegraph ends.
        /// </summary>
        public void RaiseTelegraph(ulong actorId, int abilityId, int cueId, Float2 origin, Float2 aimPoint, float seconds)
        {
            Actor actor;
            if (!_actors.TryGetValue(actorId, out actor) || actor.State.Defeated) return;
            var direction = PlanarMath.Normalize(new Float2(aimPoint.X - origin.X, aimPoint.Y - origin.Y));
            Raise(CombatEventKind.TelegraphStarted, actorId, 0, abilityId, 0, seconds);
            RaiseCue(cueId, origin, direction);
        }

        public void RaisePhaseChanged(ulong actorId, int phase)
        {
            Raise(CombatEventKind.BossPhaseChanged, actorId, 0, 0, phase, 0f);
        }

        public bool IsDefeated(ulong actorId)
        {
            Actor actor;
            return _actors.TryGetValue(actorId, out actor) && actor.State.Defeated;
        }

        private RequestStatus Validate(in AbilityRequest request)
        {
            Actor actor;
            if (!_actors.TryGetValue(request.ActorId, out actor) || actor.State.Defeated) return RequestStatus.RejectedInvalidState;
            if ((actor.State.Status & StatusFlags.Frozen) != 0) return RequestStatus.RejectedInvalidState;
            if (!_requests.TryAccept(request.ActorId, request.SequenceId)) return RequestStatus.Duplicate;

            AbilityDefinition ability;
            if (!_catalog.TryGet(request.AbilityId, out ability) || Array.IndexOf(actor.Loadout, request.AbilityId) < 0)
                return RequestStatus.RejectedInvalidState;
            if (CooldownRemaining(request.ActorId, request.AbilityId) > 0f) return RequestStatus.RejectedCooldown;
            if (actor.State.Energy + 1e-4f < ability.EnergyCost) return RequestStatus.RejectedResource;

            Float2 origin;
            if (!_world.TryGetPosition(request.ActorId, out origin)) return RequestStatus.RejectedInvalidState;
            var toTarget = new Float2(request.TargetOnGround.X - origin.X, request.TargetOnGround.Y - origin.Y);
            // Direction only: a target on top of the caster has no direction to fire in.
            if (PlanarMath.Length(toTarget) < 0.05f) return RequestStatus.RejectedRange;
            return RequestStatus.Accepted;
        }

        private void ResolveCone(ulong castId, Actor caster, AbilityDefinition ability, Float2 origin, Float2 direction)
        {
            var cosLimit = (float)Math.Cos(ability.ConeHalfAngleDegrees * Math.PI / 180.0);
            foreach (var id in _actorOrder.ToArray())
            {
                Actor target;
                Float2 position;
                if (!_actors.TryGetValue(id, out target) || !IsHostile(caster.State.Team, target) || !_world.TryGetPosition(id, out position)) continue;
                var offset = new Float2(position.X - origin.X, position.Y - origin.Y);
                var distance = PlanarMath.Length(offset);
                if (distance > ability.Range + _rules.ActorHitRadius) continue;
                if (distance > 1e-3f)
                {
                    var dir = PlanarMath.Scale(offset, 1f / distance);
                    if (dir.X * direction.X + dir.Y * direction.Y < cosLimit) continue;
                    if (_world.FreeFraction(origin, position) < 0.999f) continue;
                }
                ApplyHit(castId, caster.State.ActorId, ability, id, direction);
            }
        }

        // ---- simulation ------------------------------------------------------------------------

        public void Tick(float deltaTime)
        {
            if (!(deltaTime > 0f)) return;
            Time += deltaTime;
            StepProjectiles(deltaTime);
            StepActors(deltaTime);
        }

        private void StepProjectiles(float dt)
        {
            for (var i = _projectiles.Count - 1; i >= 0; i--)
            {
                var p = _projectiles[i];
                var stepLength = Math.Min(p.Ability.ProjectileSpeed * dt, p.Ability.Range - p.Travelled);
                var end = PlanarMath.Add(p.Position, PlanarMath.Scale(p.Direction, stepLength));

                var wallFraction = _world.FreeFraction(p.Position, end);
                ulong hitId;
                float hitFraction;
                var hitActor = FirstActorOnSegment(p, end, out hitId, out hitFraction);

                if (hitActor && hitFraction <= wallFraction)
                {
                    ApplyHit(p.CastId, p.Owner, p.Ability, hitId, p.Direction);
                    EndProjectile(i, PlanarMath.Add(p.Position, PlanarMath.Scale(p.Direction, stepLength * hitFraction)));
                    continue;
                }
                if (wallFraction < 1f)
                {
                    EndProjectile(i, PlanarMath.Add(p.Position, PlanarMath.Scale(p.Direction, stepLength * wallFraction)));
                    continue;
                }
                p.Position = end;
                p.Travelled += stepLength;
                if (p.Travelled >= p.Ability.Range - 1e-4f) EndProjectile(i, end);
            }
        }

        private bool FirstActorOnSegment(Projectile p, Float2 end, out ulong hitId, out float fraction)
        {
            hitId = 0;
            fraction = float.MaxValue;
            var segment = new Float2(end.X - p.Position.X, end.Y - p.Position.Y);
            var lengthSq = segment.X * segment.X + segment.Y * segment.Y;
            var reach = _rules.ActorHitRadius + p.Ability.ProjectileRadius;
            foreach (var id in _actorOrder)
            {
                Actor target;
                Float2 position;
                if (id == p.Owner || !_actors.TryGetValue(id, out target) || !IsHostile(p.OwnerTeam, target) || !_world.TryGetPosition(id, out position)) continue;
                var t = lengthSq > 1e-8f ? ((position.X - p.Position.X) * segment.X + (position.Y - p.Position.Y) * segment.Y) / lengthSq : 0f;
                if (t < 0f) t = 0f;
                if (t > 1f) t = 1f;
                var cx = p.Position.X + segment.X * t - position.X;
                var cy = p.Position.Y + segment.Y * t - position.Y;
                if (cx * cx + cy * cy > reach * reach || t >= fraction) continue;
                fraction = t;
                hitId = id;
            }
            return hitId != 0;
        }

        private void EndProjectile(int index, Float2 at)
        {
            var p = _projectiles[index];
            _projectiles.RemoveAt(index);
            Raise(CombatEventKind.ProjectileEnded, p.Owner, 0, p.Ability.Id, (int)p.CastId, 0f);
            RaiseImpact(p.Owner, p.Ability.Element, at, p.Ability.ProjectileRadius + 0.3f);
            RaiseCue(-p.Ability.CueId, at, p.Direction);
        }

        private void StepActors(float dt)
        {
            foreach (var id in _actorOrder.ToArray())
            {
                Actor actor;
                if (!_actors.TryGetValue(id, out actor) || actor.State.Defeated) continue;

                if (actor.State.Energy < actor.State.MaxEnergy)
                    actor.State.Energy = Math.Min(actor.State.MaxEnergy, actor.State.Energy + actor.EnergyRegen * dt);

                if ((actor.State.Status & StatusFlags.Burning) != 0)
                {
                    while (actor.NextBurnTick <= Time + 1e-6 && actor.NextBurnTick <= actor.BurningUntil + 1e-6 && !actor.State.Defeated)
                    {
                        actor.NextBurnTick += _rules.DamageTickInterval;
                        Damage(actor, actor.BurningSource, 0, actor.BurningDps * _rules.DamageTickInterval, 0);
                    }
                    if (Time >= actor.BurningUntil) RemoveStatus(actor, StatusFlags.Burning, 0);
                }
                if ((actor.State.Status & StatusFlags.Wet) != 0 && Time >= actor.WetUntil) RemoveStatus(actor, StatusFlags.Wet, 0);
                if ((actor.State.Status & StatusFlags.Frozen) != 0 && Time >= actor.FrozenUntil) RemoveStatus(actor, StatusFlags.Frozen, 0);
            }
        }

        // ---- hits ------------------------------------------------------------------------------

        private void ApplyHit(ulong castId, ulong sourceId, AbilityDefinition ability, ulong targetId, Float2 direction)
        {
            Actor target;
            if (!_actors.TryGetValue(targetId, out target) || target.State.Defeated) return;
            // One cast damages one target at most once, whatever happens on the wire.
            if (!_appliedHits.Add((castId, targetId))) return;

            var damage = ability.Damage * StatsOf(sourceId).DamageMultiplier;
            var reaction = _rules.Find(target.State.Status, ability.Element, ability.Id);
            if (reaction != null)
            {
                damage += reaction.BonusDamage;
                Raise(CombatEventKind.ReactionTriggered, sourceId, targetId, ability.Id, (int)reaction.Kind, reaction.BonusDamage);
                if (reaction.Removes != StatusFlags.None) RemoveStatus(target, reaction.Removes, ability.Id);
                if (reaction.Adds != StatusFlags.None)
                    AddStatus(target, reaction.Adds, sourceId, ability, reaction.AddsDuration > 0f ? reaction.AddsDuration : ability.StatusDuration);
                if (reaction.RefreshAbilityStatus && (target.State.Status & StatusFlags.Burning) != 0)
                    target.BurningUntil = Time + Math.Max(2f, (float)(target.BurningUntil - Time));
            }

            Damage(target, sourceId, ability.Id, damage, (int)castId);
            if (target.State.Defeated) return;

            if (ability.AppliesStatus != StatusFlags.None && (reaction == null || !reaction.SuppressAbilityStatus))
                AddStatus(target, ability.AppliesStatus, sourceId, ability, ability.StatusDuration);
            if (ability.Knockback > 0f) _world.ApplyKnockback(targetId, PlanarMath.Scale(direction, ability.Knockback));
        }

        private void Damage(Actor target, ulong sourceId, int abilityId, float amount, int castRef)
        {
            if (amount <= 0f || target.State.Defeated) return;
            var applied = Math.Min(amount, target.State.Health);
            target.State.Health -= applied;
            Raise(CombatEventKind.DamageApplied, sourceId, target.State.ActorId, abilityId, castRef, applied);
            if (target.State.Health > 1e-4f) return;
            target.State.Health = 0f;
            target.State.Defeated = true;
            target.State.Status = StatusFlags.None;
            Raise(CombatEventKind.ActorDefeated, sourceId, target.State.ActorId, abilityId, castRef, 0f);
        }

        private void AddStatus(Actor target, StatusFlags status, ulong sourceId, AbilityDefinition ability, float duration)
        {
            var isNew = (target.State.Status & status) != status;
            target.State.Status |= status;
            // Re-applying refreshes the duration instead of stacking.
            if ((status & StatusFlags.Burning) != 0)
            {
                target.BurningUntil = Time + duration;
                target.BurningDps = ability.StatusDamagePerSecond;
                target.BurningSource = sourceId;
                if (isNew) target.NextBurnTick = Time + _rules.DamageTickInterval;
            }
            if ((status & StatusFlags.Wet) != 0) target.WetUntil = Time + duration;
            if ((status & StatusFlags.Frozen) != 0) target.FrozenUntil = Time + duration;
            if (isNew) Raise(CombatEventKind.StatusApplied, sourceId, target.State.ActorId, ability.Id, (int)status, duration);
        }

        private void RemoveStatus(Actor target, StatusFlags status, int abilityId)
        {
            var present = target.State.Status & status;
            if (present == StatusFlags.None) return;
            target.State.Status &= ~status;
            Raise(CombatEventKind.StatusRemoved, 0, target.State.ActorId, abilityId, (int)present, 0f);
        }

        private bool IsHostile(TeamKind sourceTeam, Actor target)
        {
            return !target.State.Defeated && target.State.Team != sourceTeam && target.State.Team != TeamKind.Neutral;
        }

        private ulong Raise(CombatEventKind kind, ulong source, ulong target, int abilityId, int effectId, float amount)
        {
            var evt = new CombatEvent
            {
                EventId = _nextEventId++, ServerTick = ServerTick, SourceActorId = source, TargetActorId = target,
                AbilityId = abilityId, EffectId = effectId, Amount = amount, ResultKind = (int)kind
            };
            var handler = EventRaised;
            if (handler != null) handler(evt);
            return evt.EventId;
        }

        private void RaiseImpact(ulong source, ElementKind element, Float2 at, float radius)
        {
            var handler = ImpactRaised;
            if (handler != null) handler(new ElementImpact { SourceActorId = source, Element = element, Position = at, Radius = radius });
        }

        private void RaiseCue(int cueId, Float2 at, Float2 direction)
        {
            var handler = CueRaised;
            if (handler == null) return;
            _visualSeed = _visualSeed * 1664525u + 1013904223u;
            handler(new PresentationCue
            {
                EventId = _nextEventId++, ServerTick = ServerTick, CueId = cueId,
                Position = new Float3(at.X, 0f, at.Y), Direction = new Float3(direction.X, 0f, direction.Y), VisualSeed = _visualSeed
            });
        }
    }
}
