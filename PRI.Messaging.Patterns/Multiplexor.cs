using System.Collections.Generic;
using System.Linq;
using PRI.Messaging.Primitives;

namespace PRI.Messaging.Patterns
{
	/// <summary>
	/// Forwards message of type <typeparamref name="T"/> to zero or more consumers
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class Multiplexor<T> : IPipe<T, T> where T : IMessage
	{
		internal readonly List<IConsumer<T>> _consumers;

		public Multiplexor()
			: this(null)
		{
		}

		public void AttachConsumer(IConsumer<T> consumer)
		{
			_consumers.Add(consumer);
		}

		public Multiplexor(IEnumerable<IConsumer<T>> consumers)
		{
			_consumers = consumers == null ? new List<IConsumer<T>>() : consumers.ToList();
		}

		public void RemoveConsumer(IConsumer<T> consumer)
		{
			_consumers.Remove(consumer);
		}

		public void Handle(T message)
		{
			_consumers.ForEach(x => x.Handle(message));
		}
	}
}