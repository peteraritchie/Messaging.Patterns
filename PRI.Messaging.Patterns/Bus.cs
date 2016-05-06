using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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

		public void Handle(IMessage message)
		{
			var isEvent = message is IEvent;
			var messageType = message.GetType();
			Action<IMessage> consumerInvoker;
			if (_consumerInvokers.TryGetValue(messageType.GUID, out consumerInvoker))
			{
				consumerInvoker(message);
				if(!isEvent) return;
			}

			// check base type hierarchy.
			messageType = messageType.BaseType;
			while (messageType != null && messageType != typeof(object))
			{
				if (_consumerInvokers.TryGetValue(messageType.GUID, out consumerInvoker))
				{
					consumerInvoker(message);
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
				if (!isEvent) return;
			}
		}

		public void AddTranslator<TIn, TOut>(IPipe<TIn, TOut> pipe) where TIn : IMessage where TOut : IMessage
		{
			pipe.AttachConsumer(new ActionConsumer<TOut>(m => this.Handle(m)));

			Action<IMessage> handler = o => pipe.Handle((TIn) o);
			_consumerInvokers.Add(typeof(TIn).GUID, handler);
		}

		private Action<IMessage> CreateConsumerDelegate<TIn>(IConsumer<TIn> consumer) where TIn : IMessage
		{
			return o => consumer.Handle((TIn) o);
		}

		public void AddHandler<TIn>(IConsumer<TIn> consumer) where TIn : IMessage
		{
			Action<IMessage> handler = CreateConsumerDelegate(consumer);
			if (_consumerInvokers.ContainsKey(typeof(TIn).GUID))
			{
				_consumerInvokers[typeof(TIn).GUID] += handler;
			}
			else
			{
				_consumerInvokers.Add(typeof(TIn).GUID, handler);
			}
		}

		public void RemoveHandler<TIn>(IConsumer<TIn> consumer) where TIn: IMessage
		{
			if (!_consumerInvokers.ContainsKey(typeof(TIn).GUID)) return;

			var list = _consumerInvokers[typeof(TIn).GUID].GetInvocationList();
			var initialCount = list.Length;
			Action<IMessage> handler = CreateConsumerDelegate(consumer);
			Delegate m =
				list.LastOrDefault(e => e.Method.Name == handler.Method.Name && e.Target.GetType() == handler.Target.GetType());
			if (m != null) _consumerInvokers[typeof(TIn).GUID] -= (Action<IMessage>) m;
			if (!_consumerInvokers.ContainsKey(typeof(TIn).GUID)) return;
			if (_consumerInvokers[typeof(TIn).GUID] != null)
			{
				list = _consumerInvokers[typeof(TIn).GUID].GetInvocationList();
				Debug.Assert(initialCount != list.Length);
			}
			else
				_consumerInvokers.Remove(typeof(TIn).GUID);
		}
	}
}