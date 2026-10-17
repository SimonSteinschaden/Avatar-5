using Avatar5.Contracts;
using Avatar5.Gameplay.Session;
using NUnit.Framework;

namespace Avatar5.Tests.EditMode
{
    public sealed class HostCommandIngressTests
    {
        private static PlayerCommand Command(ulong claimedActor, uint sequence, float moveX = 1f, PlayerActionKind action = PlayerActionKind.None)
        {
            return new PlayerCommand { ActorId = claimedActor, SequenceId = sequence, Move = new Float2(moveX, 0f), Action = action };
        }

        [Test]
        public void ActorIds_HostClientControlsActorOne()
        {
            Assert.AreEqual(1UL, ActorIds.ForClient(0));
            Assert.AreEqual(4UL, ActorIds.ForClient(3));
        }

        [Test]
        public void Ingress_AcceptsOwnerAndStampsAuthoritativeActorId()
        {
            var ingress = new HostCommandIngress(2);
            var command = Command(999, 1);
            Assert.AreEqual(RequestStatus.Accepted, ingress.Admit(2, ref command, 0d));
            Assert.AreEqual(3UL, command.ActorId, "client-chosen actor id must be overwritten");
        }

        [Test]
        public void Ingress_RejectsForeignSenderEvenWithMatchingActorId()
        {
            var ingress = new HostCommandIngress(2);
            var command = Command(ActorIds.ForClient(2), 1);
            Assert.AreEqual(RequestStatus.RejectedNoAuthority, ingress.Admit(1, ref command, 0d));
            Assert.AreEqual(1, ingress.RejectedNoAuthority);
        }

        [Test]
        public void Ingress_RateLimitsFloodAndRecovers()
        {
            var ingress = new HostCommandIngress(1, 10f, 5f);
            var accepted = 0;
            for (uint i = 1; i <= 50; i++)
            {
                var command = Command(0, i);
                if (ingress.Admit(1, ref command, 0d) == RequestStatus.Accepted) accepted++;
            }
            Assert.AreEqual(5, accepted, "only the burst passes at one instant");
            Assert.AreEqual(45, ingress.RejectedRateLimited);

            var later = Command(0, 51);
            Assert.AreEqual(RequestStatus.Accepted, ingress.Admit(1, ref later, 1d), "tokens refill over time");
        }

        [Test]
        public void RateLimiter_SustainedRateMatchesConfiguration()
        {
            var limiter = new CommandRateLimiter(20f, 2f);
            var accepted = 0;
            for (var frame = 0; frame < 600; frame++) // 10 s at 60 Hz
            {
                if (limiter.TryConsume(frame / 60d)) accepted++;
            }
            Assert.That(accepted, Is.InRange(199, 203));
        }

        [Test]
        public void Throttle_SendsActionEdgesImmediately()
        {
            var throttle = new CommandSendThrottle(0.1f, 1f);
            Assert.IsTrue(throttle.ShouldSend(Command(1, 1), 0d));
            Assert.IsTrue(throttle.ShouldSend(Command(1, 2, 1f, PlayerActionKind.Cast), 0.001d));
        }

        [Test]
        public void Throttle_UnchangedIntentOnlyAsHeartbeat()
        {
            var throttle = new CommandSendThrottle(0.1f, 1f);
            Assert.IsTrue(throttle.ShouldSend(Command(1, 1), 0d));
            Assert.IsFalse(throttle.ShouldSend(Command(1, 2), 0.5d));
            Assert.IsTrue(throttle.ShouldSend(Command(1, 3), 1.0d));
        }

        [Test]
        public void Throttle_ChangedIntentWaitsForMinInterval()
        {
            var throttle = new CommandSendThrottle(0.1f, 1f);
            Assert.IsTrue(throttle.ShouldSend(Command(1, 1, 0f), 0d));
            Assert.IsFalse(throttle.ShouldSend(Command(1, 2, 1f), 0.05d));
            Assert.IsTrue(throttle.ShouldSend(Command(1, 3, 1f), 0.11d), "pending change goes out once the interval passed");
        }

        [Test]
        public void Throttle_144HzClientStaysUnderHostLimit()
        {
            var throttle = new CommandSendThrottle();
            var sent = 0;
            for (var frame = 0; frame < 144 * 10; frame++)
            {
                var wobble = (frame % 2 == 0) ? 0.5f : -0.5f; // intent changes every frame
                if (throttle.ShouldSend(Command(1, (uint)frame + 1, wobble), frame / 144d)) sent++;
            }
            Assert.Less(sent / 10f, HostCommandIngress.DefaultRatePerSecond);
        }
    }
}
