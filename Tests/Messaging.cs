using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;

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
				IMessage messageProcessed = null;
				EventHandler<MessageProcessedEventArgs> eventHandler = (sender, args) => { messageProcessed = args.Message; };
				MessageProcessed += eventHandler;
				base.Handle(message);
				MessageProcessed -= eventHandler;
				return messageProcessed;
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