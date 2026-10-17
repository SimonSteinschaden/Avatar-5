using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avatar5.Contracts;
using Avatar5.Gameplay.Session;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Avatar5.Networking
{
    /// <summary>
    /// Shared NGO plumbing for every session flavour: host-side version/slot admission, player count
    /// broadcast to clients, disconnect-reason mapping and state publication. Subclasses only decide
    /// how the NetworkManager gets started (direct UDP or Unity Sessions + Relay).
    /// </summary>
    public abstract class NetcodeSessionServiceBase : ISessionService, IDisposable
    {
        private const string SessionInfoMessage = "A5.SessionInfo";

        protected readonly NetworkManager Manager;
        private readonly HashSet<ulong> _approvedPending = new HashSet<ulong>();
        private SessionConfig _config;
        private SessionState _state = SessionState.Offline(SessionError.None);
        private bool _acceptingJoins;
        private bool _hooked;
        private bool _leaving;
        private TaskCompletionSource<SessionError> _clientConnect;

        public event Action<SessionState> StateChanged;

        protected NetcodeSessionServiceBase(NetworkManager manager)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            Manager = manager;
        }

        public SessionState State { get { return _state; } }

        /// <summary>Seconds a client waits for host approval before giving up.</summary>
        public float ConnectTimeoutSeconds { get; set; } = 15f;

        public abstract Task<SessionJoinResult> CreatePrivateAsync(SessionConfig config, CancellationToken token);
        public abstract Task<SessionJoinResult> JoinAsync(string joinCode, SessionConfig config, CancellationToken token);
        public abstract Task LeaveAsync(CancellationToken token);

        protected bool IsBusy
        {
            get { return _state.Phase != SessionPhase.Offline && _state.Phase != SessionPhase.Failed; }
        }

        // ---- host side -------------------------------------------------------------------------

        /// <summary>Configures approval before the host starts. Call right before StartHost/CreateSession.</summary>
        protected void PrepareHost(SessionConfig config)
        {
            _config = config;
            _config.MaxPlayers = SessionAdmission.ClampMaxPlayers(config.MaxPlayers);
            _approvedPending.Clear();
            _acceptingJoins = true;
            _leaving = false;
            Manager.NetworkConfig.ConnectionApproval = true;
            Manager.NetworkConfig.ConnectionData = ConnectionPayload.From(_config).Encode();
            Manager.ConnectionApprovalCallback = Approve;
            Hook();
            Publish(SessionPhase.Creating, SessionRole.Host, null, SessionError.None);
        }

        protected void HostStarted(string joinCode)
        {
            RegisterInfoHandler();
            Publish(SessionPhase.Connected, SessionRole.Host, joinCode, SessionError.None);
            BroadcastInfo();
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            // NGO may already list an approved client before its ClientConnected event; count it once.
            var occupied = Manager.ConnectedClientsIds.Count;
            foreach (var pending in _approvedPending)
            {
                if (!Contains(Manager.ConnectedClientsIds, pending)) occupied++;
            }
            var decision = SessionAdmission.Evaluate(request.Payload, _config, occupied, _acceptingJoins);
            response.Approved = decision.Approved;
            response.CreatePlayerObject = false; // player actors arrive with the NetworkPlayer story
            response.Pending = false;
            if (decision.Approved)
            {
                _approvedPending.Add(request.ClientNetworkId);
            }
            else
            {
                response.Reason = AdmissionReasonCodec.Encode(decision.Reason);
                Debug.Log("[Avatar5.Session] Rejected join: " + decision.Reason);
            }
        }

        private static bool Contains(IReadOnlyList<ulong> ids, ulong id)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id) return true;
            }
            return false;
        }

        /// <summary>
        /// Tells every remote client that the host is ending the session, so they show "host ended"
        /// instead of a generic connection loss. No-op on clients.
        /// </summary>
        /// <remarks>NGO sends the reason and disconnects on a later frame, so this waits briefly before returning.</remarks>
        protected async Task NotifyClientsHostEndingAsync()
        {
            _acceptingJoins = false;
            if (!Manager.IsServer || !Manager.IsListening) return;
            var reason = AdmissionReasonCodec.Encode(SessionError.HostEnded);
            var clients = new List<ulong>(Manager.ConnectedClientsIds);
            var notified = 0;
            foreach (var clientId in clients)
            {
                if (clientId == NetworkManager.ServerClientId) continue;
                Manager.DisconnectClient(clientId, reason);
                notified++;
            }
            if (notified == 0) return;
            var deadline = DateTime.UtcNow.AddMilliseconds(HostEndNotifyMilliseconds);
            while (DateTime.UtcNow < deadline && Manager.IsListening && Manager.ConnectedClientsIds.Count > 1) await Task.Yield();
        }

        /// <summary>Upper bound for waiting until remote clients received the "host ended" reason.</summary>
        public int HostEndNotifyMilliseconds { get; set; } = 500;

        /// <summary>True while hosting with at least one remote player connected.</summary>
        public bool HasRemoteClients
        {
            get { return Manager.IsServer && Manager.IsListening && Manager.ConnectedClientsIds.Count > 1; }
        }

        // ---- client side -----------------------------------------------------------------------

        /// <summary>Sets the version payload and resets client bookkeeping. Call right before StartClient/JoinSession.</summary>
        protected void PrepareClient(SessionConfig config)
        {
            _config = config;
            _leaving = false;
            Manager.NetworkConfig.ConnectionApproval = true;
            Manager.NetworkConfig.ConnectionData = ConnectionPayload.From(config).Encode();
            Manager.ConnectionApprovalCallback = null;
            _clientConnect = new TaskCompletionSource<SessionError>();
            Hook();
            Publish(SessionPhase.Joining, SessionRole.Client, null, SessionError.None);
        }

        /// <summary>Waits until the host approved this client (or rejected it / the timeout hits).</summary>
        protected async Task<SessionError> WaitForClientConnectedAsync(CancellationToken token)
        {
            if (Manager.IsConnectedClient) return SessionError.None;
            var pending = _clientConnect;
            var timeout = Task.Delay(TimeSpan.FromSeconds(ConnectTimeoutSeconds), token);
            var finished = await Task.WhenAny(pending.Task, timeout);
            if (finished == pending.Task) return pending.Task.Result;
            return token.IsCancellationRequested ? SessionError.Cancelled : SessionError.TimedOut;
        }

        protected void ClientConnected(string joinCode)
        {
            RegisterInfoHandler();
            Publish(SessionPhase.Connected, SessionRole.Client, joinCode, SessionError.None);
        }

        // ---- shared ----------------------------------------------------------------------------

        protected SessionJoinResult Fail(SessionError error, string detail)
        {
            Debug.LogWarning("[Avatar5.Session] " + error + (string.IsNullOrEmpty(detail) ? string.Empty : ": " + detail));
            StopNetwork();
            _state = SessionState.Offline(error);
            _state.Phase = SessionPhase.Failed;
            RaiseChanged();
            return SessionJoinResult.Fail(error, detail);
        }

        protected SessionJoinResult Ok()
        {
            return SessionJoinResult.Ok(_state);
        }

        /// <summary>Stops the host/client. Host shutdown ends the session for everyone (no host migration in v1).</summary>
        protected void StopNetwork()
        {
            _leaving = true;
            _acceptingJoins = false;
            if (Manager.IsListening && !Manager.ShutdownInProgress) Manager.Shutdown();
            if (Manager.CustomMessagingManager != null) Manager.CustomMessagingManager.UnregisterNamedMessageHandler(SessionInfoMessage);
            _approvedPending.Clear();
        }

        protected void PublishLeaving()
        {
            Publish(SessionPhase.Leaving, _state.Role, _state.JoinCode, SessionError.None);
        }

        protected void PublishOffline(SessionError reason)
        {
            _state = SessionState.Offline(reason);
            RaiseChanged();
        }

        /// <summary>Error the host gave for refusing us, or <paramref name="fallback"/> if none was transmitted.</summary>
        protected SessionError ReadRejectReason(SessionError fallback)
        {
            return AdmissionReasonCodec.Decode(Manager.DisconnectReason, fallback);
        }

        private void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            Manager.OnConnectionEvent += OnConnectionEvent;
            Manager.OnClientStopped += OnClientStopped;
            Manager.OnServerStopped += OnServerStopped;
        }

        public virtual void Dispose()
        {
            if (!_hooked || Manager == null) return;
            _hooked = false;
            Manager.OnConnectionEvent -= OnConnectionEvent;
            Manager.OnClientStopped -= OnClientStopped;
            Manager.OnServerStopped -= OnServerStopped;
            if (Manager.ConnectionApprovalCallback == Approve) Manager.ConnectionApprovalCallback = null;
        }

        private void OnConnectionEvent(NetworkManager manager, ConnectionEventData data)
        {
            if (Manager.IsServer)
            {
                if (data.EventType == ConnectionEvent.ClientConnected || data.EventType == ConnectionEvent.ClientDisconnected)
                {
                    _approvedPending.Remove(data.ClientId);
                    UpdatePlayerCount(Manager.ConnectedClientsIds.Count, _config.MaxPlayers);
                    BroadcastInfo();
                }
                return;
            }

            if (data.EventType == ConnectionEvent.ClientConnected && data.ClientId == Manager.LocalClientId && _clientConnect != null)
            {
                _clientConnect.TrySetResult(SessionError.None);
            }
        }

        private void OnClientStopped(bool wasHost)
        {
            if (wasHost) return;
            var reason = ReadRejectReason(_state.Phase == SessionPhase.Connected ? SessionError.ConnectionLost : SessionError.TransportFailed);
            if (_clientConnect != null && _clientConnect.TrySetResult(reason)) return; // join attempt fails with this reason
            if (_leaving || _state.Phase != SessionPhase.Connected) return;
            // Host left or the link broke while playing: back to offline with an explanation.
            if (reason == SessionError.ConnectionLost && string.IsNullOrEmpty(Manager.DisconnectReason)) reason = SessionError.HostEnded;
            PublishOffline(reason);
        }

        private void OnServerStopped(bool wasHost)
        {
            if (_leaving || _state.Phase != SessionPhase.Connected) return;
            PublishOffline(SessionError.TransportFailed);
        }

        /// <summary>Clients call this right after the transport started so the first player-count message is not missed.</summary>
        protected void RegisterInfoHandler()
        {
            if (Manager.CustomMessagingManager == null) return;
            Manager.CustomMessagingManager.UnregisterNamedMessageHandler(SessionInfoMessage);
            if (!Manager.IsServer) Manager.CustomMessagingManager.RegisterNamedMessageHandler(SessionInfoMessage, OnSessionInfo);
        }

        private void BroadcastInfo()
        {
            if (!Manager.IsServer || !Manager.IsListening || Manager.CustomMessagingManager == null) return;
            var writer = new FastBufferWriter(8, Allocator.Temp);
            try
            {
                writer.WriteValueSafe(Manager.ConnectedClientsIds.Count);
                writer.WriteValueSafe(_config.MaxPlayers);
                Manager.CustomMessagingManager.SendNamedMessageToAll(SessionInfoMessage, writer);
            }
            finally
            {
                writer.Dispose();
            }
        }

        private void OnSessionInfo(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId) return;
            int count, max;
            reader.ReadValueSafe(out count);
            reader.ReadValueSafe(out max);
            UpdatePlayerCount(count, max);
        }

        private void UpdatePlayerCount(int count, int max)
        {
            if (_state.Phase != SessionPhase.Connected && _state.Phase != SessionPhase.Leaving) return;
            if (_state.PlayerCount == count && _state.MaxPlayers == max) return;
            _state.PlayerCount = count;
            _state.MaxPlayers = max;
            RaiseChanged();
        }

        private void Publish(SessionPhase phase, SessionRole role, string joinCode, SessionError error)
        {
            _state.Phase = phase;
            _state.Role = role;
            _state.JoinCode = joinCode;
            _state.LastError = error;
            _state.MaxPlayers = _config.MaxPlayers > 0 ? SessionAdmission.ClampMaxPlayers(_config.MaxPlayers) : SessionConfig.AbsoluteMaxPlayers;
            _state.LocalPlayerId = Manager.IsListening ? Manager.LocalClientId : 0;
            _state.PlayerCount = phase == SessionPhase.Connected && Manager.IsServer ? Manager.ConnectedClientsIds.Count : _state.PlayerCount;
            if (phase != SessionPhase.Connected) _state.PlayerCount = 0;
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            var handler = StateChanged;
            if (handler != null) handler(_state);
        }
    }
}
