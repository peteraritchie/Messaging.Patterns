using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Patterns.Extensions.Consumer;
using PRI.Messaging.Primitives;

#pragma warning disable S1104 // Fields should not have public accessibility
namespace Tests
{
	[TestFixture]
	public class Messaging
	{
		private IBus bus;
		private List<IMessage> messages;

		public class TestBus : Bus
		{
			public new IMessage Handle(IMessage message)
			{
				bool wasProcessed;
				base.Handle(message, out wasProcessed);
				return wasProcessed
					? message
					: null;
			}
		}

		[SetUp]
		public void SetUp()
		{
			bus = new TestBus();
			messages = new List<IMessage>();
			bus.AddHandler(new ActionConsumer<IMessage>(message =>
			{
				messages.Add(message);
			}));
		}

		public class MyEvent : IEvent
		{
			public MyEvent()
			{
				CorrelationId = Guid.NewGuid().ToString("D");
				OccurredDateTime = DateTime.UtcNow;
			}

			public string CorrelationId { get; set; }
			public DateTime OccurredDateTime { get; set; }
		}

		public class MyMessage : IMessage
		{
			public MyMessage()
			{
				CorrelationId = Guid.NewGuid().ToString("D");
			}

			public string CorrelationId { get; set; }
		}

		public class MessageHandler : IConsumer<MyMessage>
		{
			public IMessage LastMessageReceived;
			public void Handle(MyMessage message)
			{
				LastMessageReceived = message;
			}
		}

		public class EventHandler : IConsumer<MyEvent>
		{
			public IMessage LastMessageReceived;
			public void Handle(MyEvent message)
			{
				LastMessageReceived = message;
			}
		}

		[Test]
		public void ConsumerPublishSucceeds()
		{
			IConsumer<MyEvent> consumer = new EventHandler();

			var myEvent = new MyEvent();
			consumer.Publish(myEvent);
			Assert.AreEqual(myEvent.CorrelationId, ((EventHandler)consumer).LastMessageReceived.CorrelationId);
		}

		[Test]
		public void ConsumerSendSucceeds()
		{
			IConsumer<MyMessage> consumer = new MessageHandler();

			var myEvent = new MyMessage();
			consumer.Send(myEvent);
			Assert.AreEqual(myEvent.CorrelationId, ((MessageHandler)consumer).LastMessageReceived.CorrelationId);
		}

		[Test]
		public void SendingMessageSends()
		{
			var myMessage = new MyMessage();
			bus.Send(myMessage);
			Assert.IsTrue(messages.Any(e=>myMessage.CorrelationId.Equals(e.CorrelationId)));
		}

		[Test]
		public void SendingMessageResultsInMessageProcessedEvent()
		{
			var myMessage = new MyMessage();

			var messageProcessed =  ((TestBus)bus).Handle(myMessage);
			Assert.IsNotNull(messageProcessed);
		}

		[Test]
		public void PublishingEventResultsInMessageProcessedEvent()
		{
			var myEvent = new MyEvent();

			var messageProcessed =  ((TestBus)bus).Handle(myEvent);
			Assert.IsNotNull(messageProcessed);
		}

		[Test]
		public void SendingMessageResultsInCorrectMessageProcessedEvent()
		{
			var myMessage = new MyMessage();

			var messageProcessed =  ((TestBus)bus).Handle(myMessage);
			Assert.AreSame(myMessage, messageProcessed);
		}

		[Test]
		public void PublishingEventResultsInCorrectMessageProcessedEvent()
		{
			var myEvent = new MyEvent();

			var messageProcessed =  ((TestBus)bus).Handle(myEvent);
			Assert.AreSame(myEvent, messageProcessed);
		}

		[Test]
		public void PublishingEventPublishes()
		{
			var myEvent = new MyEvent();
			bus.Publish(myEvent);
			Assert.IsTrue(messages.Any(e => myEvent.CorrelationId.Equals(e.CorrelationId)));
		}
	}
}
#pragma warning restore S1104 // Fields should not have public accessibility
