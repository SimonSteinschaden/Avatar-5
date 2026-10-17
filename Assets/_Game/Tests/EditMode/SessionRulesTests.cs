using Avatar5.Contracts;
using Avatar5.Gameplay.Session;
using NUnit.Framework;

namespace Avatar5.Tests.EditMode
{
    public sealed class SessionRulesTests
    {
        private static SessionConfig HostConfig(int maxPlayers = 4)
        {
            var config = SessionConfig.Default;
            config.MaxPlayers = maxPlayers;
            return config;
        }

        [Test]
        public void Payload_RoundTrips()
        {
            var bytes = new ConnectionPayload(7, 3).Encode();
            Assert.AreEqual(ConnectionPayload.ByteLength, bytes.Length);

            ConnectionPayload decoded;
            Assert.IsTrue(ConnectionPayload.TryDecode(bytes, out decoded));
            Assert.AreEqual(7, decoded.ProtocolVersion);
            Assert.AreEqual(3, decoded.ContentVersion);
        }

        [TestCase(null)]
        [TestCase(new byte[0])]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 })]
        public void Payload_RejectsGarbage(byte[] bytes)
        {
            ConnectionPayload decoded;
            Assert.IsFalse(ConnectionPayload.TryDecode(bytes, out decoded));
        }

        [Test]
        public void Admission_AcceptsMatchingVersionWithFreeSlot()
        {
            var payload = ConnectionPayload.From(HostConfig()).Encode();
            var decision = SessionAdmission.Evaluate(payload, HostConfig(), 1, true);
            Assert.IsTrue(decision.Approved);
            Assert.AreEqual(SessionError.None, decision.Reason);
        }

        [Test]
        public void Admission_RejectsProtocolMismatchBeforeSpawn()
        {
            var payload = new ConnectionPayload(VersionContracts.NetworkProtocolVersion + 1, VersionContracts.ContentSchemaVersion).Encode();
            var decision = SessionAdmission.Evaluate(payload, HostConfig(), 1, true);
            Assert.IsFalse(decision.Approved);
            Assert.AreEqual(SessionError.VersionMismatch, decision.Reason);
        }

        [Test]
        public void Admission_RejectsContentMismatch()
        {
            var payload = new ConnectionPayload(VersionContracts.NetworkProtocolVersion, VersionContracts.ContentSchemaVersion + 1).Encode();
            Assert.AreEqual(SessionError.VersionMismatch, SessionAdmission.Evaluate(payload, HostConfig(), 1, true).Reason);
        }

        [Test]
        public void Admission_RejectsMissingPayloadAsVersionMismatch()
        {
            Assert.AreEqual(SessionError.VersionMismatch, SessionAdmission.Evaluate(null, HostConfig(), 1, true).Reason);
        }

        [Test]
        public void Admission_FourthPlayerFitsFifthIsRejected()
        {
            var payload = ConnectionPayload.From(HostConfig()).Encode();
            Assert.IsTrue(SessionAdmission.Evaluate(payload, HostConfig(), 3, true).Approved);
            var fifth = SessionAdmission.Evaluate(payload, HostConfig(), 4, true);
            Assert.IsFalse(fifth.Approved);
            Assert.AreEqual(SessionError.SessionFull, fifth.Reason);
        }

        [Test]
        public void Admission_MaxPlayersIsClampedToFour()
        {
            var payload = ConnectionPayload.From(HostConfig(8)).Encode();
            Assert.AreEqual(SessionError.SessionFull, SessionAdmission.Evaluate(payload, HostConfig(8), 4, true).Reason);
            Assert.AreEqual(1, SessionAdmission.ClampMaxPlayers(0));
        }

        [Test]
        public void Admission_RejectsWhenHostIsShuttingDown()
        {
            var payload = ConnectionPayload.From(HostConfig()).Encode();
            Assert.AreEqual(SessionError.HostEnded, SessionAdmission.Evaluate(payload, HostConfig(), 1, false).Reason);
        }

        [TestCase(SessionError.SessionFull)]
        [TestCase(SessionError.VersionMismatch)]
        public void ReasonCodec_RoundTrips(SessionError reason)
        {
            Assert.AreEqual(reason, AdmissionReasonCodec.Decode(AdmissionReasonCodec.Encode(reason), SessionError.Unknown));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Disconnected by server")]
        [TestCase("A5:NotAnError")]
        public void ReasonCodec_UnknownTextUsesFallback(string reason)
        {
            Assert.AreEqual(SessionError.ConnectionLost, AdmissionReasonCodec.Decode(reason, SessionError.ConnectionLost));
        }

        [TestCase("abc123", "ABC123")]
        [TestCase("  ab-c1 23 ", "ABC123")]
        public void ServiceCode_IsNormalised(string raw, string expected)
        {
            string code;
            Assert.IsTrue(JoinCodes.TryNormalizeServiceCode(raw, out code));
            Assert.AreEqual(expected, code);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("AB1")]
        [TestCase("ABCDEFGHIJKLMNOPQ")]
        [TestCase("ABC12!")]
        [TestCase("ÄBC123")]
        public void ServiceCode_RejectsInvalid(string raw)
        {
            string code;
            Assert.IsFalse(JoinCodes.TryNormalizeServiceCode(raw, out code));
        }

        [TestCase("127.0.0.1:7777", "127.0.0.1", 7777)]
        [TestCase(" 192.168.0.12 ", "192.168.0.12", 7777)]
        [TestCase("localhost:9000", "127.0.0.1", 9000)]
        public void DirectCode_Parses(string raw, string expectedAddress, int expectedPort)
        {
            string address;
            ushort port;
            Assert.IsTrue(JoinCodes.TryParseDirect(raw, 7777, out address, out port));
            Assert.AreEqual(expectedAddress, address);
            Assert.AreEqual(expectedPort, port);
        }

        [TestCase("")]
        [TestCase("300.1.1.1")]
        [TestCase("1.2.3")]
        [TestCase("127.0.0.1:0")]
        [TestCase("127.0.0.1:70000")]
        [TestCase("example.com:7777")]
        public void DirectCode_RejectsInvalid(string raw)
        {
            string address;
            ushort port;
            Assert.IsFalse(JoinCodes.TryParseDirect(raw, 7777, out address, out port));
        }
    }
}
