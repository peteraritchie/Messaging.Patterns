using System;
using System.Linq;
using PRI.Messaging.Primitives;
using PRI.ProductivityExtensions.ReflectionExtensions;

namespace PRI.Messaging.Patterns.Extensions.Bus
{
	public static class BusExtensions
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="bus"></param>
		/// <param name="directory"></param>
		/// <param name="wildcard"></param>
		/// <param name="namespace"></param>
		public static void AddHandlersAndTranslators(this Patterns.Bus bus, string directory, string wildcard, string @namespace)
		{
			var consumerTypes = typeof(IConsumer<>).ByImplementedInterfaceInDirectory(directory, wildcard, @namespace);
			foreach (var consumerType in consumerTypes)
			{
				var pipeImplementationInterface =
					consumerType.GetInterfaces()
						.FirstOrDefault(t => !t.IsGenericTypeDefinition && t.GetGenericTypeDefinition() == typeof (IPipe<,>));
				if(pipeImplementationInterface != null)
				{
					var translatorType = consumerType;
					var messageTypes = pipeImplementationInterface.GetGenericTypeArguments();

					// get instance of pipe
					var translatorInstance = Activator.CreateInstance(translatorType);

					// get instance of the helper
					var helperType1 = typeof (PipeAttachConsumerHelper<,>).MakeGenericType(messageTypes);
					var helperType1Instance = Activator.CreateInstance(helperType1);
					var attachConsumerMethodInfo = helperType1.GetMethod("AttachConsumer");
					attachConsumerMethodInfo.Invoke(helperType1Instance, new[] {translatorInstance, bus});

					var inType = messageTypes[0];
					var helperType = typeof(BusAddhHandlerHelper<>).MakeGenericType(inType);
					var helperInstance = Activator.CreateInstance(helperType);
					var addHandlerMethodInfo = helperType.GetMethod("AddHandler");

					addHandlerMethodInfo.Invoke(helperInstance, new[] { bus, translatorInstance });
				}
				else
				{
					var consumerImplementationInterface =
						consumerType.GetInterfaces()
							.FirstOrDefault(t => !t.IsGenericTypeDefinition && t.GetGenericTypeDefinition() == typeof(IConsumer<>));
					if(consumerImplementationInterface == null) throw new InvalidOperationException("Type X did not implement IConsumer<> propertly");

					var messageTypes = consumerImplementationInterface.GetGenericTypeArguments();

					var helperType = typeof(BusAddhHandlerHelper<>).MakeGenericType(messageTypes);
					var helperInstance = Activator.CreateInstance(helperType);
					var addHandlerMethodInfo = helperType.GetMethod("AddHandler");

					var handlerInstance = Activator.CreateInstance(consumerType);

					addHandlerMethodInfo.Invoke(helperInstance, new[] {bus, handlerInstance});
				}
			}
		}

		private class BusAddhHandlerHelper<TMessage> where TMessage : IMessage
		{
			public void AddHandler(IBus bus, IConsumer<TMessage> consumer)
			{
				bus.AddHandler(consumer);
			}
		}

		private class PipeAttachConsumerHelper<TIn, TOut>
			where TIn : IMessage
			where TOut : IMessage
		{
			public void AttachConsumer(IPipe<TIn, TOut> pipe, IConsumer<TOut> bus)
			{
				pipe.AttachConsumer(new ActionConsumer<TOut>(bus.Handle));
			}
		}
	}
}