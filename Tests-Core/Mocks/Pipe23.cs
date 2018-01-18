using System;
using PRI.Messaging.Primitives;

namespace Tests.Mocks
{
	public class Pipe23 : IPipe<Message2, Message3>
	{
		private Action<Message3> _consumer;
		internal static IMessage LastMessageProcessed;

		public void Handle(Message2 message)
		{
			LastMessageProcessed = message;
			_consumer(new Message3 { CorrelationId = message.CorrelationId });
		}

		public void AttachConsumer(IConsumer<Message3> consumer)
		{
			AttachConsumer(consumer.Handle);
		}

		public void AttachConsumer(Action<Message3> consumer)
		{
			_consumer = consumer;
		}
	}
}