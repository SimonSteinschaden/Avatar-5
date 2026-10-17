using Avatar5.Contracts;
using Avatar5.Foundation;
using Avatar5.Gameplay.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Avatar5.Input
{
    public enum AimSource : byte { None, Pointer, Stick }

    /// <summary>
    /// One local person's devices → camera-relative <see cref="PlayerCommand"/> intents.
    /// Knows the camera (for yaw and pointer rays) but not the motor implementation: commands go
    /// to an <see cref="IPlayerCommandSink"/>, which is the local motor in P01 and a network sink in P03.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class PlayerInputAdapter : MonoBehaviour
    {
        public const string GameplayMapName = "Gameplay";

        [SerializeField] private InputActionAsset actions;
        [SerializeField] private MonoBehaviour commandSinkBehaviour;
        [SerializeField] private Transform actor;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private IsometricCameraRig cameraRig;
        [SerializeField] private ulong actorId = 1;
        [SerializeField, Range(0f, 0.9f)] private float stickDeadZone = 0.2f;
        [SerializeField, Min(0f)] private float pointerMinAimDistance = 0.15f;
        [SerializeField, Min(0f)] private float pointerMoveThresholdPixels = 0.5f;

        private InputActionAsset _runtimeActions;
        private InputAction _move;
        private InputAction _aimPointer;
        private InputAction _aimStick;
        private InputAction _cast;
        private InputAction _interact;
        private InputAction _dodge;
        private InputAction _pause;
        private InputAction _zoom;

        private IPlayerCommandSink _sink;
        private PlayerCommandSequencer _sequencer;
        private readonly ClientTickClock _clock = new ClientTickClock();
        private Vector2 _lastPointerPosition;
        private bool _hasPointerPosition;
        private bool _hasFocus = true;

        public AimSource CurrentAimSource { get; private set; }
        public Float2 CurrentAim { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public bool IsPaused { get; private set; }
        public PlayerCommand LastCommand { get; private set; }
        public RequestStatus LastStatus { get; private set; }
        public InputActionAsset RuntimeActions { get { return _runtimeActions; } }
        public InputActionAsset Actions { get { return actions; } }

        public IPlayerCommandSink CommandSink
        {
            get { return _sink; }
            set { _sink = value; }
        }

        public void Configure(InputActionAsset inputActions, IPlayerCommandSink sink, Transform actorTransform,
            Camera cameraForAim, ulong id, IsometricCameraRig rig = null)
        {
            DisableActions();
            actions = inputActions;
            _sink = sink;
            actor = actorTransform;
            viewCamera = cameraForAim;
            cameraRig = rig;
            actorId = id;
            _sequencer = new PlayerCommandSequencer(actorId);
            BindActions();
            if (isActiveAndEnabled) EnableActions();
        }

        /// <summary>Rebinding contract (UI in P07): persist and restore user overrides as JSON.</summary>
        public string SaveBindingOverridesAsJson()
        {
            return _runtimeActions != null ? _runtimeActions.SaveBindingOverridesAsJson() : string.Empty;
        }

        public void LoadBindingOverridesFromJson(string json)
        {
            if (_runtimeActions != null && !string.IsNullOrEmpty(json)) _runtimeActions.LoadBindingOverridesFromJson(json);
        }

        /// <summary>Samples devices once and submits one command. Called every frame; public for tests.</summary>
        public PlayerCommand Tick(float deltaTime)
        {
            if (_sequencer == null) _sequencer = new PlayerCommandSequencer(actorId);
            var tick = _clock.Advance(deltaTime);

            if (_pause != null && _pause.WasPressedThisFrame()) IsPaused = !IsPaused;

            var yaw = CameraYaw();
            var move = new Float2(0f, 0f);
            var action = PlayerActionKind.None;

            if (!IsPaused && _hasFocus)
            {
                if (_move != null) move = InputIntentMath.CameraRelativeMove(_move.ReadValue<Vector2>().ToFloat2(), yaw, stickDeadZone);
                UpdateAim(yaw);
                action = ReadAction();
                if (_zoom != null && cameraRig != null)
                {
                    var zoom = _zoom.ReadValue<float>();
                    if (!Mathf.Approximately(zoom, 0f)) cameraRig.AddZoom(Mathf.Sign(zoom));
                }
            }

            // ActionRefId = ability slot; always slot 0 until P02 defines the loadout.
            var command = _sequencer.Next(tick, move, CurrentAim, action, 0);
            LastCommand = command;
            LastStatus = _sink != null ? _sink.Submit(command) : RequestStatus.RejectedInvalidState;
            return command;
        }

        private void UpdateAim(float cameraYaw)
        {
            var stickAim = _aimStick != null
                ? InputIntentMath.CameraRelativeAim(_aimStick.ReadValue<Vector2>().ToFloat2(), cameraYaw, stickDeadZone)
                : new Float2(0f, 0f);

            if (!PlanarMath.IsZero(stickAim))
            {
                CurrentAimSource = AimSource.Stick;
                CurrentAim = stickAim;
                if (actor != null) AimPoint = actor.position + stickAim.ToWorldXZ() * 3f;
                return;
            }

            if (_aimPointer == null) return;
            var pointer = _aimPointer.ReadValue<Vector2>();
            // The first sample only establishes a baseline; aim switches to the mouse once it actually moves.
            var pointerMoved = _hasPointerPosition && (pointer - _lastPointerPosition).sqrMagnitude
                > pointerMoveThresholdPixels * pointerMoveThresholdPixels;
            _lastPointerPosition = pointer;
            _hasPointerPosition = true;

            // A released stick keeps its last direction until the mouse is actually moved again.
            if (CurrentAimSource == AimSource.Stick && !pointerMoved) return;
            if (CurrentAimSource != AimSource.Pointer && !pointerMoved) return;

            Float2 pointerAim;
            Vector3 point;
            if (TryPointerAim(pointer, out pointerAim, out point))
            {
                CurrentAimSource = AimSource.Pointer;
                AimPoint = point;
                if (!PlanarMath.IsZero(pointerAim)) CurrentAim = pointerAim;
            }
        }

        /// <summary>Projects a screen position onto the ground plane at actor height. False if outside the view.</summary>
        public bool TryPointerAim(Vector2 screenPosition, out Float2 aim, out Vector3 worldPoint)
        {
            aim = new Float2(0f, 0f);
            worldPoint = Vector3.zero;
            if (viewCamera == null || actor == null) return false;
            if (!viewCamera.pixelRect.Contains(screenPosition)) return false;

            var ray = viewCamera.ScreenPointToRay(screenPosition);
            Float3 hit;
            if (!InputIntentMath.TryProjectRayOnGround(ray.origin.ToFloat3(), ray.direction.ToFloat3(), actor.position.y, out hit))
                return false;

            worldPoint = hit.ToVector3();
            aim = InputIntentMath.AimDirection(actor.position.ToFloat3(), hit, pointerMinAimDistance);
            return true;
        }

        private PlayerActionKind ReadAction()
        {
            if (_dodge != null && _dodge.WasPressedThisFrame()) return PlayerActionKind.Dodge;
            if (_cast != null && _cast.WasPressedThisFrame()) return PlayerActionKind.Cast;
            if (_interact != null && _interact.WasPressedThisFrame()) return PlayerActionKind.Interact;
            return PlayerActionKind.None;
        }

        private float CameraYaw()
        {
            return viewCamera != null ? viewCamera.transform.eulerAngles.y : 0f;
        }

        private void BindActions()
        {
            if (_runtimeActions != null) Destroy(_runtimeActions);
            _runtimeActions = null;
            _move = _aimPointer = _aimStick = _cast = _interact = _dodge = _pause = _zoom = null;
            if (actions == null) return;

            // Own copy per local person so enable state and rebinding never leak between adapters.
            _runtimeActions = Instantiate(actions);
            var map = _runtimeActions.FindActionMap(GameplayMapName, true);
            _move = map.FindAction("Move", true);
            _aimPointer = map.FindAction("AimPointer", true);
            _aimStick = map.FindAction("AimStick", true);
            _cast = map.FindAction("Cast", true);
            _interact = map.FindAction("Interact", true);
            _dodge = map.FindAction("Dodge", true);
            _pause = map.FindAction("Pause", true);
            _zoom = map.FindAction("Zoom", false);
        }

        private void EnableActions()
        {
            if (_runtimeActions != null) _runtimeActions.FindActionMap(GameplayMapName, true).Enable();
        }

        private void DisableActions()
        {
            if (_runtimeActions != null) _runtimeActions.FindActionMap(GameplayMapName, true).Disable();
        }

        private void Awake()
        {
            if (_sink == null) _sink = commandSinkBehaviour as IPlayerCommandSink;
            if (_sink == null && commandSinkBehaviour != null)
                Debug.LogError("Avatar5: commandSinkBehaviour does not implement IPlayerCommandSink", this);
            if (viewCamera == null) viewCamera = Camera.main;
            _sequencer = new PlayerCommandSequencer(actorId);
            BindActions();
        }

        private void OnEnable()
        {
            EnableActions();
        }

        private void OnDisable()
        {
            DisableActions();
        }

        private void OnDestroy()
        {
            if (_runtimeActions != null) Destroy(_runtimeActions);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}
