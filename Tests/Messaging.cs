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
		private readonly IBus bus;
		private readonly List<IMessage> messages = new List<IMessage>();

		public Messaging()
		{
			bus = new Bus();
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
				OccurreDateTime = DateTime.UtcNow;
			}

			public string CorrelationId { get; set; }
			public DateTime OccurreDateTime { get; set; }
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
		public void PublishingEventPublishes()
		{
			var myEvent = new MyEvent();
			bus.Publish(myEvent);
			Assert.IsTrue(messages.Any(e => myEvent.CorrelationId.Equals(e.CorrelationId)));
		}
	}
}