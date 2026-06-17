using NUnit.Framework;
using ChemLabSimV3.Core;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Tests.EditMode.Core
{
    /// <summary>
    /// Tests for the EventBus system — ensures pub/sub works correctly.
    /// </summary>
    public class EventBusTests
    {
        [SetUp]
        public void Setup()
        {
            EventBus.Clear();
        }

        [TearDown]
        public void Teardown()
        {
            EventBus.Clear();
        }

        // ── Test event types ──

        public class TestEvent
        {
            public string Data { get; set; }
        }

        public class AnotherEvent
        {
            public int Value { get; set; }
        }

        // ── Tests ──

        [Test]
        public void SubscribeAndPublish_ReceivesEvent()
        {
            string received = null;
            EventBus.Subscribe<TestEvent>(evt => received = evt.Data);

            EventBus.Publish(new TestEvent { Data = "hello" });

            Assert.That(received, Is.EqualTo("hello"));
        }

        [Test]
        public void MultipleSubscribers_AllReceiveEvent()
        {
            int count = 0;
            EventBus.Subscribe<TestEvent>(_ => count++);
            EventBus.Subscribe<TestEvent>(_ => count++);

            EventBus.Publish(new TestEvent());

            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public void UnrelatedEvents_DoNotTriggerSubscriber()
        {
            bool triggered = false;
            EventBus.Subscribe<TestEvent>(_ => triggered = true);

            EventBus.Publish(new AnotherEvent { Value = 42 });

            Assert.That(triggered, Is.False);
        }

        [Test]
        public void UnsubscribedHandler_DoesNotReceiveEvent()
        {
            int callCount = 0;
            System.Action<TestEvent> handler = _ => callCount++;
            EventBus.Subscribe(handler);

            EventBus.Publish(new TestEvent());
            Assert.That(callCount, Is.EqualTo(1));

            EventBus.Unsubscribe(handler);

            EventBus.Publish(new TestEvent());
            Assert.That(callCount, Is.EqualTo(1)); // still 1, not 2
        }

        [Test]
        public void Clear_RemovesAllSubscribers()
        {
            int count = 0;
            EventBus.Subscribe<TestEvent>(_ => count++);

            EventBus.Clear();
            EventBus.Publish(new TestEvent());

            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void SubscribeWithNullHandler_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => EventBus.Subscribe<TestEvent>(null));
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => EventBus.Publish(new TestEvent()));
        }

        [Test]
        public void MultipleEventTypes_WorkIndependently()
        {
            string testData = null;
            int anotherValue = 0;

            EventBus.Subscribe<TestEvent>(evt => testData = evt.Data);
            EventBus.Subscribe<AnotherEvent>(evt => anotherValue = evt.Value);

            EventBus.Publish(new TestEvent { Data = "test" });
            EventBus.Publish(new AnotherEvent { Value = 99 });

            Assert.Multiple(() =>
            {
                Assert.That(testData, Is.EqualTo("test"));
                Assert.That(anotherValue, Is.EqualTo(99));
            });
        }
    }
}
