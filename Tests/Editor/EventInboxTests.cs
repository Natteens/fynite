using Fynite;
using NUnit.Framework;

namespace FyniteTests
{
    /// <summary>
    /// The inbox is subscribed exactly while it is open. Opening it a second time would leave the
    /// same machine listening to the same source twice, so it is refused instead of counted.
    /// </summary>
    public sealed class EventInboxTests
    {
        [Test]
        public void OpeningTwiceIsRejected()
        {
            var source = new FyniteEvent();
            var inbox = new FyniteEventInbox(new[] { source });

            inbox.Open();

            Assert.That(() => inbox.Open(), Throws.InvalidOperationException);
            Assert.That(source.SubscriberCount, Is.EqualTo(1));

            inbox.Close();
            Assert.That(source.SubscriberCount, Is.Zero);
        }

        [Test]
        public void OpeningAgainAfterCloseIsAllowed()
        {
            var source = new FyniteEvent();
            var inbox = new FyniteEventInbox(new[] { source });

            inbox.Open();
            inbox.Close();
            inbox.Open();

            Assert.That(source.SubscriberCount, Is.EqualTo(1));

            inbox.Close();
        }

        [Test]
        public void CloseIsSafeToRepeatAndSafeWithoutAnOpen()
        {
            var source = new FyniteEvent();
            var inbox = new FyniteEventInbox(new[] { source });

            inbox.Close();
            inbox.Open();
            inbox.Close();
            inbox.Close();

            Assert.That(source.SubscriberCount, Is.Zero);
        }

        [Test]
        public void AMachineSubscribesToEachSourceExactlyOnce()
        {
            var source = new FyniteEvent();
            var inbox = new FyniteEventInbox(new[] { source });

            inbox.Open();
            source.Publish();
            source.Publish();

            Assert.That(inbox.PendingCount, Is.EqualTo(1));

            inbox.Close();
        }
    }
}
