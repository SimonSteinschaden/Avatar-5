using System;
using System.Collections.Generic;
using Avatar5.Contracts;
using Avatar5.Gameplay.Movement;

namespace Avatar5.Gameplay.World
{
    /// <summary>Static description of one world object. Ids are stable per level and safe to save.</summary>
    public sealed class WorldObjectDefinition
    {
        public int Id;
        public WorldObjectKind Kind;
        public Float2 Position;
        /// <summary>Lever: how close an actor must be. Brazier: impact distance. Goal: trigger radius.</summary>
        public float Radius = 2f;
        /// <summary>Gate/Goal: ids that must be On before this opens/completes.</summary>
        public int[] Requires = new int[0];
        /// <summary>Brazier: the element that lights it.</summary>
        public ElementKind Element = ElementKind.Fire;
        /// <summary>Brazier: any element activates it (element shrine), so every class can solve the puzzle alone.</summary>
        public bool AnyElement;
    }

    public sealed class LevelDefinition
    {
        public int LevelId;
        public string Key;
        public readonly List<WorldObjectDefinition> Objects = new List<WorldObjectDefinition>();

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (LevelId <= 0) errors.Add("LevelId must be > 0");
            var ids = new HashSet<int>();
            var goals = 0;
            foreach (var o in Objects)
            {
                if (o.Id <= 0) errors.Add("world object id must be > 0");
                if (!ids.Add(o.Id)) errors.Add("duplicate world object id " + o.Id);
                if (!(o.Radius > 0f)) errors.Add("object " + o.Id + ": radius must be > 0");
                if (o.Kind == WorldObjectKind.Goal) goals++;
                if (o.Kind == WorldObjectKind.Gate && (o.Requires == null || o.Requires.Length == 0))
                    errors.Add("gate " + o.Id + " has no requirements");
            }
            foreach (var o in Objects)
            {
                if (o.Requires == null) continue;
                foreach (var r in o.Requires)
                {
                    if (!ids.Contains(r)) errors.Add("object " + o.Id + " requires unknown id " + r);
                    if (r == o.Id) errors.Add("object " + o.Id + " requires itself");
                }
            }
            if (goals != 1) errors.Add("level needs exactly one goal, found " + goals);
            return errors;
        }
    }

    /// <summary>
    /// Host-authoritative world state of one level: levers, element targets, gates and the goal. Every change is
    /// published once as a <see cref="WorldEvent"/> with an increasing EventId; repeated requests change nothing.
    /// </summary>
    public sealed class WorldStateService : IWorldStateService
    {
        private readonly LevelDefinition _level;
        private readonly Dictionary<int, WorldObjectDefinition> _objects = new Dictionary<int, WorldObjectDefinition>();
        private readonly Dictionary<int, int> _states = new Dictionary<int, int>();
        private readonly List<int> _order = new List<int>();
        private ulong _nextEventId = 1;
        private uint _tick;
        private int _goalId;

        public event Action<WorldEvent> Changed;

        public WorldStateService(LevelDefinition level)
        {
            if (level == null) throw new ArgumentNullException("level");
            var errors = level.Validate();
            if (errors.Count > 0) throw new ArgumentException("Invalid level " + level.Key + ": " + string.Join("; ", errors.ToArray()));
            _level = level;
            foreach (var o in level.Objects)
            {
                _objects.Add(o.Id, o);
                _states.Add(o.Id, WorldStateCodes.Off);
                _order.Add(o.Id);
                if (o.Kind == WorldObjectKind.Goal) _goalId = o.Id;
            }
        }

        public int LevelId { get { return _level.LevelId; } }
        public bool IsCompleted { get; private set; }
        public ulong CompletedBy { get; private set; }
        public uint ServerTick { get { return _tick; } set { _tick = value; } }

        public int GetState(int worldObjectId)
        {
            int state;
            return _states.TryGetValue(worldObjectId, out state) ? state : -1;
        }

        /// <summary>Snapshot (id, state) for late joiners and checkpoints, in level order.</summary>
        public List<KeyValuePair<int, int>> Snapshot()
        {
            var list = new List<KeyValuePair<int, int>>();
            foreach (var id in _order) list.Add(new KeyValuePair<int, int>(id, _states[id]));
            return list;
        }

        /// <summary>Restores a snapshot (checkpoint/rejoin) without raising change events.</summary>
        public void Restore(IEnumerable<KeyValuePair<int, int>> snapshot, bool completed)
        {
            foreach (var pair in snapshot)
            {
                if (_states.ContainsKey(pair.Key)) _states[pair.Key] = pair.Value;
            }
            IsCompleted = completed;
        }

        public RequestStatus TryInteract(ulong actorId, int worldObjectId, Float2 actorPosition)
        {
            WorldObjectDefinition o;
            if (!_objects.TryGetValue(worldObjectId, out o) || o.Kind != WorldObjectKind.Lever) return RequestStatus.RejectedInvalidState;
            if (Distance(actorPosition, o.Position) > o.Radius) return RequestStatus.RejectedRange;
            if (_states[o.Id] == WorldStateCodes.On) return RequestStatus.Duplicate;
            Set(o.Id, WorldStateCodes.On);
            return RequestStatus.Accepted;
        }

        /// <summary>The closest lever in reach of the actor, or 0. Lets the client just press "interact".</summary>
        public int FindInteractable(Float2 actorPosition)
        {
            var best = 0;
            var bestDistance = float.MaxValue;
            foreach (var id in _order)
            {
                var o = _objects[id];
                if (o.Kind != WorldObjectKind.Lever || _states[id] == WorldStateCodes.On) continue;
                var d = Distance(actorPosition, o.Position);
                if (d <= o.Radius && d < bestDistance)
                {
                    bestDistance = d;
                    best = id;
                }
            }
            return best;
        }

        public void ApplyImpact(in ElementImpact impact)
        {
            foreach (var id in _order)
            {
                var o = _objects[id];
                if (o.Kind != WorldObjectKind.Brazier || _states[id] == WorldStateCodes.On) continue;
                if (!o.AnyElement && o.Element != impact.Element) continue;
                if (Distance(impact.Position, o.Position) <= o.Radius + impact.Radius) Set(id, WorldStateCodes.On);
            }
        }

        /// <summary>Host calls this with living players' positions; completes the level once.</summary>
        public void CheckGoal(ulong actorId, Float2 position)
        {
            if (IsCompleted) return;
            var goal = _objects[_goalId];
            if (!RequirementsMet(goal) || Distance(position, goal.Position) > goal.Radius) return;
            IsCompleted = true;
            CompletedBy = actorId;
            Raise(goal.Id, WorldStateCodes.LevelCompletedCode);
        }

        private void Set(int id, int state)
        {
            if (_states[id] == state) return;
            _states[id] = state;
            Raise(id, state);
            // Changing a trigger can open gates, which can in turn satisfy other gates.
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var gateId in _order)
                {
                    var gate = _objects[gateId];
                    if (gate.Kind != WorldObjectKind.Gate || _states[gateId] == WorldStateCodes.On || !RequirementsMet(gate)) continue;
                    _states[gateId] = WorldStateCodes.On;
                    Raise(gateId, WorldStateCodes.On);
                    changed = true;
                }
            }
        }

        private bool RequirementsMet(WorldObjectDefinition o)
        {
            foreach (var r in o.Requires)
            {
                if (_states[r] != WorldStateCodes.On) return false;
            }
            return true;
        }

        private void Raise(int objectId, int state)
        {
            var e = new WorldEvent { EventId = _nextEventId++, ServerTick = _tick, LevelId = _level.LevelId, WorldObjectId = objectId, StateCode = state };
            var handler = Changed;
            if (handler != null) handler(e);
        }

        private static float Distance(Float2 a, Float2 b)
        {
            return PlanarMath.Length(new Float2(a.X - b.X, a.Y - b.Y));
        }
    }
}
