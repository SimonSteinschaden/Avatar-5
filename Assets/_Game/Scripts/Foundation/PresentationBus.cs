using System;
using System.Collections.Generic;
using Avatar5.Contracts;
using UnityEngine;

namespace Avatar5.Foundation
{
    /// <summary>
    /// Local, per-process fan-out of host combat events and presentation cues to views (VFX, HUD, audio).
    /// The network layer publishes; presentation subscribes. Nothing here changes gameplay state.
    /// </summary>
    public static class PresentationBus
    {
        public static event Action<CombatEvent> CombatEventRaised;
        public static event Action<PresentationCue> CueRaised;

        public static void Publish(in CombatEvent combatEvent)
        {
            var handler = CombatEventRaised;
            if (handler != null) handler(combatEvent);
        }

        public static void Publish(in PresentationCue cue)
        {
            var handler = CueRaised;
            if (handler != null) handler(cue);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CombatEventRaised = null;
            CueRaised = null;
            ActorRegistry.Clear();
        }
    }

    /// <summary>Actor id → scene transform lookup for views (damage numbers, health bars). Per process.</summary>
    public static class ActorRegistry
    {
        private static readonly Dictionary<ulong, Transform> Actors = new Dictionary<ulong, Transform>();

        public static void Register(ulong actorId, Transform actor)
        {
            if (actor != null) Actors[actorId] = actor;
        }

        public static void Unregister(ulong actorId, Transform actor)
        {
            Transform current;
            if (Actors.TryGetValue(actorId, out current) && current == actor) Actors.Remove(actorId);
        }

        public static bool TryGet(ulong actorId, out Transform actor)
        {
            if (Actors.TryGetValue(actorId, out actor) && actor != null) return true;
            actor = null;
            return false;
        }

        internal static void Clear()
        {
            Actors.Clear();
        }
    }
}
