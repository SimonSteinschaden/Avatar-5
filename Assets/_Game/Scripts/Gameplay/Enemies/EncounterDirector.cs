using System;
using System.Collections.Generic;
using Avatar5.Contracts;
using Avatar5.Gameplay.Movement;

namespace Avatar5.Gameplay.Enemies
{
    public enum EncounterState : byte { Dormant, Active, Completed }

    public sealed class EncounterSpawn
    {
        public int EnemyTypeId;
        public Float2 Position;
        /// <summary>Only spawned when at least this many players are in the session (co-op scaling).</summary>
        public int MinPlayers = 1;
    }

    /// <summary>A group of enemies that starts when a player comes close and completes once all are defeated.</summary>
    public sealed class EncounterDefinition
    {
        /// <summary>Same id as the level's world object of kind Encounter (completion opens what requires it).</summary>
        public int Id;
        public Float2 Center;
        public float ActivationRadius = 8f;
        /// <summary>Extra health per additional player (0.35 = +35 % per player beyond the first), not simply ×4.</summary>
        public float HealthBonusPerExtraPlayer = 0.35f;
        public readonly List<EncounterSpawn> Spawns = new List<EncounterSpawn>();

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (Id <= 0) errors.Add("encounter id must be > 0");
            if (!(ActivationRadius > 0f)) errors.Add("encounter " + Id + ": activation radius must be > 0");
            if (Spawns.Count == 0) errors.Add("encounter " + Id + " has no spawns");
            if (HealthBonusPerExtraPlayer < 0f) errors.Add("encounter " + Id + ": negative scaling");
            var solo = 0;
            foreach (var s in Spawns) if (s.MinPlayers <= 1) solo++;
            if (solo == 0) errors.Add("encounter " + Id + " would be empty for a solo player");
            return errors;
        }
    }

    /// <summary>What the host must spawn for an activated encounter.</summary>
    public struct EncounterSpawnOrder
    {
        public int EncounterId;
        public int EnemyTypeId;
        public Float2 Position;
        public float HealthMultiplier;
    }

    /// <summary>
    /// Host-only encounter state machine: Dormant → Active (when a living player enters the activation radius)
    /// → Completed (all of its enemies defeated, raised exactly once). Encounter enemies do not revive; scaling
    /// depends on the number of players at activation time.
    /// </summary>
    public sealed class EncounterDirector
    {
        private sealed class Running
        {
            public EncounterDefinition Definition;
            public EncounterState State;
            public readonly HashSet<ulong> Alive = new HashSet<ulong>();
            public int Expected;
            public int Registered;
        }

        private readonly Dictionary<int, Running> _encounters = new Dictionary<int, Running>();
        private readonly List<int> _order = new List<int>();
        private readonly Dictionary<ulong, int> _memberOf = new Dictionary<ulong, int>();

        public event Action<int> Completed;

        public EncounterDirector(IEnumerable<EncounterDefinition> encounters)
        {
            var errors = new List<string>();
            foreach (var e in encounters)
            {
                errors.AddRange(e.Validate());
                if (_encounters.ContainsKey(e.Id)) { errors.Add("duplicate encounter " + e.Id); continue; }
                _encounters.Add(e.Id, new Running { Definition = e });
                _order.Add(e.Id);
            }
            if (errors.Count > 0) throw new ArgumentException(string.Join("; ", errors.ToArray()));
        }

        public EncounterState StateOf(int encounterId)
        {
            Running r;
            return _encounters.TryGetValue(encounterId, out r) ? r.State : EncounterState.Dormant;
        }

        public bool IsMember(ulong actorId) { return _memberOf.ContainsKey(actorId); }

        /// <summary>Restored from a save: never spawns again.</summary>
        public void MarkCompleted(int encounterId)
        {
            Running r;
            if (_encounters.TryGetValue(encounterId, out r)) r.State = EncounterState.Completed;
        }

        public static float HealthMultiplier(EncounterDefinition definition, int playerCount)
        {
            return 1f + definition.HealthBonusPerExtraPlayer * Math.Max(0, playerCount - 1);
        }

        /// <summary>Activates dormant encounters a living player stands in. Returns what to spawn now.</summary>
        public List<EncounterSpawnOrder> Tick(IList<Float2> livingPlayers, int playerCount)
        {
            var orders = new List<EncounterSpawnOrder>();
            foreach (var id in _order)
            {
                var r = _encounters[id];
                if (r.State != EncounterState.Dormant) continue;
                var triggered = false;
                foreach (var p in livingPlayers)
                {
                    if (PlanarMath.Length(new Float2(p.X - r.Definition.Center.X, p.Y - r.Definition.Center.Y)) <= r.Definition.ActivationRadius)
                    {
                        triggered = true;
                        break;
                    }
                }
                if (!triggered) continue;
                r.State = EncounterState.Active;
                var multiplier = HealthMultiplier(r.Definition, playerCount);
                foreach (var spawn in r.Definition.Spawns)
                {
                    if (spawn.MinPlayers > playerCount) continue;
                    r.Expected++;
                    orders.Add(new EncounterSpawnOrder { EncounterId = id, EnemyTypeId = spawn.EnemyTypeId, Position = spawn.Position, HealthMultiplier = multiplier });
                }
            }
            return orders;
        }

        public void RegisterSpawned(int encounterId, ulong actorId)
        {
            Running r;
            if (!_encounters.TryGetValue(encounterId, out r) || r.State != EncounterState.Active) return;
            r.Alive.Add(actorId);
            r.Registered++;
            _memberOf[actorId] = encounterId;
        }

        /// <summary>Feed every ActorDefeated; completes an encounter once its last enemy falls.</summary>
        public void OnActorDefeated(ulong actorId)
        {
            int id;
            if (!_memberOf.TryGetValue(actorId, out id)) return;
            var r = _encounters[id];
            if (!r.Alive.Remove(actorId) || r.State != EncounterState.Active) return;
            if (r.Alive.Count > 0 || r.Registered < r.Expected) return;
            r.State = EncounterState.Completed;
            var handler = Completed;
            if (handler != null) handler(id);
        }
    }
}
