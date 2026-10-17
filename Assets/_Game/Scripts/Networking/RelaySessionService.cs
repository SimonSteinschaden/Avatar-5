using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Multiplayer;
using UnityEngine;
using SdkSessionError = Unity.Services.Multiplayer.SessionError;

namespace Avatar5.Networking
{
    // Inside the namespace so our SessionError/SessionState win over the SDK types of the same name.
    using Avatar5.Contracts;
    using Avatar5.Gameplay.Session;

    /// <summary>
    /// Internet session through Unity Multiplayer Services (private session + Relay). The SDK starts the
    /// NetworkManager.Singleton itself; this adapter only adds our version/slot admission on top.
    /// Requires the project to be linked to a Unity Cloud project (Project Settings › Services).
    /// </summary>
    public sealed class RelaySessionService : NetcodeSessionServiceBase
    {
        private ISession _session;

        public RelaySessionService(NetworkManager manager) : base(manager) { }

        public override async Task<SessionJoinResult> CreatePrivateAsync(SessionConfig config, CancellationToken token)
        {
            if (IsBusy) return SessionJoinResult.Fail(SessionError.AlreadyInSession, "session already active");

            var servicesError = await EnsureServicesAsync(token);
            if (servicesError != null) return SessionJoinResult.Fail(SessionError.ServicesUnavailable, servicesError);

            PrepareHost(config);
            try
            {
                var options = new SessionOptions
                {
                    MaxPlayers = SessionAdmission.ClampMaxPlayers(config.MaxPlayers),
                    IsPrivate = true
                }.WithRelayNetwork();
                var host = await MultiplayerService.Instance.CreateSessionAsync(options);
                _session = host;
                if (token.IsCancellationRequested)
                {
                    await LeaveAsync(CancellationToken.None);
                    return SessionJoinResult.Fail(SessionError.Cancelled, null);
                }
                Watch(_session);
                HostStarted(host.Code);
                return Ok();
            }
            catch (Exception e)
            {
                _session = null;
                return Fail(Map(e), Describe(e));
            }
        }

        public override async Task<SessionJoinResult> JoinAsync(string joinCode, SessionConfig config, CancellationToken token)
        {
            if (IsBusy) return SessionJoinResult.Fail(SessionError.AlreadyInSession, "session already active");

            string code;
            if (!JoinCodes.TryNormalizeServiceCode(joinCode, out code)) return SessionJoinResult.Fail(SessionError.InvalidCode, "malformed code");

            var servicesError = await EnsureServicesAsync(token);
            if (servicesError != null) return SessionJoinResult.Fail(SessionError.ServicesUnavailable, servicesError);

            PrepareClient(config);
            try
            {
                var joinTask = MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                var timeout = Task.Delay(TimeSpan.FromSeconds(ConnectTimeoutSeconds * 2f), token);
                if (await Task.WhenAny(joinTask, timeout) != joinTask)
                {
                    ObserveLater(joinTask);
                    return Fail(token.IsCancellationRequested ? SessionError.Cancelled : SessionError.TimedOut, "join by code");
                }
                _session = await joinTask;
                RegisterInfoHandler();

                // The SDK returns once the transport is up; host approval may still reject us.
                var error = await WaitForClientConnectedAsync(token);
                if (error != SessionError.None)
                {
                    await LeaveSdkSessionQuietly();
                    return Fail(error, "host approval");
                }

                Watch(_session);
                ClientConnected(code);
                return Ok();
            }
            catch (Exception e)
            {
                var error = Map(e);
                // A version/slot refusal by our host surfaces as a NetworkManager start failure in the SDK.
                if (error == SessionError.TransportFailed) error = ReadRejectReason(SessionError.TransportFailed);
                await LeaveSdkSessionQuietly();
                return Fail(error, Describe(e));
            }
        }

        public override async Task LeaveAsync(CancellationToken token)
        {
            if (State.Phase == SessionPhase.Offline && _session == null) return;
            PublishLeaving();
            var session = _session;
            _session = null;
            Unwatch(session);
            try
            {
                if (session != null)
                {
                    // Host leaving ends the session for everybody (no host migration in v1).
                    if (session.IsHost)
                    {
                        await NotifyClientsHostEndingAsync();
                        await session.AsHost().DeleteAsync();
                    }
                    else await session.LeaveAsync();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Avatar5.Session] Leave: " + Describe(e));
            }
            StopNetwork();
            PublishOffline(SessionError.None);
        }

        private static async Task<string> EnsureServicesAsync(CancellationToken token)
        {
            if (string.IsNullOrEmpty(Application.cloudProjectId))
            {
                return "project is not linked to a Unity Cloud project (Project Settings > Services)";
            }
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
                while (UnityServices.State == ServicesInitializationState.Initializing)
                {
                    token.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                return null;
            }
            catch (Exception e)
            {
                return Describe(e);
            }
        }

        private void Watch(ISession session)
        {
            if (session == null) return;
            session.RemovedFromSession += OnRemovedFromSession;
            session.Deleted += OnSessionDeleted;
        }

        private void Unwatch(ISession session)
        {
            if (session == null) return;
            session.RemovedFromSession -= OnRemovedFromSession;
            session.Deleted -= OnSessionDeleted;
        }

        private void OnRemovedFromSession()
        {
            EndFromRemote(SessionError.Kicked);
        }

        private void OnSessionDeleted()
        {
            EndFromRemote(SessionError.HostEnded);
        }

        private void EndFromRemote(SessionError reason)
        {
            if (State.Phase != SessionPhase.Connected) return;
            Unwatch(_session);
            _session = null;
            StopNetwork();
            PublishOffline(reason);
        }

        private async Task LeaveSdkSessionQuietly()
        {
            var session = _session;
            _session = null;
            if (session == null) return;
            try { await session.LeaveAsync(); }
            catch (Exception e) { Debug.LogWarning("[Avatar5.Session] Cleanup leave: " + Describe(e)); }
        }

        private static void ObserveLater(Task<ISession> task)
        {
            task.ContinueWith(t =>
            {
                if (t.Exception != null) Debug.LogWarning("[Avatar5.Session] Late join failure: " + Describe(t.Exception));
                else if (t.Status == TaskStatus.RanToCompletion && t.Result != null) t.Result.LeaveAsync();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>Maps SDK exceptions to our error codes. Unknown cases stay Unknown, detail goes to the log.</summary>
        internal static SessionError Map(Exception e)
        {
            for (var current = e; current != null; current = current.InnerException)
            {
                var lobby = current as LobbyServiceException;
                if (lobby != null)
                {
                    switch (lobby.Reason)
                    {
                        case LobbyExceptionReason.LobbyFull: return SessionError.SessionFull;
                        case LobbyExceptionReason.LobbyNotFound: return SessionError.SessionNotFound;
                        case LobbyExceptionReason.InvalidJoinCode: return SessionError.SessionNotFound;
                        case LobbyExceptionReason.RateLimited: return SessionError.ServicesUnavailable;
                    }
                }

                var session = current as SessionException;
                if (session != null)
                {
                    switch (session.Error)
                    {
                        case SdkSessionError.SessionNotFound:
                        case SdkSessionError.SessionDeleted:
                        case SdkSessionError.InvalidSessionIdentifier:
                            return SessionError.SessionNotFound;
                        case SdkSessionError.NotAuthorized:
                        case SdkSessionError.Forbidden:
                        case SdkSessionError.RateLimitExceeded:
                            return SessionError.ServicesUnavailable;
                        case SdkSessionError.NetworkManagerNotInitialized:
                        case SdkSessionError.NetworkManagerStartFailed:
                        case SdkSessionError.NetworkSetupFailed:
                        case SdkSessionError.TransportComponentMissing:
                        case SdkSessionError.TransportInvalid:
                            return SessionError.TransportFailed;
                    }
                }

                if (current is OperationCanceledException) return SessionError.Cancelled;
            }
            return SessionError.Unknown;
        }

        private static string Describe(Exception e)
        {
            var root = e is AggregateException && e.InnerException != null ? e.InnerException : e;
            return root.GetType().Name + ": " + root.Message;
        }
    }
}
