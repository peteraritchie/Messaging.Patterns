using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using PRI.ProductivityExtensions.TemporalExtensions;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Extensions.Bus;
using Tests.Mocks;

namespace Tests
{
	public class HandlingMessagesThenSpeedIsAcceptable
	{

		[Test, Explicit]
		public void CompareCapacity()
		{
			var message1 = new Message1 {CorrelationId = "1234"};

			var bus = new Bus();
			bus.AddHandler(new Message2Consumer());
			bus.AddTranslator(new Pipe());
			var stopwatch = Stopwatch.StartNew();
			for (var i = 0; i < 53000000; ++i)
				bus.Handle(message1);
			Console.WriteLine(stopwatch.Elapsed.ToEnglishString());

			var bus1 = new Bus();
			var directory = Path.GetDirectoryName(new Uri(GetType().Assembly.CodeBase).LocalPath);
			bus1.AddHandlersAndTranslators(directory, "Tests*.dll", GetType().Namespace);

			stopwatch = Stopwatch.StartNew();
			for (var i = 0; i < 53000000; ++i)
				bus1.Handle(message1);
			Console.WriteLine(stopwatch.Elapsed.ToEnglishString());
		}

		[Test , Explicit]
		public void MeasureCapacity()
		{
			var message1 = new Message2 {CorrelationId = "1234"};

			var bus = new Bus();
			var message2Consumer = new Message2Consumer();
			bus.AddHandler(message2Consumer);
			var stopwatch = Stopwatch.StartNew();
			var messageCount = 53000000;
			for (var i = 0; i < messageCount; ++i)
				bus.Handle(message1);
			Console.WriteLine("{0} million in {1}", messageCount/1.Million(), stopwatch.Elapsed.ToEnglishString());
			Console.WriteLine(message2Consumer.MessageReceivedCount);
			Assert.AreSame(message1, Message2Consumer.LastMessageReceived);
			Assert.AreEqual(messageCount, message2Consumer.MessageReceivedCount);

			message2Consumer = new Message2Consumer();
			stopwatch = Stopwatch.StartNew();
			for (var i = 0; i < messageCount; ++i)
				message2Consumer.Handle(message1);
			Console.WriteLine(stopwatch.Elapsed.ToEnglishString());
		}
	}
}