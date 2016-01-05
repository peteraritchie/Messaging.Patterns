using NUnit.Framework;
using PRI.Messaging.Patterns;
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

    }
}
