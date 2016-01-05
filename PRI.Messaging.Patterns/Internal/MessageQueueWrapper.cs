using System;
using System.Messaging;

namespace PRI.Messaging.Patterns.Internal
{
	internal class MessageQueueWrapper : IMessageQueue
	{
		private readonly MessageQueue _messageQueue;
		private EventHandler<MessageQueueReceiveCompletedEventArgs> _receiveCompletedDelegate;
		private readonly uint STATUS_CANCELLED = 0xc0000120;

		public MessageQueueWrapper(MessageQueue messageQueue)
		{
			if (messageQueue == null) throw new ArgumentNullException("messageQueue");
			_messageQueue = messageQueue;
		}

		public void Dispose()
		{
			_messageQueue.Dispose();
		}

		public event EventHandler<MessageQueueReceiveCompletedEventArgs> ReceiveCompleted
		{
			add
			{
				_receiveCompletedDelegate = value;
				_messageQueue.ReceiveCompleted +=_messageQueue_ReceiveCompleted;
			}
			remove
			{
				_messageQueue.ReceiveCompleted -= _messageQueue_ReceiveCompleted;
				_receiveCompletedDelegate = null;
			}
		}

		void _messageQueue_ReceiveCompleted(object sender, ReceiveCompletedEventArgs e)
		{
			try
			{
				_receiveCompletedDelegate(sender, new MessageQueueReceiveCompletedEventArgs(e.AsyncResult));

			}
			catch (MessageQueueException messageQueueException)
			{
				if (messageQueueException.MessageQueueErrorCode != MessageQueueErrorCode.IOTimeout &&
				    messageQueueException.MessageQueueErrorCode != MessageQueueErrorCode.QueueDeleted &&
				    (uint) messageQueueException.MessageQueueErrorCode != STATUS_CANCELLED)
				{
					throw;
				}
			}
		}

		public void BeginReceive(TimeSpan timeout)
		{
			_messageQueue.BeginReceive(timeout);
		}

		public MessageQueueMessageWrapper EndReceive(IAsyncResult asyncResult)
		{
			return new MessageQueueMessageWrapper(_messageQueue.EndReceive(asyncResult));
		}
	}
}