#if (NET45 || NET451 || NET452 || NET46 || NET461 || NET462)
using System;
using System.Messaging;
using PRI.Messaging.Patterns.Internal;
using PRI.Messaging.Primitives;

namespace PRI.Messaging.Patterns
{
	public sealed class MsmqReader<TMessage> : IDisposable, IProducer<TMessage> where TMessage : IMessage
	{
		readonly IMessageQueue _messageQueue;
		private bool _stop;
		private TimeSpan _defaultTimeout = TimeSpan.FromSeconds(1);
		private IConsumer<TMessage> _consumer;
		private readonly Func<MessageQueueMessageWrapper, TMessage> _messageDeserializer;

		internal MsmqReader(IMessageQueue messageQueue, Func<MessageQueueMessageWrapper, TMessage> messageDeserializer)
		{
			if (messageQueue == null) throw new ArgumentNullException("messageQueue");
			_messageQueue = messageQueue;
			_messageDeserializer = messageDeserializer;
		}

		public MsmqReader(MessageQueue messageQueue, Func<Message, TMessage> messageDeserializer)
		{
			if (messageQueue == null) throw new ArgumentNullException("messageQueue");
			_messageQueue = new MessageQueueWrapper(messageQueue);
			_messageDeserializer = message => messageDeserializer(message.Message);
		}


		internal MsmqReader(MessageQueue messageQueue, Func<MessageQueueMessageWrapper, TMessage> messageDeserializer)
		{
			if (messageQueue == null) throw new ArgumentNullException("messageQueue");
			_messageQueue = new MessageQueueWrapper(messageQueue);
			_messageDeserializer = messageDeserializer;
		}

		public void AttachConsumer(IConsumer<TMessage> consumer)
		{
			if (consumer == null) throw new ArgumentNullException("consumer");
			_consumer = consumer;
		}

		public void Start()
		{
			_messageQueue.ReceiveCompleted += _IMessageQueue_ReceiveCompleted;
			_messageQueue.BeginReceive(_defaultTimeout);
		}

		void _IMessageQueue_ReceiveCompleted(object sender,
			MessageQueueReceiveCompletedEventArgs messageQueueReceiveCompletedEventArgs)
		{
			try
			{
				var messageObject = _messageQueue.EndReceive(messageQueueReceiveCompletedEventArgs.AsyncResult);
				var c = _consumer;

				var message = _messageDeserializer != null ? _messageDeserializer(messageObject) : (TMessage) messageObject.Message.Body;

				if (c == null) return;
				c.Handle(message);
			}
			catch (MessageQueueException ex)
			{
				if (ex.MessageQueueErrorCode != MessageQueueErrorCode.IOTimeout) throw;
			}

			if (!_stop)
			{
				// re-try
				try
				{
					_messageQueue.BeginReceive(_defaultTimeout);

				}
				catch (MessageQueueException ex)
				{

					if (ex.MessageQueueErrorCode != MessageQueueErrorCode.IOTimeout) throw;
				}
			}
		}

		public void Dispose()
		{
			_stop = true;
			_messageQueue.Dispose();
		}
	}
}
#endif