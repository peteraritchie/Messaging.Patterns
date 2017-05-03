using System.Threading.Tasks;
using NUnit.Framework;
using PRI.Messaging.Patterns;
using PRI.Messaging.Primitives;
using Tests.Mocks;

#pragma warning disable S1481 // Unused local variables should be removed
#pragma warning disable S1186 // Methods should not be empty: TESTS
#pragma warning disable S3010 // Static fields should not be updated in constructors
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
		public void ManuallyComposedTypeSynchronouslyHandlesMessageProperly()
		{
			var message1 = new Message1 {CorrelationId = "1234"};

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

#if SUPPORT_ASYNC_CONSUMER
		[Test]
		public void ManuallyComposedSyncAsyncTypeSynchronouslyHandlesMessageWithAsyncConsumerProperly()
		{
			var message2 = new Message2 {CorrelationId = "1234"};

			var bus = new Bus();

			var message3Consumer = new Message3AsyncConsumer();
			bus.AddHandler(message3Consumer);

			var pipe = new Pipe23();
			bus.AddTranslator(pipe);

			bus.Handle(message2);

			Assert.AreSame(message2, Pipe23.LastMessageProcessed);
			Assert.IsNotNull(Message3AsyncConsumer.LastMessageReceived);
			Assert.AreEqual(message2.CorrelationId, Message3AsyncConsumer.LastMessageReceived.CorrelationId);
		}

		[Test]
		public async Task ManuallyComposedSyncAsyncTypeAsynchronouslyHandlesMessageProperly()
		{
			var message2 = new Message2 { CorrelationId = "1234" };

			var bus = new Bus();

			var message3AsyncConsumer = new Message3AsyncConsumer();
			bus.AddHandler(message3AsyncConsumer);

			var pipe = new Pipe23();
			bus.AddTranslator(pipe);

			await bus.HandleAsync(message2);

			Assert.AreSame(message2, Pipe23.LastMessageProcessed);
			Assert.IsNotNull(Message3AsyncConsumer.LastMessageReceived);
			Assert.AreEqual(message2.CorrelationId, Message3AsyncConsumer.LastMessageReceived.CorrelationId);
		}

		[Test]
		public async Task ManuallyComposedAsyncTypeAsynchronouslyHandlesMessageProperly()
		{
			var message3 = new Message3 { CorrelationId = "1234" };

			var bus = new Bus();

			var message3AsyncConsumer = new Message3AsyncConsumer();
			bus.AddHandler(message3AsyncConsumer);

			await bus.HandleAsync(message3);

			Assert.IsNotNull(Message3AsyncConsumer.LastMessageReceived);
			Assert.AreEqual(message3.CorrelationId, Message3AsyncConsumer.LastMessageReceived.CorrelationId);
		}
#endif

		[Test]
		public void ManuallyComposedWithTranslatorFirstTypeHandlesMessageProperly()
		{
			var message1 = new Message1 {CorrelationId = "1234"};

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

		[Test]
		public void ManuallyComposedWithTwoTranslatorsFirstTypeHandlesMessageProperly()
		{
			var message1 = new Message1 { CorrelationId = "1234" };

			var bus = new Bus();

			var pipe1 = new Pipe();
			bus.AddTranslator(pipe1);
			var pipe2 = new Pipe();
			bus.AddTranslator(pipe1);

			var message2Consumer = new Message2Consumer();
			bus.AddHandler(message2Consumer);

			bus.Handle(message1);

			Assert.AreSame(message1, Pipe.LastMessageProcessed);
			Assert.IsNotNull(Message2Consumer.LastMessageReceived);
			Assert.AreEqual(message1.CorrelationId, Message2Consumer.LastMessageReceived.CorrelationId);
		}

		public class Message4 : PRI.Messaging.Primitives.IMessage
		{
			public string CorrelationId { get; set; }
		}

		public class Message5 : Message4
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
			var message4 = new Message4 {CorrelationId = "1234"};

			var bus = new Bus();
			Message5 message5 = null;
			var message2Consumer = new MyConsumer();
			bus.AddHandler(message2Consumer);
			bus.AddHandler(new ActionConsumer<Message5>(m => message5 = m));
			var pipe = new Pipe();
			bus.AddTranslator(pipe);
			bus.AddTranslator(new ActionPipe<Message4, Message5>(m => new Message5 {CorrelationId = m.CorrelationId}));

			bus.Handle(message4);

			Assert.IsNotNull(message5);
			Assert.AreEqual(message4.CorrelationId, message5.CorrelationId);
			Assert.IsNotNull(Pipe.LastMessageProcessed);
			Assert.AreEqual(message4.CorrelationId, message5.CorrelationId);
		}

		[Test]
		public void ManuallyComposedTypeHandlesWithDuplicateHandlerMulticasts()
		{
			var bus = new Bus();

			var message2Consumer = new Message2Consumer();
			bus.AddHandler(message2Consumer);
			bus.AddHandler(message2Consumer);

			var message1 = new Message2 {CorrelationId = "1234"};
			bus.Handle(message1);
			Assert.AreEqual(2, message2Consumer.MessageReceivedCount);
		}

#if !PARANOID
		[Test]
		public void ManuallyComposedTypeHandlesWithRemovedDuplicateHandlerDoesNotMulticasts()
		{
			var bus = new Bus();

			var message2Consumer = new Message2Consumer();
			var token1 = bus.AddHandler(message2Consumer);
			var token2 = bus.AddHandler(message2Consumer);
			bus.RemoveHandler(message2Consumer, token2);

			var message1 = new Message2 {CorrelationId = "1234"};
			bus.Handle(message1);
			Assert.AreEqual(1, message2Consumer.MessageReceivedCount);
		}
#endif
	}
}
#pragma warning restore S1186 // Methods should not be empty: TESTS
#pragma warning restore S3010 // Static fields should not be updated in constructors
#pragma warning restore S1481 // Unused local variables should be removed
