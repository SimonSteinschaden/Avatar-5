#if UNITY_EDITOR
using Avatar5.Contracts;
using Avatar5.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Avatar5.Tests.PlayMode
{
    /// <summary>Real device bindings (virtual devices via InputTestFixture) → PlayerCommand intents.</summary>
    public sealed class PlayerInputAdapterTests : InputTestFixture
    {
        private const string ActionsPath = "Assets/_Game/Input/Avatar5PlayerInput.inputactions";
        private const float Tolerance = 1e-3f;

        private sealed class RecordingSink : IPlayerCommandSink
        {
            public int Count;
            public PlayerCommand Last;
            public RequestStatus Submit(in PlayerCommand command)
            {
                Count++;
                Last = command;
                return RequestStatus.Accepted;
            }
        }

        private GameObject _root;
        private Camera _camera;
        private Transform _actor;
        private PlayerInputAdapter _adapter;
        private RecordingSink _sink;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private Gamepad _gamepad;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            _gamepad = InputSystem.AddDevice<Gamepad>();

            _root = new GameObject("InputTestRoot");
            _actor = new GameObject("Actor").transform;
            _actor.SetParent(_root.transform);

            _camera = new GameObject("Camera").AddComponent<Camera>();
            _camera.transform.SetParent(_root.transform);
            _camera.orthographic = true;
            _camera.orthographicSize = 8f;
            _camera.transform.rotation = Quaternion.Euler(40f, 45f, 0f);
            _camera.transform.position = -(_camera.transform.rotation * Vector3.forward) * 30f;

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            Assert.That(actions, Is.Not.Null, "input actions asset");

            _sink = new RecordingSink();
            _adapter = _root.AddComponent<PlayerInputAdapter>();
            _adapter.Configure(actions, _sink, _actor, _camera, 3);
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(_root);
            base.TearDown();
        }

        private static Vector2 Planar(Float2 v) { return new Vector2(v.X, v.Y); }

        /// <summary>First pointer sample is only a baseline, so establish one before the real move.</summary>
        private void MovePointerTo(Vector3 world)
        {
            Move(_mouse.position, new Vector2(1f, 1f));
            _adapter.Tick(1f / 60f);
            var screen = _camera.WorldToScreenPoint(world);
            Move(_mouse.position, new Vector2(screen.x, screen.y));
        }

        private Vector2 CameraForward()
        {
            var f = _camera.transform.forward;
            return new Vector2(f.x, f.z).normalized;
        }

        private Vector2 CameraRight()
        {
            var r = _camera.transform.right;
            return new Vector2(r.x, r.z).normalized;
        }

        [Test]
        public void W_MovesAlongCameraForward()
        {
            Press(_keyboard.wKey);
            var command = _adapter.Tick(1f / 60f);

            Assert.That(_sink.Count, Is.EqualTo(1));
            Assert.That(command.ActorId, Is.EqualTo(3ul));
            Assert.That(Vector2.Distance(Planar(command.Move), CameraForward()), Is.LessThan(Tolerance));
        }

        [Test]
        public void WD_DiagonalIsUnitLengthAndCameraRelative()
        {
            Press(_keyboard.wKey);
            Press(_keyboard.dKey);
            var command = _adapter.Tick(1f / 60f);

            var expected = (CameraForward() + CameraRight()).normalized;
            Assert.That(Planar(command.Move).magnitude, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(Vector2.Distance(Planar(command.Move), expected), Is.LessThan(Tolerance));
        }

        [Test]
        public void EightKeyboardDirections_AllHaveUnitSpeed()
        {
            var keys = new[]
            {
                new[] { _keyboard.wKey }, new[] { _keyboard.wKey, _keyboard.dKey }, new[] { _keyboard.dKey },
                new[] { _keyboard.sKey, _keyboard.dKey }, new[] { _keyboard.sKey }, new[] { _keyboard.sKey, _keyboard.aKey },
                new[] { _keyboard.aKey }, new[] { _keyboard.wKey, _keyboard.aKey }
            };

            foreach (var combo in keys)
            {
                foreach (var key in combo) Press(key);
                var command = _adapter.Tick(1f / 60f);
                Assert.That(Planar(command.Move).magnitude, Is.EqualTo(1f).Within(Tolerance), combo[0].name);
                foreach (var key in combo) Release(key);
            }
        }

        [Test]
        public void GamepadLeftStick_MovesCameraRelative()
        {
            Set(_gamepad.leftStick, new Vector2(0f, 1f));
            var command = _adapter.Tick(1f / 60f);
            Assert.That(Vector2.Distance(Planar(command.Move).normalized, CameraForward()), Is.LessThan(0.01f));
        }

        [Test]
        public void GamepadRightStick_AimsIndependentlyOfMovement()
        {
            Set(_gamepad.leftStick, new Vector2(0f, 1f));
            Set(_gamepad.rightStick, new Vector2(1f, 0f));
            var command = _adapter.Tick(1f / 60f);

            Assert.That(_adapter.CurrentAimSource, Is.EqualTo(AimSource.Stick));
            Assert.That(Vector2.Distance(Planar(command.Aim), CameraRight()), Is.LessThan(0.01f));
            Assert.That(Vector2.Distance(Planar(command.Move).normalized, CameraForward()), Is.LessThan(0.01f));

            Set(_gamepad.rightStick, Vector2.zero);
            command = _adapter.Tick(1f / 60f);
            Assert.That(Vector2.Distance(Planar(command.Aim), CameraRight()), Is.LessThan(0.01f), "released stick keeps last aim");
        }

        [Test]
        public void Mouse_AimsAtGroundPoint()
        {
            var worldTarget = new Vector3(3f, 0f, 0f);
            MovePointerTo(worldTarget);
            Press(_keyboard.sKey);
            var command = _adapter.Tick(1f / 60f);

            Assert.That(_adapter.CurrentAimSource, Is.EqualTo(AimSource.Pointer));
            Assert.That(command.Aim.X, Is.EqualTo(1f).Within(0.01f));
            Assert.That(command.Aim.Y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(Vector3.Distance(_adapter.AimPoint, worldTarget), Is.LessThan(0.05f));
            Assert.That(Vector2.Dot(Planar(command.Move), CameraForward()), Is.LessThan(-0.99f), "moving backwards while aiming sideways");
        }

        [Test]
        public void MouseOutsideView_KeepsLastAim()
        {
            MovePointerTo(new Vector3(0f, 0f, 3f));
            var first = _adapter.Tick(1f / 60f);
            Assert.That(first.Aim.Y, Is.EqualTo(1f).Within(0.01f));

            Move(_mouse.position, new Vector2(-500f, -500f));
            var second = _adapter.Tick(1f / 60f);

            Assert.That(second.Aim.X, Is.EqualTo(first.Aim.X).Within(Tolerance));
            Assert.That(second.Aim.Y, Is.EqualTo(first.Aim.Y).Within(Tolerance));
        }

        [Test]
        public void SequenceIds_IncreaseEveryTick()
        {
            var a = _adapter.Tick(1f / 60f);
            var b = _adapter.Tick(1f / 60f);
            Assert.That(b.SequenceId, Is.EqualTo(a.SequenceId + 1));
        }

        [Test]
        public void ActionButtons_MapToPlayerActions()
        {
            Press(_mouse.leftButton);
            Assert.That(_adapter.Tick(1f / 60f).Action, Is.EqualTo(PlayerActionKind.Cast));
            Release(_mouse.leftButton);

            Press(_gamepad.buttonSouth);
            Assert.That(_adapter.Tick(1f / 60f).Action, Is.EqualTo(PlayerActionKind.Dodge));
            Release(_gamepad.buttonSouth);

            Press(_keyboard.eKey);
            Assert.That(_adapter.Tick(1f / 60f).Action, Is.EqualTo(PlayerActionKind.Interact));
        }

        [Test]
        public void Pause_SuppressesMovement()
        {
            Press(_keyboard.wKey);
            Press(_keyboard.escapeKey); // pressed in the sampled input update
            var command = _adapter.Tick(1f / 60f);
            Assert.That(_adapter.IsPaused, Is.True);
            Assert.That(command.Move.X, Is.EqualTo(0f));
            Assert.That(command.Move.Y, Is.EqualTo(0f));
        }

        [Test]
        public void BindingOverrides_RoundTripAsJson()
        {
            var move = _adapter.RuntimeActions.FindAction("Gameplay/Dodge", true);
            move.ApplyBindingOverride(0, "<Keyboard>/leftShift");
            var json = _adapter.SaveBindingOverridesAsJson();
            Assert.That(json, Does.Contain("leftShift"));

            move.RemoveAllBindingOverrides();
            _adapter.LoadBindingOverridesFromJson(json);
            Assert.That(move.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/leftShift"));
        }
    }
}
#endif
