using System.Collections.Generic;
using Avatar5.Gameplay.Session;
using Avatar5.Player;
using Unity.Netcode;
using UnityEngine;

namespace Avatar5.Networking
{
    /// <summary>
    /// Host-only: spawns one <see cref="NetworkPlayerAvatar"/> per approved connection at a free spawn point,
    /// owned by that connection. NGO despawns player objects when their connection leaves.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class NetworkPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private Transform[] spawnPoints = new Transform[0];

        private readonly Dictionary<ulong, int> _slotByClient = new Dictionary<ulong, int>();
        private NetworkManager _manager;

        public NetworkObject PlayerPrefab { get { return playerPrefab; } }

        public void Configure(NetworkObject prefab, Transform[] points)
        {
            playerPrefab = prefab;
            spawnPoints = points ?? new Transform[0];
        }

        private void Awake()
        {
            _manager = GetComponent<NetworkManager>();
        }

        private void OnEnable()
        {
            if (_manager == null) _manager = GetComponent<NetworkManager>();
            _manager.OnConnectionEvent += OnConnectionEvent;
            _manager.OnServerStopped += OnServerStopped;
        }

        private void OnDisable()
        {
            if (_manager == null) return;
            _manager.OnConnectionEvent -= OnConnectionEvent;
            _manager.OnServerStopped -= OnServerStopped;
        }

        private void OnConnectionEvent(NetworkManager manager, ConnectionEventData data)
        {
            if (!manager.IsServer) return;
            if (data.EventType == ConnectionEvent.ClientConnected) Spawn(data.ClientId);
            else if (data.EventType == ConnectionEvent.ClientDisconnected) _slotByClient.Remove(data.ClientId);
        }

        private void OnServerStopped(bool wasHost)
        {
            _slotByClient.Clear();
        }

        private void Spawn(ulong clientId)
        {
            if (playerPrefab == null || _slotByClient.ContainsKey(clientId)) return;
            var slot = FreeSlot();
            _slotByClient[clientId] = slot;

            Vector3 position;
            Quaternion rotation;
            SpawnPose(slot, out position, out rotation);
            var instance = Instantiate(playerPrefab, position, rotation);
            var respawn = instance.GetComponent<FallRespawn>();
            if (respawn != null && slot < spawnPoints.Length && spawnPoints[slot] != null) respawn.Configure(spawnPoints[slot], -10f);
            instance.SpawnAsPlayerObject(clientId, true);
            Debug.Log("[Avatar5.Session] Spawned actor " + ActorIds.ForClient(clientId) + " in slot " + slot);
        }

        private int FreeSlot()
        {
            var used = new HashSet<int>(_slotByClient.Values);
            for (var i = 0; ; i++)
            {
                if (!used.Contains(i)) return i;
            }
        }

        private void SpawnPose(int slot, out Vector3 position, out Quaternion rotation)
        {
            if (spawnPoints.Length > 0)
            {
                var point = spawnPoints[slot % spawnPoints.Length];
                if (point != null)
                {
                    // More players than points: offset sideways so nobody spawns inside another capsule.
                    var lap = slot / spawnPoints.Length;
                    position = point.position + point.right * (1.5f * lap);
                    rotation = point.rotation;
                    return;
                }
            }
            position = new Vector3(2f * slot, 0f, 0f);
            rotation = Quaternion.identity;
        }
    }
}
