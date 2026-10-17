using System;
using Avatar5.Contracts;
using Avatar5.Foundation;
using Avatar5.Gameplay.Session;
using Avatar5.Input;
using Avatar5.Player;
using Avatar5.Presentation;
using Unity.Netcode;
using UnityEngine;

namespace Avatar5.Networking
{
    /// <summary>Class the local player picked in the lobby (or via -a5-class fire|water|earth|air).</summary>
    public static class PlayerClassSettings
    {
        public static ElementKind Selected = ElementKind.Fire;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ReadCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                ElementKind parsed;
                if (args[i] == "-a5-class" && Enum.TryParse(args[i + 1], true, out parsed)) Selected = parsed;
            }
        }
    }

    /// <summary>Wire format of a <see cref="PlayerCommand"/>. ActorId is not sent: the host derives it from the sender.</summary>
    public struct NetPlayerCommand : INetworkSerializable
    {
        public uint SequenceId;
        public uint ClientTick;
        public Vector2 Move;
        public Vector2 Aim;
        public byte Action;
        public int ActionRefId;

        public NetPlayerCommand(in PlayerCommand command)
        {
            SequenceId = command.SequenceId;
            ClientTick = command.ClientTick;
            Move = new Vector2(command.Move.X, command.Move.Y);
            Aim = new Vector2(command.Aim.X, command.Aim.Y);
            Action = (byte)command.Action;
            ActionRefId = command.ActionRefId;
        }

        public PlayerCommand ToCommand(ulong actorId)
        {
            return new PlayerCommand
            {
                ActorId = actorId,
                SequenceId = SequenceId,
                ClientTick = ClientTick,
                Move = new Float2(Move.x, Move.y),
                Aim = new Float2(Aim.x, Aim.y),
                Action = Enum.IsDefined(typeof(PlayerActionKind), Action) ? (PlayerActionKind)Action : PlayerActionKind.None,
                ActionRefId = ActionRefId
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SequenceId);
            serializer.SerializeValue(ref ClientTick);
            serializer.SerializeValue(ref Move);
            serializer.SerializeValue(ref Aim);
            serializer.SerializeValue(ref Action);
            serializer.SerializeValue(ref ActionRefId);
        }
    }

    /// <summary>Host-written pose extras that the transform alone does not carry.</summary>
    public struct NetPoseExtras : INetworkSerializable, IEquatable<NetPoseExtras>
    {
        public uint LastProcessedSequenceId;
        public Vector2 Aim;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref LastProcessedSequenceId);
            serializer.SerializeValue(ref Aim);
        }

        public bool Equals(NetPoseExtras other)
        {
            return LastProcessedSequenceId == other.LastProcessedSequenceId && Aim == other.Aim;
        }
    }

    /// <summary>
    /// Networked player actor. The host owns the simulation: it runs the <see cref="CharacterMotor"/>, the
    /// NetworkTransform (server authority) replicates the result. The owning client only sends intents
    /// (<see cref="PlayerCommand"/>) through this sink; other clients just watch the interpolated transform.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public sealed class NetworkPlayerAvatar : NetworkBehaviour, IPlayerCommandSink, IPlayerPoseSource
    {
        public const string LogTag = "A5PLAYER";

        private readonly NetworkVariable<NetPoseExtras> _extras = new NetworkVariable<NetPoseExtras>();
        private readonly CommandSendThrottle _throttle = new CommandSendThrottle();

        private CharacterMotor _motor;
        private HostCommandIngress _ingress;
        private Vector3 _lastPosition;
        private Vector3 _estimatedVelocity;

        public ulong ActorId { get { return ActorIds.ForClient(OwnerClientId); } }
        public int RejectedNoAuthority { get { return _ingress != null ? _ingress.RejectedNoAuthority : 0; } }
        public int RejectedRateLimited { get { return _ingress != null ? _ingress.RejectedRateLimited : 0; } }

        /// <summary>Raised on every peer when an avatar spawns or despawns (lists, HUD, tests).</summary>
        public static event Action<NetworkPlayerAvatar, bool> SpawnStateChanged;

        public PlayerPose CurrentPose
        {
            get
            {
                if (IsServer) return _motor.CurrentPose;
                var extras = _extras.Value;
                var yaw = transform.eulerAngles.y;
                return new PlayerPose
                {
                    ActorId = ActorId,
                    LastProcessedSequenceId = extras.LastProcessedSequenceId,
                    Position = transform.position.ToFloat3(),
                    Velocity = _estimatedVelocity.ToFloat3(),
                    FacingYawDegrees = yaw,
                    AimDirection = new Float2(extras.Aim.x, extras.Aim.y)
                };
            }
        }

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
        }

        public override void OnNetworkSpawn()
        {
            var controller = GetComponent<CharacterController>();
            var respawn = GetComponent<FallRespawn>();
            if (IsServer)
            {
                _motor.Configure(MovementDefinitionOf(_motor), ActorId);
                _motor.SimulateInUpdate = true;
                _ingress = new HostCommandIngress(OwnerClientId);
            }
            else
            {
                // Clients never simulate; the replicated transform is the truth.
                _motor.SimulateInUpdate = false;
                if (controller != null) controller.enabled = false;
                if (respawn != null) respawn.enabled = false;
            }

            var adapter = GetComponent<PlayerInputAdapter>();
            if (adapter != null)
            {
                adapter.enabled = IsOwner && !SessionBot.IsActive;
                if (IsOwner) AttachLocalControls(adapter);
            }
            if (IsOwner && SessionBot.IsActive) gameObject.AddComponent<SessionBot>().Bind(this);
            var combatActor = GetComponent<NetworkCombatActor>();
            if (combatActor != null)
            {
                combatActor.ElementChanged += ApplyClassColour;
                ApplyClassColour(combatActor.Element);
                if (IsOwner) SelectClassRpc((byte)PlayerClassSettings.Selected);
            }

            var aim = GetComponent<AimIndicatorView>();
            if (aim != null) aim.Configure(this, aim.Marker);

            _lastPosition = transform.position;
            gameObject.name = "NetworkPlayer_" + ActorId + (IsOwner ? "_local" : string.Empty);
            if (SpawnStateChanged != null) SpawnStateChanged(this, true);
        }

        public override void OnNetworkDespawn()
        {
            if (SpawnStateChanged != null) SpawnStateChanged(this, false);
        }

        private void AttachLocalControls(PlayerInputAdapter adapter)
        {
            var rig = FindAnyObjectByType<IsometricCameraRig>();
            var cam = Camera.main;
            adapter.Configure(adapter.Actions, this, transform, cam, ActorId, rig);
            if (rig != null) rig.Target = transform;
            var hud = FindAnyObjectByType<MovementDebugHud>();
            if (hud != null) hud.Configure(this);
            var feedback = FindAnyObjectByType<CombatFeedbackView>();
            if (feedback != null)
            {
                var actor = GetComponent<NetworkCombatActor>();
                feedback.SetLocalPlayer(actor);
                feedback.SetLocalElement(PlayerClassSettings.Selected);
            }
        }

        /// <summary>Local intent from the owner's input adapter (or bot). Forwards to the host.</summary>
        public RequestStatus Submit(in PlayerCommand command)
        {
            if (!IsSpawned || !IsOwner) return RequestStatus.RejectedNoAuthority;
            if (!_throttle.ShouldSend(command, Time.unscaledTimeAsDouble)) return RequestStatus.Accepted;
            if (IsServer) return HostReceive(NetworkManager.LocalClientId, new NetPlayerCommand(command));
            SubmitCommandRpc(new NetPlayerCommand(command));
            return RequestStatus.Accepted;
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void SelectClassRpc(byte element, RpcParams rpcParams = default)
        {
            // Only the owner chooses, only once per spawn, and only a real element.
            if (rpcParams.Receive.SenderClientId != OwnerClientId || _classChosen || !Enum.IsDefined(typeof(ElementKind), element)) return;
            if (CombatHost.Instance != null && CombatHost.Instance.SelectClass(GetComponent<NetworkCombatActor>(), (ElementKind)element))
            {
                _classChosen = true;
                Debug.Log("[Avatar5.Session] Actor " + ActorId + " plays " + (ElementKind)element);
            }
        }

        private bool _classChosen;

        private void ApplyClassColour(ElementKind element)
        {
            var bar = GetComponent<HealthBarView>();
            if (bar == null) return;
            switch (element)
            {
                case ElementKind.Water: bar.SetBaseColor(new Color(0.2f, 0.45f, 0.95f)); break;
                case ElementKind.Earth: bar.SetBaseColor(new Color(0.45f, 0.65f, 0.25f)); break;
                case ElementKind.Air: bar.SetBaseColor(new Color(0.85f, 0.85f, 0.9f)); break;
                default: bar.SetBaseColor(new Color(0.9f, 0.35f, 0.15f)); break;
            }
        }

        /// <summary>Test hook: lets a client try to drive an avatar it does not own (host must refuse).</summary>
        public void SendForeignCommandForTest(in PlayerCommand command)
        {
            SubmitCommandRpc(new NetPlayerCommand(command));
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void SubmitCommandRpc(NetPlayerCommand command, RpcParams rpcParams = default)
        {
            HostReceive(rpcParams.Receive.SenderClientId, command);
        }

        private RequestStatus HostReceive(ulong sender, NetPlayerCommand wire)
        {
            var command = wire.ToCommand(0);
            var status = _ingress.Admit(sender, ref command, Time.unscaledTimeAsDouble);
            if (status != RequestStatus.Accepted) return status;
            // Defeated or frozen players keep their sequence moving but neither walk nor act.
            if (CombatHost.Instance != null && (CombatHost.Instance.IsDefeated(ActorId) || CombatHost.Instance.IsFrozen(ActorId)))
            {
                command.Move = new Float2(0f, 0f);
                command.Action = PlayerActionKind.None;
            }
            status = _motor.Submit(command);
            // Movement and casting share the command; the cast only runs once the motor accepted the sequence.
            if (status == RequestStatus.Accepted && command.Action == PlayerActionKind.Cast && CombatHost.Instance != null)
                CombatHost.Instance.HandleCastCommand(ActorId, command, transform);
            if (status == RequestStatus.Accepted && command.Action == PlayerActionKind.Interact && WorldHost.Instance != null)
                WorldHost.Instance.HandleInteract(ActorId, transform.position);
            return status;
        }

        private void LateUpdate()
        {
            if (!IsSpawned) return;
            var dt = Time.deltaTime;
            if (dt > 0f) _estimatedVelocity = (transform.position - _lastPosition) / dt;
            _lastPosition = transform.position;

            if (!IsServer) return;
            var pose = _motor.CurrentPose;
            var extras = new NetPoseExtras
            {
                LastProcessedSequenceId = pose.LastProcessedSequenceId,
                Aim = new Vector2(pose.AimDirection.X, pose.AimDirection.Y)
            };
            if (!extras.Equals(_extras.Value)) _extras.Value = extras;
        }

        private static PlayerMovementDefinition MovementDefinitionOf(CharacterMotor motor)
        {
            return motor.Definition;
        }
    }
}
