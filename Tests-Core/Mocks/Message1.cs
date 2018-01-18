using System;
using PRI.Messaging.Primitives;

namespace Tests.Mocks
{
	public class Message1 : IMessage
	{
		public string CorrelationId { get; set; }
	}
}