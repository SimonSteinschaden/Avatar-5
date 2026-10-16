using System.Collections.Generic;
using Avatar5.Contracts;
using Avatar5.Input;
using Avatar5.Player;
using Unity.Cinemachine;
using UnityEngine;

namespace Avatar5.Tests.PlayMode
{
    /// <summary>Builds throw-away test worlds from primitives and cleans them up again.</summary>
    internal sealed class TestWorld
    {
        private readonly List<Object> _created = new List<Object>();
        private uint _sequence;

        public T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }

        public GameObject Box(string name, Vector3 position, Vector3 scale, float yaw = 0f)
        {
            var box = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            box.name = name;
            box.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            box.transform.localScale = scale;
            return box;
        }

        public GameObject Ground(float size = 60f)
        {
            return Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(size, 1f, size));
        }

        public CharacterMotor Motor(Vector3 position, ulong actorId = 1, bool simulateInUpdate = false)
        {
            var go = Track(new GameObject("TestPlayer"));
            go.transform.position = position;
            go.AddComponent<CharacterController>();
            var motor = go.AddComponent<CharacterMotor>();
            var definition = Track(ScriptableObject.CreateInstance<PlayerMovementDefinition>());
            motor.Configure(definition, actorId);
            motor.SimulateInUpdate = simulateInUpdate;
            Physics.SyncTransforms();
            return motor;
        }

        public RequestStatus Drive(CharacterMotor motor, Vector2 move, Vector2 aim = default)
        {
            _sequence++;
            return motor.Submit(new PlayerCommand
            {
                ActorId = motor.ActorId,
                SequenceId = _sequence,
                ClientTick = _sequence,
                Move = new Float2(move.x, move.y),
                Aim = new Float2(aim.x, aim.y)
            });
        }

        public static void Simulate(CharacterMotor motor, float seconds, float step = 1f / 60f)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += step) motor.Simulate(step);
        }

        public CameraProfile Profile()
        {
            return Track(ScriptableObject.CreateInstance<CameraProfile>());
        }

        public IsometricCameraRig CameraRig(Transform target, out Camera outputCamera)
        {
            var cameraObject = Track(new GameObject("TestMainCamera"));
            outputCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<CinemachineBrain>();

            var cmObject = Track(new GameObject("TestCmCamera"));
            var cmCamera = cmObject.AddComponent<CinemachineCamera>();
            cmObject.AddComponent<CinemachinePositionComposer>();
            var rig = cmObject.AddComponent<IsometricCameraRig>();
            rig.Configure(Profile(), cmCamera, target, outputCamera);
            return rig;
        }

        public void Dispose()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null) Object.Destroy(_created[i]);
            }

            _created.Clear();
        }
    }
}
