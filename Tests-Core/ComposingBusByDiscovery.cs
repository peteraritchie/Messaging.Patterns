using System;
using System.Collections.Generic;
#if false
using System.Diagnostics;
#endif
using System.ComponentModel;
#if !(NETCOREAPP2_0 || NETCOREAPP1_1)
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
#endif
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using PRI.ProductivityExtensions.TemporalExtensions;
//using PRI.ProductivityExtensions.ReflectionExtensions;
using Xunit;
using Microsoft.Extensions.DependencyModel;
using Tests.Mocks;
using CommonServiceLocator;
using System.Diagnostics;
using PRI.ProductivityExtensions.ReflectionExtensions;

//using PRI.ProductivityExtensions.ReflectionExtensions;

#pragma warning disable S1172 // Unused method parameters should be removed
namespace Tests
{
	public class AssemblyLoader : AssemblyLoadContext
	{
		private string folderPath;

		public AssemblyLoader(string folderPath)
		{
			this.folderPath = folderPath;
		}

		protected override Assembly Load(AssemblyName assemblyName)
		{
			var deps = DependencyContext.Default;
			var res = deps.CompileLibraries.Where(d => d.Name.Contains(assemblyName.Name)).ToList();
			if (res.Count > 0)
			{
				return Assembly.Load(new AssemblyName(res.First().Name));
			}
			var apiApplicationFileInfo = new FileInfo($"{folderPath}{Path.DirectorySeparatorChar}{assemblyName.Name}.dll");
			if (!File.Exists(apiApplicationFileInfo.FullName)) return Assembly.Load(assemblyName);
			var asl = new AssemblyLoader(apiApplicationFileInfo.DirectoryName);
			return asl.LoadFromAssemblyPath(apiApplicationFileInfo.FullName);
		}
	}
	public static class X
	{
		/// <summary>
		/// Test if <param name="type"> implements interface <typeparamref name="TInterface"/>
		/// </summary>
		/// <typeparam name="TInterface"></typeparam>
		/// <param name="type"></param>
		/// <returns></returns>
		public static bool ImplementsInterface<TInterface>(this Type type)
#pragma warning restore CS1570 // XML comment has badly formed XML -- 'Expected an end tag for element 'summary'.'
#pragma warning restore CS1570 // XML comment has badly formed XML -- 'End tag 'summary' does not match the start tag 'param'.'
		{
			if (type == null) throw new ArgumentNullException(nameof(type));
			return typeof(TInterface).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo());
		}

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

		private static IEnumerable<TypeInfo> ByPredicate(IEnumerable<Assembly> assemblies, Predicate<TypeInfo> predicate)
		{
			return from assembly in assemblies
				from typeInfo in assembly.DefinedTypes
				where !typeInfo.IsAbstract && typeInfo.IsClass && predicate(typeInfo)
				select typeInfo;
		}

		//public static IEnumerable<Type> ByImplementedInterfaceInDirectory(this Type interfaceType, string directory, string wildcard, string @namespace)
		//{
		//	return ByPredicate(Directory.GetFiles(directory, wildcard).ToAssemblies(), type => ImplementsInterface(type, interfaceType)).Select(t=>t.AsType());
		//}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member 'IEnumerableable.ToAssemblies(IEnumerable<string>)'
		public static IEnumerable<Assembly> ToAssemblies(this IEnumerable<string> filenames)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member 'IEnumerableable.ToAssemblies(IEnumerable<string>)'
		{
			var loaders = new Dictionary<string, AssemblyLoadContext>();

			foreach (var f in filenames)
			{
				var directory = Path.GetDirectoryName(f);
				if(!loaders.ContainsKey(directory)) loaders.Add(directory, new AssemblyLoader(directory));
				Assembly assembly;
				try
				{
					assembly = Assembly.Load(new AssemblyName(Path.GetFileNameWithoutExtension(f)));
				}
				catch (BadImageFormatException)
				{
					// ignore anything that can't be loaded
					continue;
				}
				catch (ReflectionTypeLoadException)
				{
					// ignore anything that can't be loaded
					continue;
				}
				yield return assembly;
			}
		}
	}

	public class ComposingBusByDiscovery
	{
		private void M(int x)
		{
			return;
		}

		[Fact]
		public void TranslatorResolverIsInvoked()
		{
			var bus = new Bus();
			var calledCount = 0;
			bus.AddResolver(() =>
			{
				calledCount++;
				return new Pipe();
			});
			var directory = Path.GetDirectoryName(new Uri(GetType().GetTypeInfo().Assembly.CodeBase).LocalPath);
			IEnumerable<Type> types = typeof(IConsumer<>).ByImplementedInterfaceInDirectory(directory, "Tests*.dll", "Tests.Mocks");
			bus.AddHandlersAndTranslators(types);

			Assert.Equal(1, calledCount);
		}
#if true
		[Fact]
		public void HandlerResolverIsInvoked()
		{
			var bus = new Bus();
			var calledCount = 0;
			bus.AddResolver(() =>
			{
				calledCount++;
				return new Message2Consumer();
			});
			var directory = Path.GetDirectoryName(new Uri(GetType().GetTypeInfo().Assembly.CodeBase).LocalPath);
			bus.AddHandlersAndTranslators(directory, "Tests*.dll", "Tests.Mocks");

			Assert.Equal(1, calledCount);
		}

#if !(NETCOREAPP2_0 || NETCOREAPP1_1)
		[Fact]
		public void ServiceLocatorResolves()
		{
			var bus = new Bus();

#region composition root

			var catalog = new AggregateCatalog();
			catalog.Catalogs.Add(new AssemblyCatalog(typeof(Message2Consumer).GetTypeInfo().Assembly));
			var compositionContainer = new CompositionContainer(catalog);
			var message2Consumer = new Message2Consumer();
			compositionContainer.ComposeExportedValue(message2Consumer);
#if SUPPORT_ASYNC_CONSUMER
			var message3AsyncConsumer = new Message3AsyncConsumer();
			compositionContainer.ComposeExportedValue(message3AsyncConsumer);
#endif
			compositionContainer.ComposeExportedValue(new Pipe());
			compositionContainer.ComposeExportedValue(new Pipe23());
			ServiceLocator.SetLocatorProvider(() => new MefServiceLocator(compositionContainer));

#endregion

			bus.SetServiceLocator(ServiceLocator.Current);

			var directory = Path.GetDirectoryName(new Uri(GetType().GetTypeInfo().Assembly.CodeBase).LocalPath);
			bus.AddHandlersAndTranslators(directory, "Tests*.dll", "Tests.Mocks");

			bus.Handle(new Message2());

			Assert.Equal(1, message2Consumer.MessageReceivedCount);
		}

		[Fact]
		public void SecondServiceLocatorResolves()
		{
			var bus = new Bus();

#region composition root

			var catalog = new AggregateCatalog();
			catalog.Catalogs.Add(new AssemblyCatalog(typeof(Message2Consumer).GetTypeInfo().Assembly));
			var compositionContainer = new CompositionContainer(catalog);
			var message2Consumer = new Message2Consumer();
			ServiceLocator.SetLocatorProvider(() => new MefServiceLocator(compositionContainer));

#endregion

			bus.SetServiceLocator(new MefServiceLocator(compositionContainer));
			compositionContainer.ComposeExportedValue(message2Consumer);
#if SUPPORT_ASYNC_CONSUMER
			var message3AsyncConsumer = new Message3AsyncConsumer();
			compositionContainer.ComposeExportedValue(message3AsyncConsumer);
#endif
			compositionContainer.ComposeExportedValue(new Pipe());
			compositionContainer.ComposeExportedValue(new Pipe23());
			bus.SetServiceLocator(new MefServiceLocator(compositionContainer));

			var directory = Path.GetDirectoryName(new Uri(GetType().GetTypeInfo().Assembly.CodeBase).LocalPath);
			bus.AddHandlersAndTranslators(directory, "Tests*.dll", "Tests.Mocks");

			bus.Handle(new Message2());

			Assert.Equal(1, message2Consumer.MessageReceivedCount);
		}
#endif

		[Fact]
		public void NullBusThrowsWhenSettingServiceLocator()
		{
			Bus bus = null;
			// ReSharper disable once ExpressionIsAlwaysNull
			Assert.Throws<ArgumentNullException>(()=>bus.SetServiceLocator(null));
		}

		[Fact]
		public void NullServiceLocatorThrowsWhenSettingServiceLocator()
		{
			Bus bus = new Bus();
			Assert.Throws<ArgumentNullException>(() => bus.SetServiceLocator(null));
		}

		[Category("Performance")]
		public void MeasurePerformance()
		{
			var dictionary = new Dictionary<int, int>
			{
				{typeof (string).GetTypeInfo().MetadataToken, typeof (string).GetTypeInfo().MetadataToken},
				{typeof (bool).GetTypeInfo().MetadataToken, typeof (bool).GetTypeInfo().MetadataToken}
			};
			var stopwatch = Stopwatch.StartNew();
			var n = 5000000;
			for (int i = 0; i < n; ++i)
			{
				M(dictionary[typeof (string).GetTypeInfo().MetadataToken]);
			}
			Console.WriteLine(stopwatch.Elapsed.ToEnglishString());

			//setting
			LocalDataStoreSlot lds = System.Threading.Thread.AllocateNamedDataSlot("foo");
			System.Threading.Thread.SetData(lds, 42);

			//getting
			lds = System.Threading.Thread.GetNamedDataSlot("foo");
			stopwatch = Stopwatch.StartNew();
			for (int i = 0; i < n; ++i)
			{
				M((int) System.Threading.Thread.GetData(lds));
			}
			Console.WriteLine(stopwatch.Elapsed.ToEnglishString());
		}

		[Fact]
		public void CanFindConsumers()
		{
			var bus = new Bus();
			var directory = Path.GetDirectoryName(new Uri(GetType().GetTypeInfo().Assembly.CodeBase).LocalPath);
			bus.AddHandlersAndTranslators(directory, "Tests*.dll", "Tests.Mocks");

#if SUPPORT_ASYNC_CONSUMER
			Assert.Equal(3, bus._consumerInvokers.Count);
#else
			Assert.Equal(2, bus._consumerInvokers.Count);
#endif
		}

		[Fact]
		public void CorrectConsumersFound()
		{
			var bus = new Bus();
			var directory = Path.GetDirectoryName(new Uri(GetType().GetTypeInfo().Assembly.CodeBase).LocalPath);
			bus.AddHandlersAndTranslators(directory, "Tests*.dll", GetType().Namespace);

			// ReSharper disable AssignNullToNotNullAttribute
#if SUPPORT_ASYNC_CONSUMER
			Assert.True(bus._consumerInvokers.ContainsKey(typeof(Message3).AssemblyQualifiedName));
#endif
			Assert.True(bus._consumerInvokers.ContainsKey(typeof(Message2).AssemblyQualifiedName));
			Assert.True(bus._consumerInvokers.ContainsKey(typeof(Message1).AssemblyQualifiedName));
			// ReSharper restore AssignNullToNotNullAttribute
		}

		[Fact]
		public void DiscoveredBusConsumesMessageCorrectly()
		{
			var bus = new Bus();
			var directory = Path.GetDirectoryName(new Uri(GetType().GetTypeInfo().Assembly.CodeBase).LocalPath);
			bus.AddHandlersAndTranslators(directory, "Tests*.dll", GetType().Namespace);

			var message1 = new Message1 { CorrelationId = "1234" };
			bus.Handle(message1);

			Assert.Same(message1, Pipe.LastMessageProcessed);
			Assert.NotNull(Message2Consumer.LastMessageReceived);
			Assert.Equal(message1.CorrelationId, Message2Consumer.LastMessageReceived.CorrelationId);
		}

#if SUPPORT_ASYNC_CONSUMER
		[Fact]
		public async Task DiscoveredBusAsynchronousConsumerAsynchronouslyConsumesCorrectly()
		{
			var bus = new Bus();
			var directory = Path.GetDirectoryName(new Uri(GetType().GetTypeInfo().Assembly.CodeBase).LocalPath);
			bus.AddHandlersAndTranslators(directory, "Tests*.dll", GetType().Namespace);

			var message3 = new Message3 { CorrelationId = "1234" };
			await bus.HandleAsync(message3);

			Assert.NotNull(Message3AsyncConsumer.LastMessageReceived);
			Assert.Equal(message3.CorrelationId, Message3AsyncConsumer.LastMessageReceived.CorrelationId);
		}

		[Fact]
		public void DiscoveredBusAsynchronousConsumerSynchronouslyConsumesCorrectly()
		{
			var bus = new Bus();
			var directory = Path.GetDirectoryName(new Uri(GetType().GetTypeInfo().Assembly.CodeBase).LocalPath);
			bus.AddHandlersAndTranslators(directory, "Tests*.dll", GetType().Namespace);

			var message3 = new Message3 { CorrelationId = "1234" };
			bus.Handle(message3);

			Assert.NotNull(Message3AsyncConsumer.LastMessageReceived);
			Assert.Equal(message3.CorrelationId, Message3AsyncConsumer.LastMessageReceived.CorrelationId);
		}
#endif
#endif // false
	}
}
#pragma warning restore S1172 // Unused method parameters should be removed
