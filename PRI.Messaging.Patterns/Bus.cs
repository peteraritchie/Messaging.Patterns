using System;
using System.Collections.Generic;
using System.Linq;
#if NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4
using System.Reflection;
#else
using PRI.ProductivityExtensions.ActionExtensions;
#endif
using System.Runtime.CompilerServices;
using System.Threading;
#if SUPPORT_ASYNC_CONSUMER
using System.Threading.Tasks;
#endif
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Primitives;
using TokenType= System.Tuple<System.Guid, System.Action<PRI.Messaging.Primitives.IMessage>, System.Func<PRI.Messaging.Primitives.IMessage, System.Threading.Tasks.Task>>;

[assembly: InternalsVisibleTo("Tests")]
[assembly: InternalsVisibleTo("Tests-Core")]

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
	public class Bus : IBus, IDisposable
	{
		internal readonly Dictionary<string, Action<IMessage>> _consumerInvokers = new Dictionary<string, Action<IMessage>>();
		internal readonly Dictionary<string, Dictionary<Guid, Action<IMessage>>> _consumerInvokersDictionaries =
			new Dictionary<string, Dictionary<Guid, Action<IMessage>>>();
#if SUPPORT_ASYNC_CONSUMER // TODO: add more tests
		internal readonly Dictionary<string, Func<IMessage, Task>> _asyncConsumerInvokers =
new Dictionary<string, Func<IMessage, Task>>();
		internal readonly Dictionary<string, Dictionary<Guid, Func<IMessage, Task>>> _asyncConsumerInvokersDictionaries =
new Dictionary<string, Dictionary<Guid, Func<IMessage, Task>>>();
#endif
#if in_progress
		internal readonly Dictionary<IMessage, Dictionary<string, string>> _outgoingMessageHeaders =
new Dictionary<IMessage, Dictionary<string, string>>();
#endif

		private ReaderWriterLockSlim _readerWriterConsumersLock = new ReaderWriterLockSlim();
		private ReaderWriterLockSlim _readerWriterAsyncConsumersLock = new ReaderWriterLockSlim();
#if PARANOID
		private readonly List<Guid> _invokedConsumers = new List<Guid>();
#endif // PARANOID

		protected virtual void Handle(IMessage message, out bool wasProcessed)
		{
			var isEvent = message is IEvent;
			var messageType = message.GetType();
			Action<IMessage> consumerInvoker;
			bool consumerExists;
			_readerWriterConsumersLock.EnterReadLock();
			Guid messageTypeGuid;
#if NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4
			messageTypeGuid = messageType.GetTypeInfo().GUID;
#else
			messageTypeGuid = messageType.GUID;
#endif
			try
			{
				consumerExists = _consumerInvokers.TryGetValue(messageType.AssemblyQualifiedName ?? messageTypeGuid.ToString(),
					out consumerInvoker);
			}
			finally
			{
				_readerWriterConsumersLock.ExitReadLock();
			}

			wasProcessed = false;
			if (consumerExists)
			{
#if PARANOID
				_invokedConsumers.AddRange(_consumerInvokersDictionaries[messageType.AssemblyQualifiedName ?? messageType.GUID.ToString()].Keys);
#endif // PARANOID
				consumerInvoker(message);
				wasProcessed = true;
				if (!isEvent)
					return;
			}

			// check base type hierarchy.
#if NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4
			messageType = messageType.GetTypeInfo().BaseType;
#else
			messageType = messageType.BaseType;
#endif
			while (messageType != null && messageType != typeof(object))
			{
				_readerWriterConsumersLock.EnterReadLock();
				try
				{
					consumerExists = _consumerInvokers.TryGetValue(messageType.AssemblyQualifiedName ?? messageTypeGuid.ToString(),
						out consumerInvoker);
				}
				finally
				{
					_readerWriterConsumersLock.ExitReadLock();
				}
				if (consumerExists)
				{
					consumerInvoker(message);
					wasProcessed = true;
					if (!isEvent)
						return;
				}
#if NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4
				messageType = messageType.GetTypeInfo().BaseType;
#else
			messageType = messageType.BaseType;
#endif
			}

			// check any implemented interfaces
			messageType = message.GetType();
#if NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4
			var interfaces = messageType.GetTypeInfo().ImplementedInterfaces;
#else
			var interfaces = messageType.FindInterfaces((type, criteria) => true, null);
#endif
			foreach (var interfaceType in interfaces)
			{
				_readerWriterConsumersLock.EnterReadLock();
				try
				{
#if NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4
					var guid = interfaceType.GetTypeInfo().GUID;
#else
					var guid = interfaceType.GUID;
#endif
					consumerExists = _consumerInvokers.TryGetValue(interfaceType.AssemblyQualifiedName ?? guid.ToString(),
						out consumerInvoker);
				}
				finally
				{
					_readerWriterConsumersLock.ExitReadLock();
				}
				if (!consumerExists) continue;

				consumerInvoker(message);
				wasProcessed = true;
				if (!isEvent)
					return;
			}
		}

		public virtual void Handle(IMessage message)
		{
			bool wasProcessed;
			Handle(message, out wasProcessed);
		}

		public void AddTranslator<TIn, TOut>(IPipe<TIn, TOut> pipe) where TIn : IMessage where TOut : IMessage
		{
			pipe.AttachConsumer(new ActionConsumer<TOut>(m => this.Handle(m)));

			Action<IMessage> handler = o => pipe.Handle((TIn) o);
			var typeGuid = typeof(TIn).AssemblyQualifiedName ??
#if NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4
			               typeof(TIn).GetTypeInfo().GUID.ToString();
#else
				typeof(TIn).GUID.ToString();
#endif
			// never gets removed, inline guid
			var delegateGuid = Guid.NewGuid();
			_readerWriterConsumersLock.EnterWriteLock();
			try
			{
				if (_consumerInvokers.ContainsKey(typeGuid))
				{
					_consumerInvokersDictionaries[typeGuid][delegateGuid] = handler;
#if NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4
					Action<IMessage> actions = _consumerInvokersDictionaries[typeGuid].Values.First();
					foreach (var action in _consumerInvokersDictionaries[typeGuid].Values.Skip(1))
					{
						actions = actions + action;
					}
					_consumerInvokers[typeGuid] = actions;
#else
					_consumerInvokers[typeGuid] =
						_consumerInvokersDictionaries[typeGuid].Values.Sum();
#endif
				}
				else
				{
					_consumerInvokersDictionaries.Add(typeGuid,
						new Dictionary<Guid, Action<IMessage>> {{delegateGuid, handler}});
					_consumerInvokers.Add(typeGuid, handler);
				}
			}
			finally
			{
				_readerWriterConsumersLock.ExitWriteLock();
			}
		}

#if SUPPORT_ASYNC_CONSUMER
		private Func<IMessage, Task> CreateAsyncConsumerDelegate<TIn>(IAsyncConsumer<TIn> asyncConsumer) where TIn : IMessage
		{
			return async o => await asyncConsumer.HandleAsync((TIn)o).ConfigureAwait(false);
		}
#endif

		private Action<IMessage> CreateConsumerDelegate<TIn>(IConsumer<TIn> consumer) where TIn : IMessage
		{
			return o => consumer.Handle((TIn) o);
		}

		public object AddHandler<TIn>(IConsumer<TIn> consumer) where TIn : IMessage
		{
			Action<IMessage> handler = CreateConsumerDelegate(consumer);
			var typeGuid = typeof(TIn).AssemblyQualifiedName ??
#if NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4
			               typeof(TIn).GetTypeInfo().GUID.ToString();
#else
				typeof(TIn).GUID.ToString();
#endif
			var delegateGuid = Guid.NewGuid();
#if SUPPORT_ASYNC_CONSUMER
			var asyncConsumer = consumer as IAsyncConsumer<TIn>;
			if (asyncConsumer == null)
			{
#endif
			_readerWriterConsumersLock.EnterWriteLock();

			try
			{
				if (_consumerInvokers.ContainsKey(typeGuid))
				{
					_consumerInvokersDictionaries[typeGuid][delegateGuid] = handler;
#if NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4
					Action<IMessage> actions = _consumerInvokersDictionaries[typeGuid].Values.First();
					foreach (var action in _consumerInvokersDictionaries[typeGuid].Values.Skip(1))
					{
						actions = actions + action;
					}
					_consumerInvokers[typeGuid] = actions;
#else
					_consumerInvokers[typeGuid] = _consumerInvokersDictionaries[typeGuid].Values.Sum();
#endif
				}
				else
				{
					try
					{
						_consumerInvokersDictionaries.Add(typeGuid,
							new Dictionary<Guid, Action<IMessage>> {{delegateGuid, handler}});
					}
					catch (ArgumentException ex)
					{
						throw new UnexpectedDuplicateKeyException(ex, typeGuid, _consumerInvokersDictionaries.Keys,
							"invoker dictionaries");
					}
					try
					{
						_consumerInvokers.Add(typeGuid, handler);
					}
					catch (ArgumentException ex)
					{
						throw new UnexpectedDuplicateKeyException(ex, typeGuid, _consumerInvokers.Keys, "invokers");
					}
				}
			}
			finally
			{
				_readerWriterConsumersLock.ExitWriteLock();
			}
			return new TokenType(delegateGuid, handler, null);
#if SUPPORT_ASYNC_CONSUMER
			}
			var asyncHandler = CreateAsyncConsumerDelegate(asyncConsumer);
			_readerWriterAsyncConsumersLock.EnterWriteLock();
			try
			{
				if (_asyncConsumerInvokers.ContainsKey(typeGuid))
				{
					_asyncConsumerInvokersDictionaries[typeGuid][delegateGuid] = asyncHandler;
					_asyncConsumerInvokers[typeGuid] = _asyncConsumerInvokersDictionaries[typeGuid].Values.Sum();
				}
				else
				{
					_asyncConsumerInvokersDictionaries.Add(typeGuid,
						new Dictionary<Guid, Func<IMessage, Task>> {{delegateGuid, asyncHandler}});
					_asyncConsumerInvokers.Add(typeGuid, asyncHandler);
				}
			}
			finally
			{
				_readerWriterAsyncConsumersLock.ExitWriteLock();
			}

			_readerWriterConsumersLock.EnterWriteLock();
			try
			{
				if (_consumerInvokers.ContainsKey(typeGuid))
				{
					_consumerInvokersDictionaries[typeGuid][delegateGuid] = handler;
					_consumerInvokers[typeGuid] = _consumerInvokersDictionaries[typeGuid].Values.Sum();
				}
				else
				{
					_consumerInvokersDictionaries.Add(typeGuid,
						new Dictionary<Guid, Action<IMessage>> { { delegateGuid, handler } });
					_consumerInvokers.Add(typeGuid, handler);
				}
			}
			finally
			{
				_readerWriterConsumersLock.ExitWriteLock();

			}

			return new TokenType(delegateGuid, handler, asyncHandler);
#endif
		}

#if PARANOID
		internal 
#else
		public
#endif
			void RemoveHandler<TIn>(IConsumer<TIn> consumer, object tag
#if PARANOID
			, bool nocheck = false
#endif // PARANOID
			) where TIn : IMessage
		{
			if (tag == null)
				throw new ArgumentNullException(nameof(tag));
			var tuple = tag as TokenType;
			if (tuple == null)
				throw new InvalidOperationException();
			var typeGuid = typeof(TIn).AssemblyQualifiedName ??
#if NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4
			               typeof(TIn).GetTypeInfo().GUID.ToString();
#else
				typeof(TIn).GUID.ToString();
#endif
			_readerWriterConsumersLock.EnterUpgradeableReadLock();
			try
			{
				var hasConsumerType = _consumerInvokers.ContainsKey(typeGuid);
				if (!hasConsumerType)
					return;
#if PARANOID
				if (!nocheck && !_invokedConsumers.Contains(tuple.Item1))
					throw new MessageHandlerRemovedBeforeProcessingMessageException<TIn>();
#endif // PARANOID

				_readerWriterConsumersLock.EnterWriteLock();
				try
				{
					_consumerInvokersDictionaries[typeGuid].Remove(tuple.Item1);
					if (_consumerInvokersDictionaries[typeGuid].Any()) // any more left for that type?
					{
#if NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4
						Action<IMessage> actions = _consumerInvokersDictionaries[typeGuid].Values.First();
						foreach (var action in _consumerInvokersDictionaries[typeGuid].Values.Skip(1))
						{
							actions = actions + action;
						}
						_consumerInvokers[typeGuid] = actions;
#else
						_consumerInvokers[typeGuid] = _consumerInvokersDictionaries[typeGuid].Values.Sum();
#endif
					}
					else
					{
						_consumerInvokers.Remove(typeGuid); // if no more, get rid of invoker
						_consumerInvokersDictionaries.Remove(typeGuid);
					}
				}
				finally
				{
					_readerWriterConsumersLock.ExitWriteLock();
				}
			}
			finally
			{
				_readerWriterConsumersLock.ExitUpgradeableReadLock();
			}
		}

#if PARANOID
		public void RemoveHandler<TIn>(IConsumer<TIn> consumer, object tag) where TIn : IMessage
		{
			RemoveHandler(consumer, tag, nocheck: false);
		}
#endif

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

#if SUPPORT_ASYNC_CONSUMER
		public Task HandleAsync(IMessage message)
		{
			bool wasProcessed;
			return HandleAsync(message, out wasProcessed);
		}

		public Task HandleAsync(IMessage message, out bool wasProcessed)
		{
			var isEvent = message is IEvent;
			var messageType = message.GetType();
			Func<IMessage, Task> consumerInvoker;
			Task task = null;
			bool hasConsumer;
			_readerWriterAsyncConsumersLock.EnterReadLock();
			var messageTypeGuid = messageType.AssemblyQualifiedName ?? messageType.GUID.ToString();
			try
			{
				hasConsumer = _asyncConsumerInvokers.TryGetValue(messageTypeGuid, out consumerInvoker);
			}
			finally
			{
				_readerWriterAsyncConsumersLock.ExitReadLock();
			}

			wasProcessed = false;
			if (hasConsumer)
			{
#if PARANOID
				_invokedConsumers.AddRange(_consumerInvokersDictionaries[messageTypeGuid].Keys);
#endif // PARANOID
				task = consumerInvoker(message);
				wasProcessed = true;
				if (!isEvent)
					return task;
			}

			// check base type hierarchy.
			messageType = messageType.BaseType;
			while (messageType != null && messageType != typeof(object))
			{
				_readerWriterAsyncConsumersLock.EnterReadLock();
				try
				{
					hasConsumer = _asyncConsumerInvokers.TryGetValue(messageTypeGuid, out consumerInvoker);
				}
				finally
				{
					_readerWriterAsyncConsumersLock.ExitReadLock();
				}
				if (hasConsumer)
				{
#if PARANOID
					_invokedConsumers.AddRange(_consumerInvokersDictionaries[messageTypeGuid].Keys);
#endif // PARANOID
					var newTask = consumerInvoker(message);
					wasProcessed = true;
					// merge tasks, if needed
					task = task != null ? Task.WhenAll(task, newTask) : newTask;
					if (!isEvent)
						return task;
				}

				messageType = messageType.BaseType;
			}

			// check any implemented interfaces
			messageType = message.GetType();
			foreach (var interfaceType in messageType.FindInterfaces((type, criteria) => true, null))
			{
				_readerWriterAsyncConsumersLock.EnterReadLock();
				var interfaceTypeGuid = interfaceType.AssemblyQualifiedName ?? interfaceType.GUID.ToString();
				try
				{
					hasConsumer = _asyncConsumerInvokers.TryGetValue(interfaceTypeGuid, out consumerInvoker);
				}
				finally
				{
					_readerWriterAsyncConsumersLock.ExitReadLock();
				}

				if (!hasConsumer) continue;

#if PARANOID
				_invokedConsumers.AddRange(_consumerInvokersDictionaries[interfaceTypeGuid].Keys);
#endif // PARANOID
				var newTask = consumerInvoker(message);
				wasProcessed = true;
				// merge tasks if needed
				task = task != null ? Task.WhenAll(task, newTask) : newTask;
				if (!isEvent)
					return task;
			}

			if (task != null)
				return task;
			Handle(message); // if no async handlers, handle synchronously
			return Task.FromResult(true); // TODO: Replace with Task.CompletedTask in .NET 4.6+
		}
#endif //SUPPORT_ASYNC_CONSUMER

		public virtual void Dispose()
		{
			_readerWriterConsumersLock.Dispose();
			_readerWriterAsyncConsumersLock.Dispose();
		}
	}
}