using System;
using PRI.Messaging.Primitives;

namespace PRI.Messaging.Patterns
{
	/// <summary>
	/// Creates an IConsumer instance from a delete to quickly allow
	/// any type that does not implement IConsumer to be used as a consumer
	/// <example>
	/// <code>
	/// </code>
	/// </example>
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class ActionConsumer<T> : IConsumer<T> where T : IMessage
	{
		private readonly Action<T> _action;

		public void Handle(T message)
		{
			_action(message);
		}

		public ActionConsumer(Action<T> action)
		{
			if (action == null) throw new ArgumentNullException("action");
			_action = action;
		}
	}
}