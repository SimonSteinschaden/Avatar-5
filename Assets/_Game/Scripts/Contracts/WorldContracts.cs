using System;

namespace Avatar5.Contracts
{
    public enum WorldObjectKind : byte
    {
        /// <summary>Player-operated switch (Interact). On stays on.</summary>
        Lever,
        /// <summary>Opens when every object listed in its requirements is on/open.</summary>
        Gate,
        /// <summary>Element target: lit by a fire impact nearby.</summary>
        Brazier,
        /// <summary>Level exit: completes the level when a living player stands in it and its requirements are met.</summary>
        Goal
    }

    /// <summary>Value of <see cref="WorldEvent.StateCode"/> for object changes; level completion uses <see cref="LevelCompletedCode"/>.</summary>
    public static class WorldStateCodes
    {
        public const int Off = 0;
        public const int On = 1;
        /// <summary>WorldEvent with this code and WorldObjectId = goal id means LevelCompleted (raised exactly once).</summary>
        public const int LevelCompletedCode = 100;
    }

    /// <summary>Element hitting the world at a point (projectile end, cone area); used by element puzzles.</summary>
    [Serializable]
    public struct ElementImpact
    {
        public ulong SourceActorId;
        public ElementKind Element;
        public Float2 Position;
        public float Radius;
    }

    /// <summary>Host-only world state port (P05). Clients send Interact intents; they never change world state.</summary>
    public interface IWorldStateService
    {
        int LevelId { get; }
        bool IsCompleted { get; }
        int GetState(int worldObjectId);
        RequestStatus TryInteract(ulong actorId, int worldObjectId, Float2 actorPosition);
        void ApplyImpact(in ElementImpact impact);
        event Action<WorldEvent> Changed;
    }
}
