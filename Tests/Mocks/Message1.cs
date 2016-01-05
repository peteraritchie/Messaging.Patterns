using System;
using PRI.Messaging.Primitives;

namespace Tests.Mocks
{
	[Serializable]
	public class Message1 : IMessage
	{
		public string CorrelationId { get; set; }
	}
}