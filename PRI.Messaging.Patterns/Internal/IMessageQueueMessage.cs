namespace PRI.Messaging.Patterns.Internal
{
	internal interface IMessageQueueMessage
	{
		object Body { get; }
	}
}