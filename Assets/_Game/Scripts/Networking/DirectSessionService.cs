using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Avatar5.Contracts;
using Avatar5.Gameplay.Session;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Avatar5.Networking
{
    /// <summary>
    /// Direct UDP session for LAN and same-PC tests, without Unity Cloud services.
    /// The join code is "address:port" of the host. Same admission rules as the Relay variant.
    /// </summary>
    public sealed class DirectSessionService : NetcodeSessionServiceBase
    {
        public const ushort DefaultPort = 7777;

        public DirectSessionService(NetworkManager manager) : base(manager) { }

        public ushort Port { get; set; } = DefaultPort;

        public override Task<SessionJoinResult> CreatePrivateAsync(SessionConfig config, CancellationToken token)
        {
            if (IsBusy) return Task.FromResult(SessionJoinResult.Fail(SessionError.AlreadyInSession, "session already active"));
            if (token.IsCancellationRequested) return Task.FromResult(SessionJoinResult.Fail(SessionError.Cancelled, null));

            var transport = Transport();
            if (transport == null) return Task.FromResult(Fail(SessionError.TransportFailed, "UnityTransport missing on NetworkManager"));
            transport.SetConnectionData(true, "127.0.0.1", Port, "0.0.0.0");

            PrepareHost(config);
            if (!Manager.StartHost()) return Task.FromResult(Fail(SessionError.TransportFailed, "StartHost failed (port " + Port + " in use?)"));
            HostStarted(LocalAddress() + ":" + Port);
            return Task.FromResult(Ok());
        }

        public override async Task<SessionJoinResult> JoinAsync(string joinCode, SessionConfig config, CancellationToken token)
        {
            if (IsBusy) return SessionJoinResult.Fail(SessionError.AlreadyInSession, "session already active");

            string address;
            ushort port;
            if (!JoinCodes.TryParseDirect(joinCode, DefaultPort, out address, out port))
            {
                return SessionJoinResult.Fail(SessionError.InvalidCode, "expected address:port");
            }

            var transport = Transport();
            if (transport == null) return Fail(SessionError.TransportFailed, "UnityTransport missing on NetworkManager");
            transport.SetConnectionData(true, address, port);

            PrepareClient(config);
            if (!Manager.StartClient()) return Fail(SessionError.TransportFailed, "StartClient failed");
            RegisterInfoHandler();

            var error = await WaitForClientConnectedAsync(token);
            if (error != SessionError.None)
            {
                // Nobody answering at that address is reported as "not found", not as a transport bug.
                if (error == SessionError.TransportFailed || error == SessionError.TimedOut) error = SessionError.SessionNotFound;
                return Fail(error, "join " + address + ":" + port);
            }

            ClientConnected(address + ":" + port);
            return Ok();
        }

        public override async Task LeaveAsync(CancellationToken token)
        {
            if (State.Phase == SessionPhase.Offline || State.Phase == SessionPhase.Leaving) return;
            PublishLeaving();
            await NotifyClientsHostEndingAsync();
            StopNetwork();
            PublishOffline(SessionError.None);
        }

        private UnityTransport Transport()
        {
            return Manager.NetworkConfig.NetworkTransport as UnityTransport;
        }

        /// <summary>First LAN IPv4 of this PC so the host can read its code to a friend; falls back to loopback.</summary>
        private static string LocalAddress()
        {
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    // Skip loopback and 169.254.x.x (link-local, not reachable by other PCs).
                    if (ip.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ip)) continue;
                    var bytes = ip.GetAddressBytes();
                    if (bytes[0] == 169 && bytes[1] == 254) continue;
                    return ip.ToString();
                }
            }
            catch (SocketException) { }
            catch (ArgumentException) { }
            return "127.0.0.1";
        }
    }
}
