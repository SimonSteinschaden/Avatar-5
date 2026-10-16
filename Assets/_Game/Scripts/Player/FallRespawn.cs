using UnityEngine;

namespace Avatar5.Player
{
    /// <summary>Greybox safety net: actors falling below the kill height respawn at the spawn point.</summary>
    [RequireComponent(typeof(CharacterMotor))]
    public sealed class FallRespawn : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float killHeight = -10f;

        private CharacterMotor _motor;

        public void Configure(Transform spawn, float minHeight)
        {
            spawnPoint = spawn;
            killHeight = minHeight;
        }

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
        }

        private void LateUpdate()
        {
            if (spawnPoint == null || transform.position.y >= killHeight) return;
            _motor.Teleport(spawnPoint.position, spawnPoint.eulerAngles.y);
        }
    }
}
