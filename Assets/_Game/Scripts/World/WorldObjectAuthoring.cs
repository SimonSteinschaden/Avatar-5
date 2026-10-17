using Avatar5.Contracts;
using Avatar5.Gameplay.World;
using UnityEngine;
using UnityEngine.AI;

namespace Avatar5.World
{
    /// <summary>
    /// Scene-placed world object (lever, gate, brazier, goal) with a stable id. Describes the object for the host's
    /// <see cref="WorldStateService"/> and shows the replicated state on every peer. Never decides state itself.
    /// </summary>
    public sealed class WorldObjectAuthoring : MonoBehaviour
    {
        [SerializeField] private int id;
        [SerializeField] private WorldObjectKind kind;
        [SerializeField, Min(0.1f)] private float radius = 2f;
        [SerializeField] private int[] requires = new int[0];
        [SerializeField] private ElementKind element = ElementKind.Fire;
        [Tooltip("Lever handle / gate leaf / flame that changes with the state.")]
        [SerializeField] private Transform animated;

        private int _state = -1;

        public int Id { get { return id; } }
        public WorldObjectKind Kind { get { return kind; } }
        public int State { get { return _state < 0 ? WorldStateCodes.Off : _state; } }

        public void Configure(int objectId, WorldObjectKind objectKind, float objectRadius, int[] requirements, Transform animatedPart, ElementKind objectElement = ElementKind.Fire)
        {
            id = objectId;
            kind = objectKind;
            radius = objectRadius;
            requires = requirements ?? new int[0];
            animated = animatedPart;
            element = objectElement;
        }

        public WorldObjectDefinition ToDefinition()
        {
            var p = transform.position;
            return new WorldObjectDefinition
            {
                Id = id, Kind = kind, Position = new Float2(p.x, p.z), Radius = radius, Requires = (int[])requires.Clone(), Element = element
            };
        }

        private void Awake()
        {
            Apply(WorldStateCodes.Off);
        }

        /// <summary>Shows a state; gates also stop blocking movement and navigation once open.</summary>
        public void Apply(int state)
        {
            if (state == _state) return;
            _state = state;
            var on = state == WorldStateCodes.On;
            switch (kind)
            {
                case WorldObjectKind.Gate:
                    foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = !on;
                    var obstacle = GetComponentInChildren<NavMeshObstacle>();
                    if (obstacle != null) obstacle.enabled = !on;
                    if (animated != null) animated.localPosition = on ? new Vector3(0f, -2.2f, 0f) : Vector3.zero;
                    break;
                case WorldObjectKind.Lever:
                    if (animated != null) animated.localRotation = Quaternion.Euler(on ? -50f : 50f, 0f, 0f);
                    break;
                case WorldObjectKind.Brazier:
                    if (animated != null) animated.gameObject.SetActive(on);
                    break;
            }
        }
    }
}
