using System;

namespace Avatar5.Contracts
{
    public enum TeamKind : byte { Players, Enemies, Neutral }

    public enum AbilityTargeting : byte
    {
        /// <summary>Travels along the aim direction and hits the first enemy (or wall) on its way.</summary>
        Projectile,
        /// <summary>Instant cone in front of the caster (radius = Range, half-angle = ConeHalfAngleDegrees).</summary>
        Cone
    }

    /// <summary>Status effects as flags so an actor can carry several at once and replicate them in one byte.</summary>
    [Flags]
    public enum StatusFlags : byte
    {
        None = 0,
        Burning = 1 << 0,
        Wet = 1 << 1,
        /// <summary>Cannot move, act or finish a telegraph while frozen (interrupts enemy attacks).</summary>
        Frozen = 1 << 2
    }

    /// <summary>Value of <see cref="CombatEvent.ResultKind"/>.</summary>
    public enum CombatEventKind
    {
        AbilityCastAccepted = 1,
        AbilityRejected = 2,
        DamageApplied = 3,
        StatusApplied = 4,
        StatusRemoved = 5,
        ReactionTriggered = 6,
        ActorDefeated = 7,
        ActorRevived = 8,
        ProjectileEnded = 9,
        /// <summary>An enemy announced an attack (Amount = telegraph seconds, AbilityId = the attack).</summary>
        TelegraphStarted = 10,
        /// <summary>A boss entered a new phase (EffectId = phase index, 0 = first).</summary>
        BossPhaseChanged = 11
    }

    /// <summary>Elemental reactions (value of <see cref="CombatEvent.EffectId"/> for ReactionTriggered).</summary>
    public enum ReactionKind
    {
        None = 0,
        /// <summary>Fire hits a wet target: the water flashes to steam, bonus damage, Wet removed, no Burning.</summary>
        Steam = 1,
        /// <summary>Water hits a burning target: fire goes out, target becomes Wet.</summary>
        Extinguish = 2,
        /// <summary>Ice hits a wet target: it freezes solid for a moment.</summary>
        Freeze = 3,
        /// <summary>Wind fans a burning target: the fire flares up for bonus damage and keeps burning.</summary>
        Flare = 4
    }

    /// <summary>Host-authoritative, readable combat state of one actor (replicated to clients).</summary>
    [Serializable]
    public struct ActorCombatState
    {
        public ulong ActorId;
        public TeamKind Team;
        public float Health;
        public float MaxHealth;
        public float Energy;
        public float MaxEnergy;
        public StatusFlags Status;
        public bool Defeated;
    }

    /// <summary>Result of a cast request at the moment the host evaluated it.</summary>
    [Serializable]
    public struct AbilityCastResult
    {
        public RequestStatus Status;
        /// <summary>Unique id of the accepted cast; hits of this cast refer to it. 0 when rejected.</summary>
        public ulong CastId;
    }

    public interface IAbilityCatalog
    {
        bool Contains(int abilityId);
        /// <summary>Ability ids in stable order (slot assignment uses this until P06 loadouts exist).</summary>
        int[] Ids { get; }
    }

    /// <summary>Effective modifiers of one actor (talents, later equipment). Neutral = no change.</summary>
    [Serializable]
    public struct ActorStats
    {
        public float DamageMultiplier;
        public float CooldownMultiplier;
        public float MaxHealthBonus;

        public static ActorStats Neutral { get { return new ActorStats { DamageMultiplier = 1f, CooldownMultiplier = 1f }; } }
    }

    /// <summary>P06 → P02: effective stats, deterministic and UI-free.</summary>
    public interface IStatProvider
    {
        ActorStats GetStats(ulong actorId);
    }

    /// <summary>Host-only combat port. Clients never call this; they send AbilityRequests through P03.</summary>
    public interface ICombatService
    {
        AbilityCastResult TryCast(in AbilityRequest request);
        bool TryGetState(ulong actorId, out ActorCombatState state);
    }

    /// <summary>Ordered stream of host combat events (EventId strictly increasing per session).</summary>
    public interface ICombatEventStream
    {
        event Action<CombatEvent> EventRaised;
    }
}

namespace Avatar5.Contracts
{
    /// <summary>Read-only combat state of one actor for HUD, health bars and AI (written only by the host).</summary>
    public interface ICombatStateSource
    {
        ActorCombatState CombatState { get; }
    }
}
