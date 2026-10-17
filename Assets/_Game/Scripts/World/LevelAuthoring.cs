using System.Collections.Generic;
using Avatar5.Gameplay.World;
using UnityEngine;

namespace Avatar5.World
{
    /// <summary>Scene root of a level: id plus all <see cref="WorldObjectAuthoring"/> below it.</summary>
    public sealed class LevelAuthoring : MonoBehaviour
    {
        [SerializeField] private int levelId = 1;
        [SerializeField] private string levelKey = "level01";

        private readonly Dictionary<int, WorldObjectAuthoring> _byId = new Dictionary<int, WorldObjectAuthoring>();

        public int LevelId { get { return levelId; } }

        public void Configure(int id, string key)
        {
            levelId = id;
            levelKey = key;
        }

        public LevelDefinition BuildDefinition()
        {
            var level = new LevelDefinition { LevelId = levelId, Key = levelKey };
            foreach (var o in GetComponentsInChildren<WorldObjectAuthoring>(true)) level.Objects.Add(o.ToDefinition());
            return level;
        }

        public void Apply(int objectId, int state)
        {
            if (_byId.Count == 0)
            {
                foreach (var o in GetComponentsInChildren<WorldObjectAuthoring>(true)) _byId[o.Id] = o;
            }
            WorldObjectAuthoring target;
            if (_byId.TryGetValue(objectId, out target)) target.Apply(state);
        }
    }
}
