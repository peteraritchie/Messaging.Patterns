using PRI.Messaging.Primitives;

namespace Tests.Mocks
{
	public class Message2 : IMessage
	{
		public string CorrelationId { get; set; }
	}
}