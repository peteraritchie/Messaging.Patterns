using System;
using System.Threading.Tasks;
using PRI.Messaging.Primitives;

namespace PRI.Messaging.Patterns
{
	public class AsyncActionConsumer<T> : IAsyncConsumer<T> where T : IMessage
	{
		private readonly Func<T, Task> _action;

		public AsyncActionConsumer(Func<T, Task> action)
		{
			if (action == null) throw new ArgumentNullException(nameof(action));
			_action = action;
		}

		public void Handle(T message)
		{
			HandleAsync(message).Wait();
		}

		public Task HandleAsync(T message)
		{
			return _action(message);
		}
	}
}