using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Extensions.Bus;
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

		[Test]
		public void MultipleAsyncHandlersOfSameMessageDoesntThrow()
		{
			using (var bus = new Bus())
			{
				bus.AddHandler(new AsyncActionConsumer<Message1>(m => Task.FromResult(0)));
				bus.AddHandler(new AsyncActionConsumer<Message1>(m => Task.FromResult(1)));
			}
		}

		public class TheEvent : IEvent
		{
			public TheEvent()
			{
				CorrelationId = Guid.NewGuid().ToString("D");
				OccurredDateTime = DateTime.UtcNow;
			}

			public string CorrelationId { get; set; }
			public DateTime OccurredDateTime { get; set; }
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
			bus.Handle(new TheEvent());
			Assert.AreSame(message1, receivedMessage);
			Assert.IsNotNull(receivedMessage);
			Assert.AreEqual(message1.CorrelationId, receivedMessage.CorrelationId);
			Assert.AreEqual("ding", text);
		}

		[Test]
		public void EnsureWithMultipleMessageTypesInterfaceHandlerIsInvoked()
		{
			var bus = new Bus();
			Message1 receivedMessage1 = null;
			bus.AddHandler(new ActionConsumer<Message1>(m => receivedMessage1 = m));
			Message2 receivedMessage2 = null;
			bus.AddHandler(new ActionConsumer<Message2>(m => receivedMessage2 = m));
			string text = null;
			bus.AddHandler(new ActionConsumer<IEvent>(_ => { text = "ding"; }));

			var message1 = new Message1 { CorrelationId = "1234" };
			bus.Handle(message1);
			bus.Handle(new TheEvent());
			Assert.AreSame(message1, receivedMessage1);
			Assert.IsNotNull(receivedMessage1);
			Assert.AreEqual(message1.CorrelationId, receivedMessage1.CorrelationId);
			Assert.AreEqual("ding", text);
		}

		public class Message1Specialization : Message1
		{
		}

		[Test]
		public void BaseTypeHandlerIsCalledCorrectly()
		{
			var bus = new Bus();
			Message1 receivedMessage1 = null;
			bus.AddHandler(new ActionConsumer<Message1>(m => receivedMessage1 = m));
			var message1 = new Message1Specialization { CorrelationId = "1234" };
			bus.Handle(message1);
			Assert.AreSame(message1, receivedMessage1);
		}

		public class Message1SpecializationSpecialization : Message1Specialization
		{
		}

		[Test]
		public void BaseBaseTypeHandlerIsCalledCorrectly()
		{
			var bus = new Bus();
			Message1 receivedMessage1 = null;
			bus.AddHandler(new ActionConsumer<Message1>(m => receivedMessage1 = m));
			var message1 = new Message1SpecializationSpecialization() { CorrelationId = "1234" };
			bus.Handle(message1);
			Assert.AreSame(message1, receivedMessage1);
		}

		[Test]
		public void RemoveUnsubscribedHandlerDoesNotThrow()
		{
			var bus = new Bus();
			Message1 receivedMessage1 = null;
			bus.RemoveHandler(new ActionConsumer<Message1>(m => receivedMessage1 = m));
		}

		[Test]
		public void RemoveLastSubscribedHandlerDoesNotThrow()
		{
			var bus = new Bus();
			Message1 receivedMessage1 = null;
			bus.AddHandler(new ActionConsumer<Message1>(m => receivedMessage1 = m));
			bus.RemoveHandler(new ActionConsumer<Message1>(m => receivedMessage1 = m));
		}

		[Test]
		public void InterleavedRemoveHandlerRemovesCorrectHandler()
		{
			string ordinal = null;
			var bus = new Bus();
			var actionConsumer1 = new ActionConsumer<Message1>(m => ordinal = "1");
			var token1 = bus.AddHandler(actionConsumer1);
			var actionConsumer2 = new ActionConsumer<Message1>(m => ordinal = "2");
			var token2 = bus.AddHandler(actionConsumer2);
			bus.RemoveHandler(actionConsumer1, token1);
			bus.Send(new Message1());
			Assert.AreEqual("2", ordinal);
		}

		[Test]
		public void RemoveHandlerWithNullTokenThrows()
		{
			var bus = new Bus();
			Assert.Throws<InvalidOperationException>(() => bus.RemoveHandler(new ActionConsumer<Message1>(m => { }), null));
		}

		[Test]
		public void RemoveHandlerTwiceSucceeds()
		{
			var bus = new Bus();
			var actionConsumer1 = new ActionConsumer<Message1>(m => { });
			var token1 = bus.AddHandler(actionConsumer1);
			bus.RemoveHandler(actionConsumer1, token1);
			bus.RemoveHandler(actionConsumer1, token1);
		}
	}
}