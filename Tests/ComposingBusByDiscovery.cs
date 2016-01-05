using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Compatibility;
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

			Assert.AreEqual(2, bus._consumerInvokers.Count);
		}

		[Test]
		public void CorrectConsumersFound()
		{
			var bus = new Bus();
			var directory = Path.GetDirectoryName(new Uri(GetType().Assembly.CodeBase).LocalPath);
			bus.AddHandlersAndTranslators(directory, "Tests*.dll", GetType().Namespace);

			Assert.IsTrue(bus._consumerInvokers.ContainsKey(typeof(Message2).MetadataToken));
			Assert.IsTrue(bus._consumerInvokers.ContainsKey(typeof(Message1).MetadataToken));
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
	}
}