using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using PRI.Messaging.Primitives;

namespace PRI.Messaging.Patterns.Exceptions
{
	public class MessageHandlerRemovedBeforeProcessingMessageException<T> : Exception
		where T : IMessage
	{
		public MessageHandlerRemovedBeforeProcessingMessageException()
			:base($"Consumer of message type {typeof(T).Name} was removed without being invoked.")
		{
		}
	}

	[ExcludeFromCodeCoverage/*unable to reproduce this exception in a unit test*/]
	public class UnexpectedDuplicateKeyException : Exception
	{
		public UnexpectedDuplicateKeyException(ArgumentException argumentException, string key, IEnumerable<string> keys, string context = "<unknown>")
			: base($"{key} already found in {string.Join("", "", keys)} ({context}) ", argumentException)
		{
			throw new NotImplementedException();
		}
	}
}