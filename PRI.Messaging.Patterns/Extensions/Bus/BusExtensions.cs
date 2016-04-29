using System;
using System.Linq;
using PRI.Messaging.Primitives;
using PRI.ProductivityExtensions.ReflectionExtensions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PRI.Messaging.Patterns.Extensions.Bus
{
	public static class BusExtensions
	{
		private static readonly Dictionary<Patterns.Bus, Dictionary<Type, Delegate>> busResolverDictionaries = new Dictionary<Patterns.Bus, Dictionary<Type, Delegate>>();

		/// <summary>
		/// Add the ability to resolve an instance of <typeparam name="T"></typeparam>
		/// Without adding a resolver the bus will attempt to find and call the default
		/// constructor to create an instance when adding handlers from assemblies.
		/// Use <see cref="AddResolver{T}"/> if your handler does not have default constructor
		/// or you want to re-use an instance.
		/// </summary>
		/// <example>
		/// <code>
		/// // For type MessageHandler, call the c'tor that takes an IBus parameter
		/// bus.AddResolver(()=> new MessageHandler(bus));
		/// // find all the handlers in assemblies named *handlers.dll" in the current directory in the namespace
		/// "MyCorp.MessageHandlers
		/// bus.AddHandlersAndTranslators(Directory.GetCurrentDirectory(), "*handlers.dll", "MyCorp.MessageHandlers");
		/// </code>
		/// </example>
		/// <typeparam name="T">The type of instance to resolve.  Typically of type IConsumer{TMessage}</typeparam>
		/// <param name="bus">The <seealso cref="IBus"/> instance this will apply to</param>
		/// <param name="resolver">A delegate that returns an instance of <typeparamref name="T"/></param>
		public static void AddResolver<T>(this Patterns.Bus bus, Func<T> resolver)
		{
			if(!busResolverDictionaries.ContainsKey(bus)) busResolverDictionaries.Add(bus, new Dictionary<Type, Delegate>());
			var dictionary = busResolverDictionaries[bus];
			dictionary[typeof (T)] = resolver;
		}

		/// <summary>
		/// Dynamically load message handlers by filename in a specific directory.
		/// </summary>
		/// <example>
		/// <code>
		/// // find all the handlers in assemblies named *handlers.dll" in the current directory in the namespace
		/// "MyCorp.MessageHandlers
		/// bus.AddHandlersAndTranslators(Directory.GetCurrentDirectory(), "*handlers.dll", "MyCorp.MessageHandlers");
		/// </code>
		/// </example>
		/// <param name="bus">The <seealso cref="IBus"/> instance this will apply to</param>
		/// <param name="directory">What directory to search</param>
		/// <param name="wildcard">What filenames to search</param>
		/// <param name="namespace">Include IConumers{TMessage} within this namespace</param>
		public static void AddHandlersAndTranslators(this Patterns.Bus bus, string directory, string wildcard, string @namespace)
		{
			if (!busResolverDictionaries.ContainsKey(bus)) busResolverDictionaries.Add(bus, new Dictionary<Type, Delegate>());

			IEnumerable<Type> consumerTypes = typeof(IConsumer<>).ByImplementedInterfaceInDirectory(directory, wildcard, @namespace);
			foreach (var consumerType in consumerTypes)
			{
				var pipeImplementationInterface =
					consumerType.GetInterfaces()
						.FirstOrDefault(t => !t.IsGenericTypeDefinition && t.GetGenericTypeDefinition() == typeof (IPipe<,>));
				if (pipeImplementationInterface != null)
				{
					var translatorType = consumerType;
					var messageTypes = pipeImplementationInterface.GetGenericTypeArguments();

					// get instance of pipe
					var translatorInstance = busResolverDictionaries[bus].ContainsKey(translatorType)
						? InvokeFunc(busResolverDictionaries[bus][translatorType])
						: Activator.CreateInstance(translatorType);

					// get instance of the helper that will help add specific handler
					// code to the bus.
					var helperType1 = typeof (PipeAttachConsumerHelper<,>).MakeGenericType(messageTypes);
					var helperType1Instance = Activator.CreateInstance(helperType1);
					var attachConsumerMethodInfo = helperType1.GetMethod("AttachConsumer");
					attachConsumerMethodInfo.Invoke(helperType1Instance, new[] {translatorInstance, bus});

					var inType = messageTypes[0];
					var helperType = typeof (BusAddhHandlerHelper<>).MakeGenericType(inType);
					var helperInstance = Activator.CreateInstance(helperType);
					var addHandlerMethodInfo = helperType.GetMethod("AddHandler");

					addHandlerMethodInfo.Invoke(helperInstance, new[] {bus, translatorInstance});
				}
				else
				{
					var consumerImplementationInterface =
						consumerType.GetInterfaces()
							.FirstOrDefault(t => !t.IsGenericTypeDefinition && t.GetGenericTypeDefinition() == typeof (IConsumer<>));
					Debug.Assert(consumerImplementationInterface != null,
						"Unexpected state enumerating implementations of IConsumer<T>");

					var messageTypes = consumerImplementationInterface.GetGenericTypeArguments();

					var helperType = typeof (BusAddhHandlerHelper<>).MakeGenericType(messageTypes);
					var helperInstance = Activator.CreateInstance(helperType);
					var addHandlerMethodInfo = helperType.GetMethod("AddHandler");

					var handlerInstance = busResolverDictionaries[bus].ContainsKey(consumerType)
						? InvokeFunc(busResolverDictionaries[bus][consumerType])
						: Activator.CreateInstance(consumerType);

					addHandlerMethodInfo.Invoke(helperInstance, new[] {bus, handlerInstance});
				}
			}
		}

		/// <summary>
		/// Alias to be explicit about the intent of handling an <see cref="IMessage"/> instance.
		/// </summary>
		/// <typeparam name="TMessage">IMessage-based type to send</typeparam>
		/// <param name="bus">bus to send within</param>
		/// <param name="message">Message to send</param>
		public static void Send<TMessage>(this IBus bus, TMessage message) where TMessage : IMessage
		{
			bus.Handle(message);
		}

		/// <summary>
		/// Aliaos to be explcit about the intent of handling an <see cref="IEvent"/> instance.
		/// </summary>
		/// <typeparam name="TEvent">IEvent-based type to publish</typeparam>
		/// <param name="bus">bus to send within</param>
		/// <param name="event">Event to publish</param>
		public static void Publish<TEvent>(this IBus bus, TEvent @event) where TEvent : IEvent
		{
			bus.Handle(@event);
		}

		/// <summary>
		/// Perform a request/response
		/// </summary>
		/// <typeparam name="TMessage">The type of the message being sent</typeparam>
		/// <typeparam name="TEvent">The type of the event for the response</typeparam>
		/// <param name="bus">The bus to send/receive from</param>
		/// <param name="message">The message to send</param>
		/// <returns>The event response</returns>
		public static Task<TEvent> RequestAsync<TMessage, TEvent>(this IBus bus, TMessage message) where TMessage : IMessage
			where TEvent : IEvent
		{
			return RequestAsync<TMessage, TEvent>(bus, message, CancellationToken.None);
		}

		/// <summary>
		/// Perform a request/response
		/// </summary>
		/// <typeparam name="TMessage">The type of the message being sent</typeparam>
		/// <typeparam name="TEvent">The type of the event for the response</typeparam>
		/// <param name="bus">The bus to send/receive from</param>
		/// <param name="message">The message to send</param>
		/// <param name="cancellationToken">CancellationToken to use to cancel or timeout.</param>
		/// <returns>The event response</returns>
		public static Task<TEvent> RequestAsync<TMessage, TEvent>(this IBus bus, TMessage message, CancellationToken cancellationToken) where TMessage : IMessage
			where TEvent : IEvent
		{
			if (bus == null) throw new ArgumentNullException(nameof(bus));
			if (message == null) throw new ArgumentNullException(nameof(message));

			var tcs = new TaskCompletionSource<TEvent>();
			cancellationToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);
			ActionConsumer<TEvent> actionConsumer = null;
			actionConsumer = new ActionConsumer<TEvent>(e =>
			{
				if (actionConsumer != null) bus.RemoveHandler(actionConsumer);
				tcs.SetResult(e);
			});
			bus.AddHandler(actionConsumer);
			bus.Send(message);
			return tcs.Task;
		}

		/// <summary>
		/// Given only a delegate, invoke it via reflection
		/// </summary>
		/// <param name="func">The delegate to invoke</param>
		/// <returns>Whatever <paramref name="func"/> returns</returns>
		private static object InvokeFunc(Delegate func)
		{
			return func.Method.Invoke(func.Target, null);
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