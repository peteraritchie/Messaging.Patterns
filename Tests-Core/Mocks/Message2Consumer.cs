using PRI.Messaging.Primitives;

namespace Tests.Mocks
{
	public class Message2Consumer : IConsumer<Message2>
	{
		public static Message2 LastMessageReceived { get; private set; }
		public int MessageReceivedCount { get; private set; }
		public void Handle(Message2 message)
		{
			LastMessageReceived = message;
			++MessageReceivedCount;
		}
	}
}