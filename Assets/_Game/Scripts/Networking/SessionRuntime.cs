using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avatar5.Contracts;
using Unity.Netcode;
using UnityEngine;

namespace Avatar5.Networking
{
    public enum SessionTransportMode { Direct, Relay }

    /// <summary>
    /// Owns the NetworkManager and the active <see cref="ISessionService"/> for the whole app lifetime.
    /// Also supports command-line automation for multi-process tests:
    /// <c>-a5-session host|join -a5-mode direct|relay -a5-code CODE -a5-port N -a5-protocol N -a5-leave-after S -a5-quit-after S</c>.
    /// Every state change is logged as one "A5SESSION ..." line so test scripts can parse it.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class SessionRuntime : MonoBehaviour
    {
        public const string LogTag = "A5SESSION";

        private static SessionRuntime _instance;

        [SerializeField] private SessionTransportMode mode = SessionTransportMode.Direct;
        [SerializeField] private ushort directPort = DirectSessionService.DefaultPort;

        private NetworkManager _manager;
        private DirectSessionService _direct;
        private RelaySessionService _relay;
        private SessionConfig _config = SessionConfig.Default;
        private CancellationTokenSource _lifetime;
        private bool _quitAfterLeave;

        public static SessionRuntime Instance { get { return _instance; } }

        public event Action<SessionState> StateChanged;

        public SessionTransportMode Mode
        {
            get { return mode; }
            set
            {
                if (mode == value) return;
                if (Current.State.Phase != SessionPhase.Offline && Current.State.Phase != SessionPhase.Failed)
                {
                    Debug.LogWarning("[Avatar5.Session] Leave the session before switching transport.");
                    return;
                }
                mode = value;
            }
        }

        public ISessionService Current
        {
            get { return mode == SessionTransportMode.Relay ? (ISessionService)_relay : _direct; }
        }

        /// <summary>Versions this build announces. Overridable via -a5-protocol for mismatch tests.</summary>
        public SessionConfig Config { get { return _config; } }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _manager = GetComponent<NetworkManager>();
            _lifetime = new CancellationTokenSource();
            _direct = new DirectSessionService(_manager) { Port = directPort };
            _relay = new RelaySessionService(_manager);
            _direct.StateChanged += Forward;
            _relay.StateChanged += Forward;
            ApplyCommandLineOverrides();
            Application.wantsToQuit += OnWantsToQuit;
        }

        private void Start()
        {
            if (_instance == this) RunCommandLineAutomation();
        }

        /// <summary>
        /// Closing the game while hosting first tells the other players that the host ended the session,
        /// then quits. Clients and solo hosts quit immediately.
        /// </summary>
        private bool OnWantsToQuit()
        {
            if (_quitAfterLeave) return true;
            var hosting = Current as NetcodeSessionServiceBase;
            if (hosting == null || !hosting.HasRemoteClients) return true;
            _quitAfterLeave = true;
            LeaveThenQuit();
            return false;
        }

        private async void LeaveThenQuit()
        {
            try { await LeaveAsync(); }
            finally { Application.Quit(); }
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
            Application.wantsToQuit -= OnWantsToQuit;
            if (_lifetime != null) _lifetime.Cancel();
            if (_direct != null) { _direct.StateChanged -= Forward; _direct.Dispose(); }
            if (_relay != null) { _relay.StateChanged -= Forward; _relay.Dispose(); }
        }

        public Task<SessionJoinResult> CreateAsync()
        {
            return Current.CreatePrivateAsync(_config, _lifetime.Token);
        }

        public Task<SessionJoinResult> JoinAsync(string code)
        {
            return Current.JoinAsync(code, _config, _lifetime.Token);
        }

        public Task LeaveAsync()
        {
            return Current.LeaveAsync(_lifetime.Token);
        }

        private void Forward(SessionState state)
        {
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "{0} mode={1} phase={2} role={3} players={4}/{5} code={6} error={7}",
                LogTag, mode, state.Phase, state.Role, state.PlayerCount, state.MaxPlayers,
                string.IsNullOrEmpty(state.JoinCode) ? "-" : state.JoinCode, state.LastError));
            var handler = StateChanged;
            if (handler != null) handler(state);
        }

        // ---- command line ----------------------------------------------------------------------

        private void ApplyCommandLineOverrides()
        {
            string value;
            if (TryArg("-a5-mode", out value)) mode = string.Equals(value, "relay", StringComparison.OrdinalIgnoreCase) ? SessionTransportMode.Relay : SessionTransportMode.Direct;
            int number;
            if (TryArg("-a5-port", out value) && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number > 0 && number <= ushort.MaxValue)
            {
                _direct.Port = (ushort)number;
            }
            if (TryArg("-a5-protocol", out value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            {
                _config.ProtocolVersion = number;
            }
        }

        private async void RunCommandLineAutomation()
        {
            string role;
            if (!TryArg("-a5-session", out role)) return;

            float quitAfter = ReadSeconds("-a5-quit-after", 0f);
            if (quitAfter > 0f) Invoke(nameof(QuitFromAutomation), quitAfter);

            SessionJoinResult result;
            if (string.Equals(role, "host", StringComparison.OrdinalIgnoreCase))
            {
                result = await CreateAsync();
            }
            else
            {
                string code;
                TryArg("-a5-code", out code);
                result = await JoinAsync(code);
            }
            Debug.Log(LogTag + " result=" + (result.Success ? "ok" : "fail") + " error=" + result.Error
                + (string.IsNullOrEmpty(result.Detail) ? string.Empty : " detail=" + result.Detail));

            float leaveAfter = ReadSeconds("-a5-leave-after", 0f);
            if (result.Success && leaveAfter > 0f) Invoke(nameof(LeaveFromAutomation), leaveAfter);
        }

        private async void LeaveFromAutomation()
        {
            await LeaveAsync();
            Debug.Log(LogTag + " left");
        }

        private void QuitFromAutomation()
        {
            Debug.Log(LogTag + " quit");
            Application.Quit();
        }

        private static float ReadSeconds(string name, float fallback)
        {
            string value;
            float seconds;
            return TryArg(name, out value) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) ? seconds : fallback;
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
