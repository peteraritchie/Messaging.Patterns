using System;
using System.IO;
using NUnit.Framework;
using PRI.Messaging.Patterns;
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
	}
}