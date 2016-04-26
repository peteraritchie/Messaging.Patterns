using System.Messaging;
using NUnit.Framework;
using PRI.Messaging.Patterns;
using PRI.Messaging.Primitives;
using Tests.Mocks;

namespace Tests
{
	// docs on:
	//	creating an in-memory bus ManuallyComposingBus
	//	creating an in-memory bus that discovers consumers CompsingBusByDiscovery
	//	creating a queue reader QueueReaderTest
	//	creating a queue reader that discovers consumers?
	[TestFixture]
    public class ManuallyComposingBus
    {
		public ManuallyComposingBus()
		{
			Pipe.LastMessageProcessed = null;
		}

		[Test]
		public void ManuallyComposedTypeHandlesMessageProperly()
		{
			var message1 = new Message1 { CorrelationId = "1234" };

			var bus = new Bus();

			var message2Consumer = new Message2Consumer();
			bus.AddHandler(message2Consumer);
			
			var pipe = new Pipe();
			bus.AddTranslator(pipe);

			bus.Handle(message1);

			Assert.AreSame(message1, Pipe.LastMessageProcessed);
			Assert.IsNotNull(Message2Consumer.LastMessageReceived);
			Assert.AreEqual(message1.CorrelationId, Message2Consumer.LastMessageReceived.CorrelationId);
		}

		[Test]
		public void ManuallyComposedWithTranslatorFirstTypeHandlesMessageProperly()
		{
			var message1 = new Message1 { CorrelationId = "1234" };

			var bus = new Bus();

			var pipe = new Pipe();
			bus.AddTranslator(pipe);

			var message2Consumer = new Message2Consumer();
			bus.AddHandler(message2Consumer);

			bus.Handle(message1);

			Assert.AreSame(message1, Pipe.LastMessageProcessed);
			Assert.IsNotNull(Message2Consumer.LastMessageReceived);
			Assert.AreEqual(message1.CorrelationId, Message2Consumer.LastMessageReceived.CorrelationId);
		}

		public class Message3 : PRI.Messaging.Primitives.IMessage
		{
			public string CorrelationId { get; set; }
		}

		public class Message4 : Message3
		{
		}

		public class MyConsumer : IConsumer<Message2>
		{
			public void Handle(Message2 message)
			{
			}
		}

		[Test]
		public void ManuallyComposedTypeWithMultipleTranslatorsHandlesMessageProperly()
		{
			var message3 = new Message3 { CorrelationId = "1234" };

			var bus = new Bus();
			Message4 message4 = null;
			var message2Consumer = new MyConsumer();
			bus.AddHandler(message2Consumer);
			bus.AddHandler(new ActionConsumer<Message4>(m=>message4 = m));
			var pipe = new Pipe();
			bus.AddTranslator(pipe);
			bus.AddTranslator(new ActionPipe<Message3, Message4>(m=>new Message4 {CorrelationId = m.CorrelationId}));

			bus.Handle(message3);

			Assert.IsNotNull(message4);
			Assert.AreEqual(message3.CorrelationId, message4.CorrelationId);
			Assert.IsNotNull(Pipe.LastMessageProcessed);
			Assert.AreEqual(message3.CorrelationId, message4.CorrelationId);
		}

		[Test]
		public void ManuallyComposedTypeHandlesWithDuplicateHandlerMulticasts()
		{
			var bus = new Bus();

			var message2Consumer = new Message2Consumer();
			bus.AddHandler(message2Consumer);
			bus.AddHandler(message2Consumer);

			var message1 = new Message2 { CorrelationId = "1234" };
			bus.Handle(message1);
			Assert.AreEqual(2, message2Consumer.MessageReceivedCount);
		}

		[Test]
		public void ManuallyComposedTypeHandlesWithRemovedDuplicateHandlerDoesNotMulticasts()
		{
			var bus = new Bus();

			var message2Consumer = new Message2Consumer();
			bus.AddHandler(message2Consumer);
			bus.AddHandler(message2Consumer);
			bus.RemoveHandler(message2Consumer);

			var message1 = new Message2 { CorrelationId = "1234" };
			bus.Handle(message1);
			Assert.AreEqual(1, message2Consumer.MessageReceivedCount);
		}

		[Test]
		public void RemoveHandlerWithUnknownHandlerDoesNotFail()
		{
			var bus = new Bus();

			var message2Consumer = new Message2Consumer();
			bus.AddHandler(message2Consumer);
			bus.RemoveHandler(new ActionConsumer<Message1>(_ => { }));
		}
	}
}
