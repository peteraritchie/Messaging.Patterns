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
}