#if (NET45)
using System.Messaging;

namespace PRI.Messaging.Patterns.Internal
{
	public class MessageQueueMessageWrapper
	{
		private readonly Message _message;

		public MessageQueueMessageWrapper(Message message)
		{
			_message = message;
		}
		public Message Message { get {return _message;} }
	}
}
#endif