using System;
using System.IO;
using NUnit.Framework;
using PRI.Messaging.Patterns;
using PRI.Messaging.Primitives;
using Tests.Mocks;

namespace Tests
{
	[TestFixture]
	public class BusTests
	{
		[Test]
		public void BusConsumesMessagesCorrectly()
		{
			var bus = new Bus();
			var directory = Path.GetDirectoryName(new Uri(GetType().Assembly.CodeBase).LocalPath);
			Message1 receivedMessage = null;
			bus.AddHandler(new ActionConsumer<Message1>(m=>receivedMessage = m));

			var message1 = new Message1 { CorrelationId = "1234" };
			bus.Handle(message1);

			Assert.AreSame(message1, receivedMessage);
			Assert.IsNotNull(receivedMessage);
			Assert.AreEqual(message1.CorrelationId, receivedMessage.CorrelationId);
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

		[Test]
		public void EnsureInterfaceHandlerIsInvoked()
		{
			var bus = new Bus();
			Message1 receivedMessage = null;
			bus.AddHandler(new ActionConsumer<Message1>(m => receivedMessage = m));
			string text = null;
			bus.AddHandler(new ActionConsumer<IEvent>(_=> { text = "ding"; }));

			var message1 = new Message1 { CorrelationId = "1234" };
			bus.Handle(message1);
			bus.Handle(new MyEvent());
			Assert.AreSame(message1, receivedMessage);
			Assert.IsNotNull(receivedMessage);
			Assert.AreEqual(message1.CorrelationId, receivedMessage.CorrelationId);
			Assert.AreEqual("ding", text);
		}
	}
}