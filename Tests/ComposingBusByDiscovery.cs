using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Mef.CommonServiceLocator;
using Microsoft.Practices.ServiceLocation;
using NUnit.Framework;
using PRI.ProductivityExtensions.TemporalExtensions;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Extensions.Bus;
using Tests.Mocks;

namespace Tests
{
	[TestFixture]
	public class ComposingBusByDiscovery
	{
		private void M(int x)
		{
			return;
		}

		[Test]
		public void TranslatorResolverIsInvoked()
		{
			var bus = new Bus();
			var calledCount = 0;
			bus.AddResolver(() =>
			{
				calledCount++;
				return new Pipe();
			});
			var directory = Path.GetDirectoryName(new Uri(GetType().Assembly.CodeBase).LocalPath);
			bus.AddHandlersAndTranslators(directory, "Tests*.dll", "Tests.Mocks");

			Assert.AreEqual(1, calledCount);
		}

		[Test]
		public void HandlerResolverIsInvoked()
		{
			var bus = new Bus();
			var calledCount = 0;
			bus.AddResolver(() =>
			{
				calledCount++;
				return new Message2Consumer();
			});
			var directory = Path.GetDirectoryName(new Uri(GetType().Assembly.CodeBase).LocalPath);
			bus.AddHandlersAndTranslators(directory, "Tests*.dll", "Tests.Mocks");

			Assert.AreEqual(1, calledCount);
		}

		[Test]
		public void ServiceLocatorResolves()
		{
			var bus = new Bus();

			#region composition root

			var catalog = new AggregateCatalog();
			catalog.Catalogs.Add(new AssemblyCatalog(typeof(Message2Consumer).Assembly));
			var compositionContainer = new CompositionContainer(catalog);
			var message2Consumer = new Message2Consumer();
			compositionContainer.ComposeExportedValue(message2Consumer);
			var message3AsyncConsumer = new Message3AsyncConsumer();
			compositionContainer.ComposeExportedValue(message3AsyncConsumer);
			compositionContainer.ComposeExportedValue(new Pipe());
			compositionContainer.ComposeExportedValue(new Pipe23());
			ServiceLocator.SetLocatorProvider(() => new MefServiceLocator(compositionContainer));

			#endregion

			bus.SetServiceLocator(ServiceLocator.Current);

			var directory = Path.GetDirectoryName(new Uri(GetType().Assembly.CodeBase).LocalPath);
			bus.AddHandlersAndTranslators(directory, "Tests*.dll", "Tests.Mocks");

			bus.Handle(new Message2());

			Assert.AreEqual(1, message2Consumer.MessageReceivedCount);
		}

		[Test]
		public void NullBusThrowsWhenSettingServiceLocator()
		{
			Bus bus = null;
			// ReSharper disable once ExpressionIsAlwaysNull
			Assert.Throws<ArgumentNullException>(()=>bus.SetServiceLocator(null));
		}

		[Test]
		public void NullServiceLocatorThrowsWhenSettingServiceLocator()
		{
			Bus bus = new Bus();
			Assert.Throws<ArgumentNullException>(() => bus.SetServiceLocator(null));
		}

		[Test]
		public void SecondServiceLocatorResolves()
		{
			var bus = new Bus();

			#region composition root

			var catalog = new AggregateCatalog();
			catalog.Catalogs.Add(new AssemblyCatalog(typeof(Message2Consumer).Assembly));
			var compositionContainer = new CompositionContainer(catalog);
			var message2Consumer = new Message2Consumer();
			ServiceLocator.SetLocatorProvider(() => new MefServiceLocator(compositionContainer));

			#endregion

			bus.SetServiceLocator(new MefServiceLocator(compositionContainer));
			compositionContainer.ComposeExportedValue(message2Consumer);
			var message3AsyncConsumer = new Message3AsyncConsumer();
			compositionContainer.ComposeExportedValue(message3AsyncConsumer);
			compositionContainer.ComposeExportedValue(new Pipe());
			compositionContainer.ComposeExportedValue(new Pipe23());
			bus.SetServiceLocator(new MefServiceLocator(compositionContainer));

			var directory = Path.GetDirectoryName(new Uri(GetType().Assembly.CodeBase).LocalPath);
			bus.AddHandlersAndTranslators(directory, "Tests*.dll", "Tests.Mocks");

			bus.Handle(new Message2());

			Assert.AreEqual(1, message2Consumer.MessageReceivedCount);
		}

		[Test, Category("Performance")]
		public void MeasurePerformance()
		{
			var dictionary = new Dictionary<int, int>
			{
				{typeof (string).MetadataToken, typeof (string).MetadataToken},
				{typeof (bool).MetadataToken, typeof (bool).MetadataToken}
			};
			var stopwatch = Stopwatch.StartNew();
			var n = 5000000;
			for (int i = 0; i < n; ++i)
			{
				M(dictionary[typeof (string).MetadataToken]);
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

		[Test]
		public void CanFindConsumers()
		{
			var bus = new Bus();
			var directory = Path.GetDirectoryName(new Uri(GetType().Assembly.CodeBase).LocalPath);
			bus.AddHandlersAndTranslators(directory, "Tests*.dll", "Tests.Mocks");

			Assert.AreEqual(3, bus._consumerInvokers.Count);
		}

		[Test]
		public void CorrectConsumersFound()
		{
			var bus = new Bus();
			var directory = Path.GetDirectoryName(new Uri(GetType().Assembly.CodeBase).LocalPath);
			bus.AddHandlersAndTranslators(directory, "Tests*.dll", GetType().Namespace);

			// ReSharper disable AssignNullToNotNullAttribute
			Assert.IsTrue(bus._consumerInvokers.ContainsKey(typeof(Message3).AssemblyQualifiedName));
			Assert.IsTrue(bus._consumerInvokers.ContainsKey(typeof(Message2).AssemblyQualifiedName));
			Assert.IsTrue(bus._consumerInvokers.ContainsKey(typeof(Message1).AssemblyQualifiedName));
			// ReSharper restore AssignNullToNotNullAttribute
		}

		[Test]
		public void DiscoveredBusConsumesMessageCorrectly()
		{
			var bus = new Bus();
			var directory = Path.GetDirectoryName(new Uri(GetType().Assembly.CodeBase).LocalPath);
			bus.AddHandlersAndTranslators(directory, "Tests*.dll", GetType().Namespace);

			var message1 = new Message1 { CorrelationId = "1234" };
			bus.Handle(message1);

			Assert.AreSame(message1, Pipe.LastMessageProcessed);
			Assert.IsNotNull(Message2Consumer.LastMessageReceived);
			Assert.AreEqual(message1.CorrelationId, Message2Consumer.LastMessageReceived.CorrelationId);
		}

		[Test]
		public async Task DiscoveredBusAsynchronousConsumerAsynchronouslyConsumesCorrectly()
		{
			var bus = new Bus();
			var directory = Path.GetDirectoryName(new Uri(GetType().Assembly.CodeBase).LocalPath);
			bus.AddHandlersAndTranslators(directory, "Tests*.dll", GetType().Namespace);

			var message3 = new Message3 { CorrelationId = "1234" };
			await bus.HandleAsync(message3);

			Assert.IsNotNull(Message3AsyncConsumer.LastMessageReceived);
			Assert.AreEqual(message3.CorrelationId, Message3AsyncConsumer.LastMessageReceived.CorrelationId);
		}

		[Test]
		public void DiscoveredBusAsynchronousConsumerSynchronouslyConsumesCorrectly()
		{
			var bus = new Bus();
			var directory = Path.GetDirectoryName(new Uri(GetType().Assembly.CodeBase).LocalPath);
			bus.AddHandlersAndTranslators(directory, "Tests*.dll", GetType().Namespace);

			var message3 = new Message3 { CorrelationId = "1234" };
			bus.Handle(message3);

			Assert.IsNotNull(Message3AsyncConsumer.LastMessageReceived);
			Assert.AreEqual(message3.CorrelationId, Message3AsyncConsumer.LastMessageReceived.CorrelationId);
		}
	}
}