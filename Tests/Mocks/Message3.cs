using PRI.Messaging.Primitives;

namespace Tests.Mocks
{
	public class Message3 : IMessage
	{
		public string CorrelationId { get; set; }
	}
}