using System;
using Avatar5.Contracts;

namespace Avatar5.Gameplay.Session
{
    /// <summary>
    /// Version handshake sent by a joining client with its connection request and checked by the host
    /// before any player object is spawned. Fixed 12-byte little-endian layout: magic, protocol, content.
    /// </summary>
    public readonly struct ConnectionPayload
    {
        public const int Magic = 0x35415641; // "AVA5" little-endian
        public const int ByteLength = 12;

        public readonly int ProtocolVersion;
        public readonly int ContentVersion;

        public ConnectionPayload(int protocolVersion, int contentVersion)
        {
            ProtocolVersion = protocolVersion;
            ContentVersion = contentVersion;
        }

        public static ConnectionPayload From(in SessionConfig config)
        {
            return new ConnectionPayload(config.ProtocolVersion, config.ContentVersion);
        }

        public byte[] Encode()
        {
            var bytes = new byte[ByteLength];
            WriteInt(bytes, 0, Magic);
            WriteInt(bytes, 4, ProtocolVersion);
            WriteInt(bytes, 8, ContentVersion);
            return bytes;
        }

        public static bool TryDecode(byte[] bytes, out ConnectionPayload payload)
        {
            payload = default(ConnectionPayload);
            if (bytes == null || bytes.Length != ByteLength || ReadInt(bytes, 0) != Magic) return false;
            payload = new ConnectionPayload(ReadInt(bytes, 4), ReadInt(bytes, 8));
            return true;
        }

        private static void WriteInt(byte[] target, int offset, int value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }

        private static int ReadInt(byte[] source, int offset)
        {
            return source[offset] | (source[offset + 1] << 8) | (source[offset + 2] << 16) | (source[offset + 3] << 24);
        }
    }

    public readonly struct AdmissionDecision
    {
        public readonly bool Approved;
        public readonly SessionError Reason;

        private AdmissionDecision(bool approved, SessionError reason)
        {
            Approved = approved;
            Reason = reason;
        }

        public static readonly AdmissionDecision Accept = new AdmissionDecision(true, SessionError.None);

        public static AdmissionDecision Reject(SessionError reason)
        {
            return new AdmissionDecision(false, reason);
        }
    }

    /// <summary>Host-side join rules, independent of the transport SDK.</summary>
    public static class SessionAdmission
    {
        /// <param name="connectedPlayers">Players already in the session, including the host itself.</param>
        public static AdmissionDecision Evaluate(byte[] payloadBytes, in SessionConfig hostConfig, int connectedPlayers, bool acceptingJoins)
        {
            if (!acceptingJoins) return AdmissionDecision.Reject(SessionError.HostEnded);

            ConnectionPayload payload;
            if (!ConnectionPayload.TryDecode(payloadBytes, out payload)) return AdmissionDecision.Reject(SessionError.VersionMismatch);
            if (VersionContracts.CheckNetworkProtocol(payload.ProtocolVersion) != RequestStatus.Accepted
                || payload.ProtocolVersion != hostConfig.ProtocolVersion
                || payload.ContentVersion != hostConfig.ContentVersion)
            {
                return AdmissionDecision.Reject(SessionError.VersionMismatch);
            }

            if (connectedPlayers >= ClampMaxPlayers(hostConfig.MaxPlayers)) return AdmissionDecision.Reject(SessionError.SessionFull);
            return AdmissionDecision.Accept;
        }

        public static int ClampMaxPlayers(int requested)
        {
            if (requested < 1) return 1;
            return requested > SessionConfig.AbsoluteMaxPlayers ? SessionConfig.AbsoluteMaxPlayers : requested;
        }
    }

    /// <summary>
    /// Encodes a rejection reason into the short disconnect-reason string the transport hands to the client,
    /// so the client can show the same error the host decided on.
    /// </summary>
    public static class AdmissionReasonCodec
    {
        private const string Prefix = "A5:";

        public static string Encode(SessionError reason)
        {
            return Prefix + reason;
        }

        public static SessionError Decode(string reason, SessionError fallback)
        {
            if (string.IsNullOrEmpty(reason) || !reason.StartsWith(Prefix, StringComparison.Ordinal)) return fallback;
            SessionError parsed;
            return Enum.TryParse(reason.Substring(Prefix.Length), false, out parsed) ? parsed : fallback;
        }
    }
}
