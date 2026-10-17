using System;
using System.Threading;
using System.Threading.Tasks;

namespace Avatar5.Contracts
{
    public enum SessionPhase : byte { Offline, Creating, Joining, Connected, Leaving, Failed }

    public enum SessionRole : byte { None, Host, Client }

    /// <summary>Why a session operation failed or ended. Mapped to readable text by the UI layer.</summary>
    public enum SessionError : byte
    {
        None,
        InvalidCode,
        SessionNotFound,
        SessionFull,
        VersionMismatch,
        TimedOut,
        Cancelled,
        AlreadyInSession,
        ServicesUnavailable,
        TransportFailed,
        ConnectionLost,
        HostEnded,
        Kicked,
        Unknown
    }

    /// <summary>Settings for a private session. Solo is a session with one player (host).</summary>
    [Serializable]
    public struct SessionConfig
    {
        public const int AbsoluteMaxPlayers = 4;

        public int MaxPlayers;
        public int ProtocolVersion;
        public int ContentVersion;

        public static SessionConfig Default
        {
            get
            {
                return new SessionConfig
                {
                    MaxPlayers = AbsoluteMaxPlayers,
                    ProtocolVersion = VersionContracts.NetworkProtocolVersion,
                    ContentVersion = VersionContracts.ContentSchemaVersion
                };
            }
        }
    }

    /// <summary>
    /// Readable session snapshot. Written only by the session service (P03); read by P01, P05, P07.
    /// LocalPlayerId is the transport client id for this session only, never a persistent identity.
    /// </summary>
    [Serializable]
    public struct SessionState
    {
        public SessionPhase Phase;
        public SessionRole Role;
        public string JoinCode;
        public int PlayerCount;
        public int MaxPlayers;
        public ulong LocalPlayerId;
        public SessionError LastError;

        public bool IsActive { get { return Phase == SessionPhase.Connected; } }

        public static SessionState Offline(SessionError lastError)
        {
            return new SessionState { Phase = SessionPhase.Offline, Role = SessionRole.None, LastError = lastError };
        }
    }

    [Serializable]
    public struct SessionJoinResult
    {
        public bool Success;
        public SessionError Error;
        /// <summary>Technical detail for logs (no tokens or personal ids). UI shows text for Error instead.</summary>
        public string Detail;
        public SessionState State;

        public static SessionJoinResult Ok(SessionState state)
        {
            return new SessionJoinResult { Success = true, Error = SessionError.None, State = state };
        }

        public static SessionJoinResult Fail(SessionError error, string detail)
        {
            return new SessionJoinResult { Success = false, Error = error, Detail = detail, State = SessionState.Offline(error) };
        }
    }

    /// <summary>
    /// Session lifecycle port. Implementations wrap the transport/service SDK (direct UDP, Unity Sessions + Relay);
    /// gameplay code only sees this interface. All methods are called from the Unity main thread.
    /// </summary>
    public interface ISessionService
    {
        SessionState State { get; }
        event Action<SessionState> StateChanged;

        Task<SessionJoinResult> CreatePrivateAsync(SessionConfig config, CancellationToken token);
        Task<SessionJoinResult> JoinAsync(string joinCode, SessionConfig config, CancellationToken token);
        Task LeaveAsync(CancellationToken token);
    }
}
