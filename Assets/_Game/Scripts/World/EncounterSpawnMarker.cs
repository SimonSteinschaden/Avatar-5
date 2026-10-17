using UnityEngine;

namespace Avatar5.World
{
    /// <summary>Spawn position of one enemy inside an encounter (child of a WorldObjectAuthoring of kind Encounter).</summary>
    public sealed class EncounterSpawnMarker : MonoBehaviour
    {
        [SerializeField] private int enemyTypeId = 1;
        [SerializeField, Min(1)] private int minPlayers = 1;

        public int EnemyTypeId { get { return enemyTypeId; } }
        public int MinPlayers { get { return minPlayers; } }

        public void Configure(int typeId, int playersNeeded)
        {
            enemyTypeId = typeId;
            minPlayers = playersNeeded;
        }
    }
}
