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
    /// <c>-a5-bot-cast SLOT:INTERVAL[,SLOT:INTERVAL]</c>: casts the ability slot every INTERVAL seconds at the nearest enemy.
    /// <c>-a5-bot-cast-for S</c>: stops casting S seconds after moving starts (default: never).
    /// <c>-a5-bot-approach D</c>: walks toward the nearest enemy until it is closer than D metres.
    /// <c>-a5-bot-after-move X,Z</c>: once casting time is over, walk in this direction instead (e.g. to leave an enemy's leash).
    /// <c>-a5-bot-target any|brute</c>: which enemies count as targets for aiming and approaching (default any).
    /// <c>-a5-bot-route "W:x:z;I;K;F:x:z;..."</c>: scripted level run (Walk to point, Interact, Kill enemies near, Fire at point).
    /// <c>-a5-bot-hijack</c>: additionally tries to drive every foreign avatar (the host must refuse).
    /// <c>-a5-report-players S</c>: logs all avatar positions every S seconds as "A5PLAYER ..." lines.
    /// Only active when those arguments are present; never part of normal play.
    /// </summary>
    public sealed class SessionBot : MonoBehaviour
    {
        private static readonly List<NetworkPlayerAvatar> Avatars = new List<NetworkPlayerAvatar>();
        private static bool? _active;
        private static readonly Dictionary<ulong, float> DamageReceived = new Dictionary<ulong, float>();
        private static int _eventsSeen;
        private static int _steam;
        private static int _extinguish;
        private static int _freeze;
        private static int _flare;
        private static int _castsAccepted;
        private static int _telegraphs;
        private static int _enemyCasts;
        private static int _enemyHits;
        private static int _enemyDefeats;
        private static int _playerDefeats;
        private bool _onlyBrutes;
        private bool _hasAfterMove;
        private Float2 _afterMove;

        private struct RouteStep { public char Kind; public Float2 Point; }
        private List<RouteStep> _route;
        private int _routeIndex;
        private float _stepTimer;
        private float _nextRouteCast;

        private NetworkPlayerAvatar _avatar;
        private PlayerCommandSequencer _sequencer;
        private Float2 _move;
        private bool _hijack;
        private uint _tick;
        private float _startTime;
        private float _castUntil = float.MaxValue;
        private float _approach;
        private readonly List<int> _castSlots = new List<int>();
        private readonly List<float> _castIntervals = new List<float>();
        private readonly List<float> _nextCast = new List<float>();

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
            if (TryArg("-a5-bot-cast", out value))
            {
                foreach (var part in value.Split(','))
                {
                    var pieces = part.Split(':');
                    int slot;
                    float interval;
                    if (pieces.Length == 2 && int.TryParse(pieces[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out slot)
                        && float.TryParse(pieces[1], NumberStyles.Float, CultureInfo.InvariantCulture, out interval) && interval > 0f)
                    {
                        _castSlots.Add(slot);
                        _castIntervals.Add(interval);
                        _nextCast.Add(_startTime + 0.1f * _castSlots.Count);
                    }
                }
            }
            if (TryArg("-a5-bot-route", out value)) _route = ParseRoute(value);
            if (TryArg("-a5-bot-after-move", out value)) { _afterMove = ParseVector(value); _hasAfterMove = true; }
            _onlyBrutes = TryArg("-a5-bot-target", out value) && string.Equals(value, "brute", StringComparison.OrdinalIgnoreCase);
            float approach;
            if (TryArg("-a5-bot-approach", out value) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out approach)) _approach = approach;
            float castFor;
            if (TryArg("-a5-bot-cast-for", out value) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out castFor)) _castUntil = _startTime + castFor;
        }

        private void Update()
        {
            if (_avatar == null || !_avatar.IsSpawned) return;
            _tick++;
            if (_route != null)
            {
                if (Time.unscaledTime >= _startTime) RouteTick();
                else _avatar.Submit(_sequencer.Next(++_tick, new Float2(0f, 0f), new Float2(0f, 0f), PlayerActionKind.None, 0));
                return;
            }
            var move = Time.unscaledTime >= _startTime ? _move : new Float2(0f, 0f);
            if (_hasAfterMove && Time.unscaledTime >= _castUntil) move = _afterMove;
            else if (_approach > 0f && Time.unscaledTime >= _startTime)
            {
                float distance;
                var toEnemy = AimAtNearestEnemy(out distance);
                move = distance > _approach ? toEnemy : new Float2(0f, 0f);
            }
            var action = PlayerActionKind.None;
            var slotToCast = 0;
            var aim = new Float2(0f, 0f);
            var now = Time.unscaledTime;
            for (var i = 0; i < _castSlots.Count && now < _castUntil; i++)
            {
                if (now < _nextCast[i]) continue;
                _nextCast[i] = now + _castIntervals[i];
                action = PlayerActionKind.Cast;
                slotToCast = _castSlots[i];
                float ignored;
                aim = AimAtNearestEnemy(out ignored);
                break;
            }
            var command = _sequencer.Next(_tick, move, aim, action, slotToCast);
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

        private void RouteTick()
        {
            _tick++;
            var move = new Float2(0f, 0f);
            var aim = new Float2(0f, 0f);
            var action = PlayerActionKind.None;
            var slot = 0;
            var self = new Float2(_avatar.transform.position.x, _avatar.transform.position.z);
            if (_routeIndex >= _route.Count)
            {
                _avatar.Submit(_sequencer.Next(_tick, move, aim, action, slot));
                return;
            }
            var step = _route[_routeIndex];
            _stepTimer += Time.deltaTime;
            var done = false;
            switch (step.Kind)
            {
                case 'W':
                    var offset = new Float2(step.Point.X - self.X, step.Point.Y - self.Y);
                    var length = Mathf.Sqrt(offset.X * offset.X + offset.Y * offset.Y);
                    if (length < 0.6f) done = true;
                    else move = new Float2(offset.X / length, offset.Y / length);
                    break;
                case 'I':
                    action = PlayerActionKind.Interact;
                    done = true;
                    break;
                case 'F':
                    if (Time.unscaledTime >= _nextRouteCast)
                    {
                        var to = new Float2(step.Point.X - self.X, step.Point.Y - self.Y);
                        var l = Mathf.Max(1e-3f, Mathf.Sqrt(to.X * to.X + to.Y * to.Y));
                        aim = new Float2(to.X / l, to.Y / l);
                        action = PlayerActionKind.Cast;
                        _nextRouteCast = Time.unscaledTime + 1f;
                        done = true;
                    }
                    break;
                case 'K':
                    float distance;
                    var toEnemy = AimAtNearestEnemy(out distance);
                    if (distance > 14f) { done = _stepTimer > 1f; break; }
                    _stepTimer = 0f;
                    move = distance > 3.5f ? toEnemy : new Float2(0f, 0f);
                    if (distance <= 9f && Time.unscaledTime >= _nextRouteCast)
                    {
                        aim = toEnemy;
                        action = PlayerActionKind.Cast;
                        slot = (_tick % 3 == 0) ? 1 : 0;
                        _nextRouteCast = Time.unscaledTime + 0.9f;
                    }
                    break;
            }
            _avatar.Submit(_sequencer.Next(_tick, move, aim, action, slot));
            if (!done) return;
            _routeIndex++;
            _stepTimer = 0f;
            Debug.Log("A5ROUTE step=" + _routeIndex + "/" + _route.Count + (_routeIndex >= _route.Count ? " done" : string.Empty)
                + " at=" + self.X.ToString("0.0", CultureInfo.InvariantCulture) + "," + self.Y.ToString("0.0", CultureInfo.InvariantCulture));
        }

        private static List<RouteStep> ParseRoute(string value)
        {
            var steps = new List<RouteStep>();
            foreach (var raw in value.Split(';'))
            {
                var parts = raw.Trim().Split(':');
                if (parts.Length == 0 || parts[0].Length == 0) continue;
                var step = new RouteStep { Kind = char.ToUpperInvariant(parts[0][0]) };
                float x, z;
                if (parts.Length == 3 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                    && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                    step.Point = new Float2(x, z);
                steps.Add(step);
            }
            return steps;
        }

        private Float2 AimAtNearestEnemy(out float distance)
        {
            var best = float.MaxValue;
            distance = float.MaxValue;
            var aim = new Float2(0f, 0f);
            foreach (var actor in FindObjectsByType<NetworkCombatActor>(FindObjectsSortMode.None))
            {
                if (actor.Team != TeamKind.Enemies) continue;
                if (_onlyBrutes && actor.GetComponent<EnemyBrainHost>() == null) continue;
                if (actor.CombatState.Defeated) continue;
                var offset = actor.transform.position - _avatar.transform.position;
                offset.y = 0f;
                var d = offset.sqrMagnitude;
                if (d < best && d > 1e-4f)
                {
                    best = d;
                    distance = Mathf.Sqrt(d);
                    offset.Normalize();
                    aim = new Float2(offset.x, offset.z);
                }
            }
            return aim;
        }

        // ---- reporting -------------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallReporter()
        {
            NetworkPlayerAvatar.SpawnStateChanged -= OnSpawnStateChanged;
            NetworkPlayerAvatar.SpawnStateChanged += OnSpawnStateChanged;
            Avatar5.Foundation.PresentationBus.CombatEventRaised -= OnCombatEvent;
            Avatar5.Foundation.PresentationBus.CombatEventRaised += OnCombatEvent;
            string value;
            float interval;
            if (!TryArg("-a5-report-players", out value) || !float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out interval)) return;
            var go = new GameObject("A5PlayerReporter");
            DontDestroyOnLoad(go);
            go.AddComponent<Reporter>().Interval = Mathf.Max(0.25f, interval);
        }

        private static void OnCombatEvent(CombatEvent e)
        {
            _eventsSeen++;
            if (e.ResultKind == (int)CombatEventKind.DamageApplied)
            {
                float total;
                DamageReceived.TryGetValue(e.TargetActorId, out total);
                DamageReceived[e.TargetActorId] = total + e.Amount;
            }
            else if (e.ResultKind == (int)CombatEventKind.ReactionTriggered)
            {
                if (e.EffectId == (int)ReactionKind.Steam) _steam++;
                else if (e.EffectId == (int)ReactionKind.Extinguish) _extinguish++;
                else if (e.EffectId == (int)ReactionKind.Freeze) _freeze++;
                else if (e.EffectId == (int)ReactionKind.Flare) _flare++;
            }
            else if (e.ResultKind == (int)CombatEventKind.AbilityCastAccepted)
            {
                _castsAccepted++;
                if (e.SourceActorId >= NetworkCombatActor.NonPlayerIdBase) _enemyCasts++;
            }
            else if (e.ResultKind == (int)CombatEventKind.TelegraphStarted) _telegraphs++;
            else if (e.ResultKind == (int)CombatEventKind.ActorDefeated)
            {
                if (e.TargetActorId >= NetworkCombatActor.NonPlayerIdBase) _enemyDefeats++;
                else _playerDefeats++;
            }
            if (e.ResultKind == (int)CombatEventKind.DamageApplied && e.SourceActorId >= NetworkCombatActor.NonPlayerIdBase) _enemyHits++;
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
                Debug.Log(CombatLine(manager));
                if (WorldHost.Instance != null && WorldHost.Instance.LevelId > 0) Debug.Log(WorldLine());
            }
        }

        private static string WorldLine()
        {
            var world = WorldHost.Instance;
            var line = new StringBuilder("A5WORLD");
            line.Append(" t=").Append(Time.unscaledTime.ToString("0.0", CultureInfo.InvariantCulture));
            line.Append(" level=").Append(world.LevelId).Append(" completed=").Append(world.IsCompleted ? 1 : 0);
            var ids = new List<int>(world.States.Keys);
            ids.Sort();
            line.Append(" states=");
            for (var i = 0; i < ids.Count; i++) line.Append(i == 0 ? "" : ",").Append(ids[i]).Append(':').Append(world.States[ids[i]]);
            return line.ToString();
        }

        private static string CombatLine(NetworkManager manager)
        {
            var line = new StringBuilder("A5COMBAT");
            line.Append(" t=").Append(Time.unscaledTime.ToString("0.0", CultureInfo.InvariantCulture));
            line.Append(" self=").Append(manager.LocalClientId + 1UL);
            line.Append(" events=").Append(_eventsSeen).Append(" casts=").Append(_castsAccepted);
            line.Append(" steam=").Append(_steam).Append(" extinguish=").Append(_extinguish)
                .Append(" freeze=").Append(_freeze).Append(" flare=").Append(_flare);
            line.Append(" dupes=").Append(CombatHost.Instance != null ? CombatHost.Instance.DuplicateEventsDropped : -1);
            line.Append(" telegraphs=").Append(_telegraphs).Append(" enemyCasts=").Append(_enemyCasts).Append(" enemyHits=").Append(_enemyHits);
            line.Append(" enemyDefeats=").Append(_enemyDefeats).Append(" playerDefeats=").Append(_playerDefeats);
            foreach (var actor in FindObjectsByType<NetworkCombatActor>(FindObjectsSortMode.None))
            {
                if (!actor.IsSpawned) continue;
                var state = actor.CombatState;
                if (actor.Team == TeamKind.Players)
                {
                    line.Append(" p").Append(actor.ActorId).Append('=').Append(state.Health.ToString("0.0", CultureInfo.InvariantCulture));
                    continue;
                }
                float dealt;
                DamageReceived.TryGetValue(actor.ActorId, out dealt);
                line.Append(" e").Append(actor.ActorId).Append('=')
                    .Append(state.Health.ToString("0.0", CultureInfo.InvariantCulture)).Append('/')
                    .Append(state.MaxHealth.ToString("0", CultureInfo.InvariantCulture)).Append("|dmg=")
                    .Append(dealt.ToString("0.0", CultureInfo.InvariantCulture)).Append("|st=").Append((int)state.Status);
            }
            return line.ToString();
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
