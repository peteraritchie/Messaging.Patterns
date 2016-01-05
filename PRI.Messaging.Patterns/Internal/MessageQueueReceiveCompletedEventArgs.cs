using System;

namespace PRI.Messaging.Patterns.Internal
{
	public class MessageQueueReceiveCompletedEventArgs : EventArgs
	{
		public MessageQueueReceiveCompletedEventArgs(IAsyncResult asyncResult)
		{
			AsyncResult = asyncResult;
		}

		public IAsyncResult AsyncResult { get; private set; }
	}
}