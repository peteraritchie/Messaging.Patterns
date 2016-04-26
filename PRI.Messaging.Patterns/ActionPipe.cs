using System;
using PRI.Messaging.Primitives;

namespace PRI.Messaging.Patterns
{
	public class ActionPipe<TIn, TOut> : IPipe<TIn, TOut> where TIn : IMessage where TOut : IMessage
	{
		private IConsumer<TOut> _consumer;
		private readonly Func<TIn, TOut> _translator;

		public ActionPipe(Func<TIn, TOut> translator)
		{
			_translator = translator;
		}

		public void AttachConsumer(IConsumer<TOut> consumer)
		{
			_consumer = consumer;
		}

		public void Handle(TIn message)
		{
			_consumer.Handle(_translator(message));
		}
	}
}