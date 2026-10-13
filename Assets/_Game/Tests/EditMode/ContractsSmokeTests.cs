using Avatar5.Contracts;
using Avatar5.Gameplay;
using NUnit.Framework;

namespace Avatar5.Tests.EditMode
{
    public sealed class ContractsSmokeTests
    {
        [Test]
        public void NetworkProtocol_AcceptsMatchingVersion()
        {
            Assert.That(VersionContracts.CheckNetworkProtocol(VersionContracts.NetworkProtocolVersion),
                Is.EqualTo(RequestStatus.Accepted));
        }

        [Test]
        public void NetworkProtocol_RejectsMismatchedVersion()
        {
            Assert.That(VersionContracts.CheckNetworkProtocol(VersionContracts.NetworkProtocolVersion + 1),
                Is.EqualTo(RequestStatus.RejectedVersion));
        }

        [Test]
        public void DuplicateCommands_AreRejectedForSameActor()
        {
            var gate = new SequenceDeduplicator();
            Assert.That(gate.TryAccept(42, 100), Is.True);
            Assert.That(gate.TryAccept(42, 100), Is.False);
            Assert.That(gate.TryAccept(42, 99), Is.False);
            Assert.That(gate.TryAccept(42, 101), Is.True);
        }

        [Test]
        public void CommandSequences_AreIndependentPerActor()
        {
            var gate = new SequenceDeduplicator();
            Assert.That(gate.TryAccept(1, 3), Is.True);
            Assert.That(gate.TryAccept(2, 3), Is.True);
            Assert.That(gate.TryAccept(1, 3), Is.False);
            Assert.That(gate.RemoveActor(1), Is.True);
            Assert.That(gate.TryAccept(1, 3), Is.True);
        }
    }
}
