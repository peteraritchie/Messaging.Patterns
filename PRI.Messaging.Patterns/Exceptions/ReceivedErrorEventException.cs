using System;
using PRI.Messaging.Primitives;

namespace PRI.Messaging.Patterns.Exceptions
{
	public class ReceivedErrorEventException<TEvent> : Exception
		where TEvent : IEvent
	{
		public TEvent ErrorEvent { get; private set; }

		public ReceivedErrorEventException(TEvent errorEvent)
		{
			ErrorEvent = errorEvent;
		}
	}

	public class MessageHandlerRemovedBeforeProcessingMessage<T> : Exception
		where T : IMessage
	{
		public MessageHandlerRemovedBeforeProcessingMessage()
			:base($"Consumer of message type {typeof(T).Name} was removed without being invoked.")
		{
		}
	}
}