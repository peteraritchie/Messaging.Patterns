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
		internal readonly Dictionary<int, Action<IMessage>> _consumerInvokers = new Dictionary<int, Action<IMessage>>();
		private Action<IMessage> _singleConsumerDelegate;

		public void Handle(IMessage message)
		{
			var messageType = message.GetType();
			if (_singleConsumerDelegate != null)
			{
				_singleConsumerDelegate(message);
				return;
			}
			Action<IMessage> consumerInvoker;
			if (_consumerInvokers.TryGetValue(messageType.MetadataToken, out consumerInvoker))
			{
				consumerInvoker(message);
			}
		}

		public void AddTranslator<TIn, TOut>(IPipe<TIn, TOut> pipe) where TIn : IMessage where TOut : IMessage
		{
			pipe.AttachConsumer(new ActionConsumer<TOut>(m => this.Handle(m)));

			Action<IMessage> handler = o => pipe.Handle((TIn) o);
			_consumerInvokers.Add(typeof(TIn).MetadataToken, handler);
			if (_consumerInvokers.Count > 1) _singleConsumerDelegate = (Action<object>) null;
			else _singleConsumerDelegate = handler;
		}

		private Action<IMessage> CreateConsumerDelegate<TIn>(IConsumer<TIn> consumer) where TIn : IMessage
		{
			return o => consumer.Handle((TIn) o);
		}

		public void AddHandler<TIn>(IConsumer<TIn> consumer) where TIn : IMessage
		{
			Action<IMessage> handler = CreateConsumerDelegate(consumer);
			if (_consumerInvokers.ContainsKey(typeof(TIn).MetadataToken))
			{
				_consumerInvokers[typeof(TIn).MetadataToken] += handler;
				_singleConsumerDelegate = null;
			}
			else
			{
				_consumerInvokers.Add(typeof(TIn).MetadataToken, handler);
				_singleConsumerDelegate = _consumerInvokers.Count > 1 ? null : handler;
			}
		}

		public void RemoveHandler<TIn>(IConsumer<TIn> consumer) where TIn: IMessage
		{
			if (!_consumerInvokers.ContainsKey(typeof(TIn).MetadataToken)) return;

			var list = _consumerInvokers[typeof(TIn).MetadataToken].GetInvocationList();
			var initialCount = list.Length;
			Action<IMessage> handler = CreateConsumerDelegate(consumer);
			Delegate m =
				list.LastOrDefault(e => e.Method.Name == handler.Method.Name && e.Target.GetType() == handler.Target.GetType());
			if (m != null) _consumerInvokers[typeof(TIn).MetadataToken] -= (Action<IMessage>) m;
			list = _consumerInvokers[typeof(TIn).MetadataToken].GetInvocationList();
			Debug.Assert(initialCount != list.Length);
		}
	}
}