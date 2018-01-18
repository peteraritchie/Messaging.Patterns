#if (NET45)
using System;

namespace PRI.Messaging.Patterns.Internal
{
	/// <summary>
	/// interface for decoupling a System.Messaging.MessageQueue object
	/// </summary>
	public interface IMessageQueue : IDisposable
	{
		event EventHandler<MessageQueueReceiveCompletedEventArgs> ReceiveCompleted;
		void BeginReceive(TimeSpan timeout);
		MessageQueueMessageWrapper EndReceive(IAsyncResult asyncResult);

	}
}
#endif