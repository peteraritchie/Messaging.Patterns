#if (NET45 || NET451 || NET452 || NET46 || NET461 || NET462)
#define SERVICE_LOCATOR_SUPPORT
#endif
#if (NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6 || NETSTANDARD20)
#define SERVICE_LOCATOR_SUPPORT
//#define SERVICE_PROVIDER_SUPPORT
#endif

#if (NET45 || NET451 || NET452 || NET46 || NET461 || NET462)
using System.Diagnostics.CodeAnalysis;
#endif
#if SERVICE_LOCATOR_SUPPORT
using CommonServiceLocator;
//using Microsoft.Practices.ServiceLocation;
#elif SERVICE_PROVIDER_SUPPORT
using CommonServiceLocator;
#endif

using System;
using System.Linq;
using PRI.Messaging.Primitives;
using PRI.ProductivityExtensions.ReflectionExtensions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
//#if (NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6 || NETSTANDARD20)
using System.Reflection;
//#endif
#if SERVICE_PROVIDER_SUPPORT
using Microsoft.Extensions.DependencyInjection;
#endif
using PRI.Messaging.Patterns.Exceptions;

namespace PRI.Messaging.Patterns.Extensions.Bus
{
	public static class BusExtensions
	{
		private static readonly Dictionary<IBus, Dictionary<string, Delegate>> busResolverDictionaries = new Dictionary<IBus, Dictionary<string, Delegate>>();
#if SERVICE_LOCATOR_SUPPORT
		private static readonly Dictionary<IBus, IServiceLocator> busServiceLocators = new Dictionary<IBus, IServiceLocator>();
#elif SERVICE_PROVIDER_SUPPORT
		private static readonly Dictionary<IBus, IServiceProvider> busServiceProviders = new Dictionary<IBus, IServiceProvider>();
#endif

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
		public static void AddResolver<T>(this IBus bus, Func<T> resolver)
		{
			if(!busResolverDictionaries.ContainsKey(bus))
				busResolverDictionaries.Add(bus, new Dictionary<string, Delegate>());
			var dictionary = busResolverDictionaries[bus];
#if (NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6)
			var key = typeof (T).AssemblyQualifiedName ?? typeof(T).GetTypeInfo().GUID.ToString();
#else
			var key = typeof (T).AssemblyQualifiedName ?? typeof(T).GUID.ToString();
#endif
			dictionary[key] = resolver;
		}

#if SERVICE_LOCATOR_SUPPORT
		public static void SetServiceLocator(this IBus bus, IServiceLocator serviceLocator)
		{
			if (bus == null)
				throw new ArgumentNullException(nameof(bus));
			if (serviceLocator == null)
				throw new ArgumentNullException(nameof(serviceLocator));
			if (!busServiceLocators.ContainsKey(bus))
				busServiceLocators.Add(bus, serviceLocator);
			else
				busServiceLocators[bus] = serviceLocator;
		}

		/// <summary>
		/// A private class to handle Activator creations as a service locator type
		/// </summary>
		private class ActivatorServiceLocator : ServiceLocatorImplBase
		{
			protected override object DoGetInstance(Type serviceType, string key)
			{
				return Activator.CreateInstance(serviceType);
			}

#if !(NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6)
			[ExcludeFromCodeCoverage]
#endif
			protected override IEnumerable<object> DoGetAllInstances(Type serviceType)
			{
				throw new NotImplementedException();
			}
		}
#elif SERVICE_PROVIDER_SUPPORT
		public static void SetServiceProvider(this IBus bus, IServiceProvider serviceProvider)
		{
			if (bus == null)
				throw new ArgumentNullException(nameof(bus));
			if (serviceProvider == null)
				throw new ArgumentNullException(nameof(serviceProvider));
			if (!busServiceProviders.ContainsKey(bus))
				busServiceProviders.Add(bus, serviceProvider);
			else
				busServiceProviders[bus] = serviceProvider;
		}

		/// <summary>
		/// A private class to handle Activator creations as a service locator type
		/// </summary>
		private class ActivatorServiceProvider : IServiceProvider
		{
			public object GetService(Type serviceType)
			{
				return Activator.CreateInstance(serviceType);
			}
		}
#endif

		// NETSTANDARD1_3 || NETSTANDARD1_4 || 
#if (NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6 || NETSTANDARD20 || NET40 || NET45 || NET451 || NET452 || NET46 || NET461 || NET462 || NET47)
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
		/// <param name="namespace">Include IConsumers{TMessage} within this namespace</param>
		public static void AddHandlersAndTranslators(this IBus bus, string directory, string wildcard, string @namespace)
		{
			IEnumerable<Type> consumerTypes = typeof(IConsumer<>)./*GetTypeInfo().*/ByImplementedInterfaceInDirectory(directory, wildcard, @namespace);
			AddHandlersAndTranslators(bus, consumerTypes);
		}
#endif

		/// <summary>
		/// Dynamically load message handlers from a collection of types.
		/// </summary>
		/// <param name="bus">The <seealso cref="IBus"/> instance this will apply to</param>
		/// <param name="consumerTypes">Types to test for being a handler and load if so.</param>
		public static void AddHandlersAndTranslators(this IBus bus, IEnumerable<Type> consumerTypes)
		{
			if (!busResolverDictionaries.ContainsKey(bus))
				busResolverDictionaries.Add(bus, new Dictionary<string, Delegate>());
#if SERVICE_LOCATOR_SUPPORT
			IServiceLocator serviceLocator = busServiceLocators.ContainsKey(bus)
				? busServiceLocators[bus]
				: new ActivatorServiceLocator();
#elif SERVICE_PROVIDER_SUPPORT
			IServiceProvider serviceProvider = busServiceProviders.ContainsKey(bus)
				? busServiceProviders[bus]
				: new ActivatorServiceProvider();
#endif

			foreach (var consumerType in consumerTypes)
			{
#if (NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6)
				var consumerTypeInterfaces = consumerType.GetTypeInfo().ImplementedInterfaces;
				var pipeImplementationInterface =
					consumerTypeInterfaces
						.FirstOrDefault(t => t.GetTypeInfo().IsGenericType && !t.GetTypeInfo().IsGenericTypeDefinition &&
						                     t.GetGenericTypeDefinition() == typeof(IPipe<,>));
#else
				var consumerTypeInterfaces = consumerType.GetInterfaces();
				var pipeImplementationInterface =
					consumerTypeInterfaces
						.FirstOrDefault(t => t.IsGenericType && !t.IsGenericTypeDefinition &&
						                     t.GetGenericTypeDefinition() == typeof(IPipe<,>));
#endif

				if (pipeImplementationInterface != null)
				{
					var translatorType = consumerType;
					var messageTypes = pipeImplementationInterface.GetGenericTypeArguments();
#if (NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6)
					var key = translatorType.AssemblyQualifiedName ?? translatorType.GetTypeInfo().GUID.ToString();
#else
					var key = translatorType.AssemblyQualifiedName ?? translatorType.GUID.ToString();
#endif
					// get instance of pipe
					var translatorInstance = busResolverDictionaries[bus].ContainsKey(key)
						? InvokeFunc(busResolverDictionaries[bus][key])
#if SERVICE_LOCATOR_SUPPORT
						: serviceLocator.GetInstance(translatorType);
#elif SERVICE_PROVIDER_SUPPORT
						: serviceProvider.GetService(translatorType);
#else
						: null;
#endif
					// get instance of the helper that will help add specific handler
					// code to the bus.
					var helperType1 = typeof(PipeAttachConsumerHelper<,>).MakeGenericType(messageTypes);
					var helperType1Instance = Activator.CreateInstance(helperType1);
#if (NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6)
					var attachConsumerMethodInfo = helperType1.GetTypeInfo().GetDeclaredMethod("AttachConsumer");
#else
					var attachConsumerMethodInfo = helperType1.GetMethod("AttachConsumer");
#endif
					attachConsumerMethodInfo.Invoke(helperType1Instance, new[] {translatorInstance, bus});

					var inType = messageTypes[0];
					var helperType = typeof(BusAddHandlerHelper<>).MakeGenericType(inType);
					var helperInstance = Activator.CreateInstance(helperType);
#if (NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6)
					var addHandlerMethodInfo = helperType.GetTypeInfo().GetDeclaredMethod("AddHandler");
#else
					var addHandlerMethodInfo = helperType.GetMethod("AddHandler");
#endif

					addHandlerMethodInfo.Invoke(helperInstance, new[] {bus, translatorInstance});
				}
				else
				{
					var consumerImplementationInterface =
						consumerTypeInterfaces
#if (NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6)
						.FirstOrDefault(t => t.GetTypeInfo().IsGenericType && !t.GetTypeInfo().IsGenericTypeDefinition && t.GetGenericTypeDefinition() == typeof (IConsumer<>));
#else
							.FirstOrDefault(t => t.IsGenericType && !t.IsGenericTypeDefinition &&
							                     t.GetGenericTypeDefinition() == typeof(IConsumer<>));
#endif
					Debug.Assert(consumerImplementationInterface != null,
						"Unexpected state enumerating implementations of IConsumer<T>");

					var messageTypes = consumerImplementationInterface.GetGenericTypeArguments();

					var helperType = typeof(BusAddHandlerHelper<>).MakeGenericType(messageTypes);
					var helperInstance = Activator.CreateInstance(helperType);
#if (NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6)
					var addHandlerMethodInfo = helperType.GetTypeInfo().GetDeclaredMethod("AddHandler");
					var key = consumerType.AssemblyQualifiedName ?? consumerType.GetTypeInfo().GUID.ToString();
#else
					var addHandlerMethodInfo = helperType.GetMethod("AddHandler");
					var key = consumerType.AssemblyQualifiedName ?? consumerType.GUID.ToString();
#endif

					var handlerInstance = busResolverDictionaries[bus].ContainsKey(key)
						? InvokeFunc(busResolverDictionaries[bus][key])
#if SERVICE_LOCATOR_SUPPORT
						: serviceLocator.GetInstance(consumerType);
#elif SERVICE_PROVIDER_SUPPORT
						: serviceProvider.GetService(consumerType);
#else
						: null;
#endif
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
		/// Alias to be explicit about the intent of handling an <see cref="IEvent"/> instance.
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
			if (bus == null)
				throw new ArgumentNullException(nameof(bus));
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			var tcs = new TaskCompletionSource<TEvent>();
			ActionConsumer<TEvent> actionConsumer = null;
			object token = null;
			cancellationToken.Register(() =>
			{
				if (actionConsumer != null)
#if PARANOID
				{
					var internalBus = bus as Patterns.Bus;
					if(internalBus != null)
						internalBus.RemoveHandler(actionConsumer, token, nocheck: true);
					else
						bus.RemoveHandler(actionConsumer, token);
				}
#else
					bus.RemoveHandler(actionConsumer, token);
#endif // PARANOID
				tcs.TrySetCanceled();
			}, useSynchronizationContext: false);
			actionConsumer = new ActionConsumer<TEvent>(e =>
			{
				try
				{
					if (e.CorrelationId != message.CorrelationId)
						return;
					if (actionConsumer != null)
						bus.RemoveHandler(actionConsumer, token);
					tcs.SetResult(e);
				}
				catch (Exception ex)
				{
					tcs.SetException(ex);
				}
			});
			token = bus.AddHandler(actionConsumer);
			bus.Send(message);
			return tcs.Task;
		}

		/// <summary>
		/// Perform a request/response
		/// </summary>
		/// <typeparam name="TMessage">The type of the message being sent</typeparam>
		/// <typeparam name="TSuccessEvent">The type of the event for the response</typeparam>
		/// <typeparam name="TErrorEvent">The type of the event in case of error.</typeparam>
		/// <param name="bus">The bus to send/receive from</param>
		/// <param name="message">The message to send</param>
		/// <returns>The event response</returns>
		public static Task<TSuccessEvent> RequestAsync<TMessage, TSuccessEvent, TErrorEvent>(this IBus bus, TMessage message)
			where TMessage : IMessage
			where TSuccessEvent : IEvent
			where TErrorEvent : IEvent
		{
			return RequestAsync<TMessage, TSuccessEvent, TErrorEvent>(bus, message, CancellationToken.None);
		}

		/// <summary>
		/// Perform a request/response
		/// </summary>
		/// <typeparam name="TMessage">The type of the message being sent</typeparam>
		/// <typeparam name="TSuccessEvent">The type of the event for the response</typeparam>
		/// <param name="bus">The bus to send/receive from</param>
		/// <param name="message">The message to send</param>
		/// <param name="cancellationToken">CancellationToken to use to cancel or timeout.</param>
		/// <returns>The event response</returns>
		public static Task<TSuccessEvent> RequestAsync<TMessage, TSuccessEvent, TErrorEvent>(this IBus bus, TMessage message, CancellationToken cancellationToken) where TMessage : IMessage
			where TSuccessEvent : IEvent
			where TErrorEvent : IEvent
		{
			if (bus == null)
				throw new ArgumentNullException(nameof(bus));
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			var tcs = new TaskCompletionSource<TSuccessEvent>();
			ActionConsumer<TSuccessEvent> successActionConsumer = null;
			ActionConsumer<TErrorEvent> errorActionConsumer = null;
			object successToken = null;
			object errorToken = null;
			cancellationToken.Register(() =>
			{
				if (successActionConsumer != null)
#if PARANOID
				{
					var internalBus = bus as Patterns.Bus;
					if (internalBus != null)
						internalBus.RemoveHandler(successActionConsumer, successToken, nocheck: true);
					else
						bus.RemoveHandler(successActionConsumer, successToken);
				}
#else
					bus.RemoveHandler(successActionConsumer, successToken);//, nocheck:true);
#endif // PARANOID
				if (errorActionConsumer != null)
#if PARANOID
				{
					var internalBus = bus as Patterns.Bus;
					if (internalBus != null)
						internalBus.RemoveHandler(errorActionConsumer, errorToken, nocheck: true);
					else
						bus.RemoveHandler(errorActionConsumer, errorToken);
				}
#else
					bus.RemoveHandler(errorActionConsumer, errorToken);
#endif // PARANOID
				tcs.TrySetCanceled();
			}, useSynchronizationContext: false);
			{
				successActionConsumer = new ActionConsumer<TSuccessEvent>(e =>
				{
					try
					{
						if (e.CorrelationId != message.CorrelationId)
							return;
						if (successActionConsumer != null)
							bus.RemoveHandler(successActionConsumer, successToken);
						if (errorActionConsumer != null)
#if PARANOID
						{
							var internalBus = bus as Patterns.Bus;
							// don't check for unhandled error if success occured
							if (internalBus != null)
								internalBus.RemoveHandler(errorActionConsumer, errorToken, nocheck: true);
							else
								bus.RemoveHandler(errorActionConsumer, errorToken);
						}
#else
					bus.RemoveHandler(errorActionConsumer, errorToken);
#endif // PARANOID
						tcs.SetResult(e);
					}
					catch (Exception ex)
					{
						tcs.SetException(ex);
					}
				});
				successToken = bus.AddHandler(successActionConsumer);
			}
			{
				errorActionConsumer = new ActionConsumer<TErrorEvent>(e =>
				{
					try
					{
						if (e.CorrelationId != message.CorrelationId)
							return;
						if (errorActionConsumer != null)
							bus.RemoveHandler(errorActionConsumer, errorToken);
						if (successActionConsumer != null)
#if PARANOID
						{
							var internalBus = bus as Patterns.Bus;
							// don't check for unhandled success if error occured
							if (internalBus != null)
								internalBus.RemoveHandler(successActionConsumer, successToken, nocheck: true);
							else
								bus.RemoveHandler(successActionConsumer, successToken);
						}
#else
					bus.RemoveHandler(successActionConsumer, successToken);//, nocheck:true);
#endif // PARANOID
						tcs.SetException(new ReceivedErrorEventException<TErrorEvent>(e));
					}
					catch (Exception ex)
					{
						tcs.SetException(ex);
					}
				});
				errorToken = bus.AddHandler(errorActionConsumer);
			}
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
#if (NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6)
			return func.GetMethodInfo().Invoke(func.Target, null);
#else
			return func.Method.Invoke(func.Target, null);
#endif
		}

		private class BusAddHandlerHelper<TMessage> where TMessage : IMessage
		{
			[UsedImplicitly]
			public void AddHandler(IBus bus, IConsumer<TMessage> consumer)
			{
				bus.AddHandler(consumer);
			}
		}

		private class PipeAttachConsumerHelper<TIn, TOut>
			where TIn : IMessage
			where TOut : IMessage
		{
			[UsedImplicitly]
			public void AttachConsumer(IPipe<TIn, TOut> pipe, IConsumer<TOut> bus)
			{
				pipe.AttachConsumer(new ActionConsumer<TOut>(bus.Handle));
			}
		}
	}

//	public static class X
//	{
//		public static IEnumerable<Type> ByImplementedInterfaceInDirectory(this Type interfaceType, string directory, string wildcard, string namespaceName)
//		{
//			if (interfaceType == null) throw new ArgumentNullException(nameof(interfaceType));
//			if (directory == null) throw new ArgumentNullException(nameof(directory));
//			if (wildcard == null) throw new ArgumentNullException(nameof(wildcard));
//			if (namespaceName == null) throw new ArgumentNullException(nameof(namespaceName));
//			if (!interfaceType.GetTypeInfo().IsInterface)
//			{
//				throw new ArgumentException("Type is not an interface", nameof(interfaceType));
//			}

//			return ByPredicate(
//					System.IO.Directory.GetFiles(directory, wildcard).ToAssemblies(),
//					type => (type.Namespace ?? string.Empty).StartsWith(namespaceName) && type.ImplementsInterface(interfaceType))
//				.Select(t => t.AsType());
//		}

//		public static bool ImplementsInterface(this TypeInfo typeTypeInfo, TypeInfo interfaceTypeInfo)
//		{
//			if (interfaceTypeInfo == null) throw new ArgumentNullException(nameof(interfaceTypeInfo));
//			if (typeTypeInfo == null) throw new ArgumentNullException(nameof(typeTypeInfo));

//			var interfaceType = interfaceTypeInfo.AsType();
//			if (interfaceTypeInfo.IsGenericType && interfaceTypeInfo.ContainsGenericParameters)
//			{
//				return interfaceTypeInfo.ImplementedInterfaces
//					.Select(t => t.GetTypeInfo())
//					.Any(t => t.IsGenericType && t.GetGenericTypeDefinition() == interfaceType);
//			}
//			return interfaceTypeInfo.IsAssignableFrom(typeTypeInfo);
//		}
//		public static bool ImplementsInterface(this TypeInfo typeTypeInfo, Type interfaceType)
//		{
//			if (interfaceType == null) throw new ArgumentNullException(nameof(interfaceType));
//			if (typeTypeInfo == null) throw new ArgumentNullException(nameof(typeTypeInfo));

//			var interfaceTypeInfo = interfaceType.GetTypeInfo();
//			if (interfaceTypeInfo.IsGenericType && interfaceTypeInfo.ContainsGenericParameters)
//			{
//				return typeTypeInfo.Im.ImplementedInterfaces
//					.Select(t => t.GetTypeInfo())
//					.Any(t => t.IsGenericType && t.GetGenericTypeDefinition() == interfaceType);
//			}
//			return interfaceTypeInfo.IsAssignableFrom(typeTypeInfo);
//		}

//		private static IEnumerable<TypeInfo> ByPredicate(IEnumerable<Assembly> assemblies, Predicate<TypeInfo> predicate)
//		{
//			var assemblyTypes = from assembly in assemblies
//				from type in assembly.DefinedTypes
//				select type;
//			var nonAbstractClasses = from type in assemblyTypes
//				where !type.IsAbstract && type.IsClass
//				select type;
//			var types = from type in nonAbstractClasses
//				where predicate(type)
//				select type;
//			return types;
//		}

//		public static IEnumerable<Assembly> ToAssemblies(this IEnumerable<string> filenames)
//#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member 'IEnumerableable.ToAssemblies(IEnumerable<string>)'
//		{
//			foreach (var f in filenames)
//			{
//				Assembly loadFrom;
//#if (NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6 || NETCOREAPP1_0 || NETCOREAPP1_1)
//				var dir = System.IO.Directory.GetCurrentDirectory();
//#endif
//				try
//				{
//#if (NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6 || NETCOREAPP1_0 || NETCOREAPP1_1)
//					System.IO.Directory.SetCurrentDirectory(System.IO.Path.GetDirectoryName(f));
//					loadFrom = Assembly.Load(new AssemblyName(System.IO.Path.GetFileNameWithoutExtension(f)));
//#elif (!NETSTANDARD1_0 && !NETSTANDARD1_1 && !NETSTANDARD1_2)
//					loadFrom = Assembly.LoadFrom(f);
//#endif
//				}
//				catch (BadImageFormatException)
//				{
//					// ignore anything that can't be loaded
//					continue;
//				}
//				catch (ReflectionTypeLoadException)
//				{
//					// ignore anything that can't be loaded
//					continue;
//				}
//#if (NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6 || NETCOREAPP1_0 || NETCOREAPP1_1)
//				finally
//				{
//					System.IO.Directory.SetCurrentDirectory(dir);
//				}
//#endif
//				yield return loadFrom;
//			}
//		}
//	}
}

namespace JetBrains.Annotations
{
	/// <summary>
	/// Indicates that the marked symbol is used implicitly (e.g. via reflection, in external library),
	/// so this symbol will not be marked as unused (as well as by other usage inspections).
	/// </summary>
	[AttributeUsage(AttributeTargets.All)]
#if NET45
	[ExcludeFromCodeCoverage]
#endif
	internal sealed class UsedImplicitlyAttribute : Attribute
	{
		public UsedImplicitlyAttribute()
			: this(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.Default)
		{
		}

		public UsedImplicitlyAttribute(ImplicitUseKindFlags useKindFlags)
			: this(useKindFlags, ImplicitUseTargetFlags.Default)
		{
		}

		public UsedImplicitlyAttribute(ImplicitUseTargetFlags targetFlags)
			: this(ImplicitUseKindFlags.Default, targetFlags)
		{
		}

		public UsedImplicitlyAttribute(ImplicitUseKindFlags useKindFlags, ImplicitUseTargetFlags targetFlags)
		{
			UseKindFlags = useKindFlags;
			TargetFlags = targetFlags;
		}

		public ImplicitUseKindFlags UseKindFlags { get; private set; }

		public ImplicitUseTargetFlags TargetFlags { get; private set; }
	}

	/// <summary>
	/// Specify what is considered used implicitly when marked
	/// with <see cref="JetBrains.Annotations.MeansImplicitUseAttribute"/> or <see cref="UsedImplicitlyAttribute"/>.
	/// </summary>
	[Flags]
	internal enum ImplicitUseTargetFlags
	{
		Default = Itself,
		Itself = 1,
		/// <summary>Members of entity marked with attribute are considered used.</summary>
		Members = 2,
		/// <summary>Entity marked with attribute and all its members considered used.</summary>
		WithMembers = Itself | Members
	}

	[Flags]
	internal enum ImplicitUseKindFlags
	{
		Default = Access | Assign | InstantiatedWithFixedConstructorSignature,
		/// <summary>Only entity marked with attribute considered used.</summary>
		Access = 1,
		/// <summary>Indicates implicit assignment to a member.</summary>
		Assign = 2,
		/// <summary>
		/// Indicates implicit instantiation of a type with fixed constructor signature.
		/// That means any unused constructor parameters won't be reported as such.
		/// </summary>
		InstantiatedWithFixedConstructorSignature = 4,
		/// <summary>Indicates implicit instantiation of a type.</summary>
		InstantiatedNoFixedConstructorSignature = 8,
	}

#if false
	public static class X
	{
#if (NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6)
		public static bool ImplementsInterface(this TypeInfo type, Type interfaceType)
		{
			if (interfaceType == null) throw new ArgumentNullException(nameof(interfaceType));
			if (type == null) throw new ArgumentNullException(nameof(type));
			var typeInfo = interfaceType.GetTypeInfo();
			if (typeInfo.IsGenericType && typeInfo.ContainsGenericParameters)
			{
				return type.ImplementedInterfaces.Any(t => t.GetTypeInfo().IsGenericType && t.GetTypeInfo().GetGenericTypeDefinition() == interfaceType);
			}
			return typeInfo.IsAssignableFrom(type);
		}
#endif
		public static IEnumerable<Type> ByImplementedInterfaceInDirectory(this Type interfaceType, string directory, string wildcard, string namespaceName)
		{
#if (NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6)
			if (!interfaceType.GetTypeInfo().IsInterface)
#else
			if (!interfaceType.IsInterface)
#endif
			{
				throw new ArgumentException("Type is not an interface", "TInterface");
			}

			return ByPredicate(
				System.IO.Directory.GetFiles(directory, wildcard).ToAssemblies(),
				type => (type.Namespace ?? string.Empty).StartsWith(namespaceName) && type.ImplementsInterface(interfaceType));
		}

		private static IEnumerable<TypeInfo> ByPredicate(IEnumerable<Assembly> assemblies, Predicate<TypeInfo> predicate)
		{
			return from assembly in assemblies
#if (NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4)
				from type in assembly.DefinedTypes
#else
				   from type in assembly.GetTypes()
#endif
				where !type.IsAbstract && type.IsClass && predicate(type)
				select type;
		}
	}
#endif
}
