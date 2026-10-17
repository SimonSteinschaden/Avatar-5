using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Avatar5.Contracts;
using Avatar5.Gameplay.Movement;
using Unity.Netcode;
using UnityEngine;

namespace Avatar5.Networking
{
    /// <summary>
    /// Test automation for multi-process runs (no input devices in batchmode).
    /// <c>-a5-bot-move X,Z</c>: the local avatar sends this world-space move intent every frame.
    /// <c>-a5-bot-delay S</c>: waits S seconds after spawning before moving (default 0).
    /// <c>-a5-bot-hijack</c>: additionally tries to drive every foreign avatar (the host must refuse).
    /// <c>-a5-report-players S</c>: logs all avatar positions every S seconds as "A5PLAYER ..." lines.
    /// Only active when those arguments are present; never part of normal play.
    /// </summary>
    public sealed class SessionBot : MonoBehaviour
    {
        private static readonly List<NetworkPlayerAvatar> Avatars = new List<NetworkPlayerAvatar>();
        private static bool? _active;

        private NetworkPlayerAvatar _avatar;
        private PlayerCommandSequencer _sequencer;
        private Float2 _move;
        private bool _hijack;
        private uint _tick;
        private float _startTime;

        public static bool IsActive
        {
            get
            {
                if (!_active.HasValue) _active = HasArg("-a5-bot-move");
                return _active.Value;
            }
        }

        public void Bind(NetworkPlayerAvatar avatar)
        {
            _avatar = avatar;
            _sequencer = new PlayerCommandSequencer(avatar.ActorId);
            string value;
            if (TryArg("-a5-bot-move", out value)) _move = ParseVector(value);
            _hijack = HasArg("-a5-bot-hijack");
            float delay;
            _startTime = Time.unscaledTime + (TryArg("-a5-bot-delay", out value) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out delay) ? delay : 0f);
            // Batchmode runs unthrottled; a realistic frame rate keeps command traffic representative.
            Application.targetFrameRate = 60;
        }

        private void Update()
        {
            if (_avatar == null || !_avatar.IsSpawned) return;
            _tick++;
            var move = Time.unscaledTime >= _startTime ? _move : new Float2(0f, 0f);
            var command = _sequencer.Next(_tick, move, new Float2(0f, 0f), PlayerActionKind.None, 0);
            _avatar.Submit(command);
            if (!_hijack) return;
            foreach (var other in Avatars)
            {
                if (other == null || other == _avatar || !other.IsSpawned) continue;
                // Claims the victim's actor id and a far-ahead sequence; the host must ignore both.
                var forged = command;
                forged.ActorId = other.ActorId;
                forged.SequenceId += 1000000;
                other.SendForeignCommandForTest(forged);
            }
        }

        // ---- reporting -------------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallReporter()
        {
            NetworkPlayerAvatar.SpawnStateChanged -= OnSpawnStateChanged;
            NetworkPlayerAvatar.SpawnStateChanged += OnSpawnStateChanged;
            string value;
            float interval;
            if (!TryArg("-a5-report-players", out value) || !float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out interval)) return;
            var go = new GameObject("A5PlayerReporter");
            DontDestroyOnLoad(go);
            go.AddComponent<Reporter>().Interval = Mathf.Max(0.25f, interval);
        }

        private static void OnSpawnStateChanged(NetworkPlayerAvatar avatar, bool spawned)
        {
            if (spawned) { if (!Avatars.Contains(avatar)) Avatars.Add(avatar); }
            else Avatars.Remove(avatar);
        }

        private sealed class Reporter : MonoBehaviour
        {
            public float Interval = 2f;
            private float _next;

            private void Update()
            {
                if (Time.unscaledTime < _next) return;
                _next = Time.unscaledTime + Interval;
                var manager = NetworkManager.Singleton;
                if (manager == null || !manager.IsListening) return;
                var line = new StringBuilder(NetworkPlayerAvatar.LogTag);
                line.Append(" t=").Append(Time.unscaledTime.ToString("0.0", CultureInfo.InvariantCulture));
                line.Append(" self=").Append(manager.LocalClientId + 1UL);
                line.Append(" count=").Append(Avatars.Count);
                foreach (var avatar in Avatars)
                {
                    if (avatar == null || !avatar.IsSpawned) continue;
                    var p = avatar.transform.position;
                    line.Append(" a").Append(avatar.ActorId).Append('=')
                        .Append(p.x.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                        .Append(p.z.ToString("0.00", CultureInfo.InvariantCulture));
                    if (manager.IsServer)
                    {
                        line.Append("[seq=").Append(avatar.CurrentPose.LastProcessedSequenceId)
                            .Append(",noauth=").Append(avatar.RejectedNoAuthority)
                            .Append(",rate=").Append(avatar.RejectedRateLimited).Append(']');
                    }
                }
                Debug.Log(line.ToString());
            }
        }

        // ---- args ------------------------------------------------------------------------------

        private static Float2 ParseVector(string value)
        {
            var parts = value.Split(',');
            float x, z;
            if (parts.Length == 2
                && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
            {
                return PlanarMath.ClampToUnit(new Float2(x, z));
            }
            return new Float2(0f, 0f);
        }

        private static bool HasArg(string name)
        {
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool TryArg(string name, out string value)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    value = args[i + 1];
                    return true;
                }
            }
            value = null;
            return false;
        }
    }
}
