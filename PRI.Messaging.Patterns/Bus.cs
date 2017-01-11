using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using PRI.Messaging.Primitives;

[assembly:InternalsVisibleTo("Tests")]
namespace PRI.Messaging.Patterns
{
	/// <summary>
	/// An implementation of a composable bus https://en.wikipedia.org/wiki/Bus_(computing) that transfers messages between zero or more producers
	/// and zero or more consumers, decoupling producers from consumers.
	/// <example>
	/// Compose a bus from any/all consumers located in current directory in the Rock.QL.Endeavor.MessageHandlers namespace with a filename that matches Rock.QL.Endeavor.*Handler*.dll
	/// <code>
	/// var bus = new Bus();
	/// var directory = Path.GetDirectoryName(new Uri(GetType().Assembly.CodeBase).LocalPath);
	/// bus.AddHandlersAndTranslators(directory, "Rock.QL.Endeavor.*Handler*.dll", "Rock.QL.Endeavor.MessageHandlers");
	/// </code>
	/// Manually compose a bus and send it a message
	/// <code>
	/// var bus = new Bus();
	/// 
	/// var message2Consumer = new MessageConsumer();
	/// bus.AddHandler(message2Consumer);
	/// 
	/// bus.Handle(new Message());
	/// </code>
	/// </example>
	/// </summary>
	public class Bus : IBus
	{
		internal readonly Dictionary<Guid, Action<IMessage>> _consumerInvokers = new Dictionary<Guid, Action<IMessage>>();
		internal readonly Dictionary<Guid, Func<IMessage, Task>> _asyncConsumerInvokers = new Dictionary<Guid, Func<IMessage, Task>>();
		internal readonly Dictionary<IMessage, Dictionary<string, string>> _outgoingMessageHeaders = new Dictionary<IMessage, Dictionary<string, string>>();

		protected EventHandler<MessageProcessedEventArgs> MessageProcessed;

		public virtual void Handle(IMessage message)
		{
			var messageProcessedHandler = MessageProcessed;
			var isEvent = message is IEvent;
			var messageType = message.GetType();
			Action<IMessage> consumerInvoker;
			if (_consumerInvokers.TryGetValue(messageType.GUID, out consumerInvoker))
			{
				consumerInvoker(message);
				messageProcessedHandler?.Invoke(this, new MessageProcessedEventArgs(message));
				if (!isEvent) return;
			}

			// check base type hierarchy.
			messageType = messageType.BaseType;
			while (messageType != null && messageType != typeof(object))
			{
				if (_consumerInvokers.TryGetValue(messageType.GUID, out consumerInvoker))
				{
					consumerInvoker(message);
					messageProcessedHandler?.Invoke(this, new MessageProcessedEventArgs(message));
					if (!isEvent) return;
				}
				messageType = messageType.BaseType;
			}

			// check any implemented interfaces
			messageType = message.GetType();
			foreach (var interfaceType in messageType.FindInterfaces((type, criteria) => true, null))
			{
				if (!_consumerInvokers.TryGetValue(interfaceType.GUID, out consumerInvoker)) continue;

				consumerInvoker(message);
				messageProcessedHandler?.Invoke(this, new MessageProcessedEventArgs(message));
				if (!isEvent) return;
			}
		}

		public void AddTranslator<TIn, TOut>(IPipe<TIn, TOut> pipe) where TIn : IMessage where TOut : IMessage
		{
			pipe.AttachConsumer(new ActionConsumer<TOut>(m => this.Handle(m)));

			Action<IMessage> handler = o => pipe.Handle((TIn)o);
			_consumerInvokers.Add(typeof(TIn).GUID, handler);
		}

		private Func<IMessage, Task> CreateAsyncConsumerDelegate<TIn>(IAsyncConsumer<TIn> asyncConsumer) where TIn : IMessage
		{
			return async o => await asyncConsumer.HandleAsync((TIn)o).ConfigureAwait(false);
		}

		private Action<IMessage> CreateConsumerDelegate<TIn>(IConsumer<TIn> consumer) where TIn : IMessage
		{
			return o => consumer.Handle((TIn)o);
		}

		public object AddHandler<TIn>(IConsumer<TIn> consumer) where TIn : IMessage
		{
			var asyncConsumer = consumer as IAsyncConsumer<TIn>;
			Action<IMessage> handler = CreateConsumerDelegate(consumer);
			if (asyncConsumer == null)
			{
				if (_consumerInvokers.ContainsKey(typeof(TIn).GUID))
				{
					_consumerInvokers[typeof(TIn).GUID] += handler;
				}
				else
				{
					_consumerInvokers.Add(typeof(TIn).GUID, handler);
				}
				return new Tuple<Action<IMessage>, Func<IMessage, Task>>(handler, null);
			}
			var asyncHandler = CreateAsyncConsumerDelegate(asyncConsumer);
			if (_asyncConsumerInvokers.ContainsKey(typeof(TIn).GUID))
			{
				_asyncConsumerInvokers[typeof(TIn).GUID] += asyncHandler;
			}
			else
			{
				_asyncConsumerInvokers.Add(typeof(TIn).GUID, asyncHandler);
			}

			if (_consumerInvokers.ContainsKey(typeof(TIn).GUID))
			{
				_consumerInvokers[typeof(TIn).GUID] += handler;
			}
			else
			{
				_consumerInvokers.Add(typeof(TIn).GUID, handler);
			}
			return new Tuple<Action<IMessage>, Func<IMessage, Task>>(handler, asyncHandler);
		}

		public void RemoveHandler<TIn>(IConsumer<TIn> consumer, object tag) where TIn : IMessage
		{
			var tuple = tag as Tuple<Action<IMessage>, Func<IMessage, Task>>;
			if (tuple == null) throw new InvalidOperationException();

			if (!_consumerInvokers.ContainsKey(typeof(TIn).GUID)) return;

			var list = _consumerInvokers[typeof(TIn).GUID].GetInvocationList();
			var initialCount = list.Length;
			if (tuple.Item1 != null) _consumerInvokers[typeof(TIn).GUID] -= tuple.Item1;
			if (!_consumerInvokers.ContainsKey(typeof(TIn).GUID)) return;
			if (_consumerInvokers[typeof(TIn).GUID] != null)
			{
				list = _consumerInvokers[typeof(TIn).GUID].GetInvocationList();
				if (initialCount == list.Length)
				{
					Action<IMessage> handler = CreateConsumerDelegate(consumer);
					Delegate m =
						list.LastOrDefault(e => e.Method.Name == handler.Method.Name && e.Target.GetType() == handler.Target.GetType());
					if (m != null) _consumerInvokers[typeof(TIn).GUID] -= (Action<IMessage>)m;
				}
				if (_consumerInvokers[typeof(TIn).GUID] != null)
				{
					list = _consumerInvokers[typeof(TIn).GUID].GetInvocationList();
#if DEBUG
					Debug.Assert(initialCount != list.Length);
#endif
				}
				else
					_consumerInvokers.Remove(typeof(TIn).GUID);
			}
			else
				_consumerInvokers.Remove(typeof(TIn).GUID);
		}

		[Obsolete]
		public void RemoveHandler<TIn>(IConsumer<TIn> consumer) where TIn : IMessage
		{
			if (!_consumerInvokers.ContainsKey(typeof(TIn).GUID)) return;

			var list = _consumerInvokers[typeof(TIn).GUID].GetInvocationList();
			var initialCount = list.Length;
			Action<IMessage> handler = CreateConsumerDelegate(consumer);
			Delegate m =
				list.LastOrDefault(e => e.Method.Name == handler.Method.Name && e.Target.GetType() == handler.Target.GetType());
			if (m != null) _consumerInvokers[typeof(TIn).GUID] -= (Action<IMessage>)m;
			if (!_consumerInvokers.ContainsKey(typeof(TIn).GUID)) return;
			if (_consumerInvokers[typeof(TIn).GUID] != null)
			{
				list = _consumerInvokers[typeof(TIn).GUID].GetInvocationList();
				Debug.Assert(initialCount != list.Length);
			}
			else
				_consumerInvokers.Remove(typeof(TIn).GUID);
		}

#if in_progress
		ThreadLocal<Dictionary<string,string>> currentMessageDictionary = new ThreadLocal<Dictionary<string, string>>();

		public void AddHeader(IMessage message, string key, string value)
		{
			Dictionary<string, string> dict;
			if (_outgoingMessageHeaders.ContainsKey(message))
			{
				dict = _outgoingMessageHeaders[message];
			}
			else
			{
				dict = new Dictionary<string, string>();
				_outgoingMessageHeaders[message] = dict;
			}

			dict[key] = value;
		}
#endif // in_progress

		public Task HandleAsync(IMessage message)
		{
			var messageProcessedHandler = MessageProcessed;
			var isEvent = message is IEvent;
			var messageType = message.GetType();
			Func<IMessage, Task> consumerInvoker;
			Task task = null;
			if (_asyncConsumerInvokers.TryGetValue(messageType.GUID, out consumerInvoker))
			{
				task = consumerInvoker(message);
				messageProcessedHandler?.Invoke(this, new MessageProcessedEventArgs(message));
				if (!isEvent) return task;
			}

			// check base type hierarchy.
			messageType = messageType.BaseType;
			while (messageType != null && messageType != typeof(object))
			{
				if (_asyncConsumerInvokers.TryGetValue(messageType.GUID, out consumerInvoker))
				{
					var newTask = consumerInvoker(message);
					messageProcessedHandler?.Invoke(this, new MessageProcessedEventArgs(message));
					if (task != null) // merge tasks
					{
						task = Task.WhenAll(task, newTask);
					}
					if (!isEvent) return task;
				}
				messageType = messageType.BaseType;
			}

			// check any implemented interfaces
			messageType = message.GetType();
			foreach (var interfaceType in messageType.FindInterfaces((type, criteria) => true, null))
			{
				if (!_asyncConsumerInvokers.TryGetValue(interfaceType.GUID, out consumerInvoker)) continue;

				var newTask = consumerInvoker(message);
				messageProcessedHandler?.Invoke(this, new MessageProcessedEventArgs(message));
				if (task != null) // merge tasks
				{
					task = Task.WhenAll(task, newTask);
				}
				if (!isEvent) return task;
			}

			if (task == null)
			{
				Handle(message); // if no async handlers, handle synchronously
				return Task.FromResult(true); // TODO: Replace with Task.CompletedTask in .NET 4.6+
			}
			return task;
		}
	}
}