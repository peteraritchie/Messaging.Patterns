using System;
using PRI.Messaging.Primitives;

namespace PRI.Messaging.Patterns
{
	public class MessageProcessedEventArgs : EventArgs
	{
		public MessageProcessedEventArgs(IMessage message)
		{
			Message = message;
		}

		public IMessage Message { get; }
	}
}