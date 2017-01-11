using System;
using System.Threading.Tasks;
using PRI.Messaging.Primitives;

namespace Tests.Mocks
{
	public class Message3AsyncConsumer : IAsyncConsumer<Message3>
	{
		private bool invokedAsynchronously;
		public static Message3 LastMessageReceived { get; private set; }
		public int MessageReceivedCount { get; private set; }

		public bool InvokedAsynchronously
		{
			get
			{
				if (LastMessageReceived == null) throw new InvalidOperationException();
				return invokedAsynchronously;
			}
		}

		public void Handle(Message3 message)
		{
			LastMessageReceived = message;
			++MessageReceivedCount;
		}

		public Task HandleAsync(Message3 message)
		{
			invokedAsynchronously = true;
			LastMessageReceived = message;
			++MessageReceivedCount;
			return Task.FromResult(true);
		}
	}
}