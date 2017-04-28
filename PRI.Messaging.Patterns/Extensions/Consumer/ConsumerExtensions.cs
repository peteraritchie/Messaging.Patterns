using PRI.Messaging.Primitives;

namespace PRI.Messaging.Patterns.Extensions.Consumer
{
	public static class ConsumerExtensions
	{
		/// <summary>
		/// Alias to be explicit about the intent of handling an <see cref="IMessage"/> instance.
		/// </summary>
		/// <typeparam name="TMessage">IMessage-based type to send</typeparam>
		/// <param name="consumer">consumer to send within</param>
		/// <param name="message">Message to send</param>
		public static void Send<TMessage>(this IConsumer<TMessage> consumer, TMessage message) where TMessage : IMessage
		{
			consumer.Handle(message);
		}

		/// <summary>
		/// Alias to be explicit about the intent of handling an <see cref="IEvent"/> instance.
		/// </summary>
		/// <typeparam name="TEvent">IEvent-based type to publish</typeparam>
		/// <param name="consumer">consumer to send within</param>
		/// <param name="event">Event to publish</param>
		public static void Publish<TEvent>(this IConsumer<TEvent> consumer, TEvent @event) where TEvent : IEvent
		{
			consumer.Handle(@event);
		}
	}
}
