using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using Tests.Mocks;

namespace Tests
{
	[TestFixture]
	public class BusTests
	{
		[Test]
		public void BusConsumesMessagesCorrectly()
		{
			var bus = new Bus();
			Message1 receivedMessage = null;
			bus.AddHandler(new ActionConsumer<Message1>(m=>receivedMessage = m));

			var message1 = new Message1 { CorrelationId = "1234" };
			bus.Handle(message1);

			Assert.AreSame(message1, receivedMessage);
			Assert.IsNotNull(receivedMessage);
			Assert.AreEqual(message1.CorrelationId, receivedMessage.CorrelationId);
		}

#if PARANOID
		[Test]
		public void RemovingHandlerBeforeProcessingThrows()
		{
			var bus = new Bus();
			var actionConsumer = new ActionConsumer<Message1>(m => { });
			var token = bus.AddHandler(actionConsumer);
			Assert.Throws<MessageHandlerRemovedBeforeProcessingMessageException<Message1>>(()=>bus.RemoveHandler(actionConsumer, token));
		}
#endif // PARANOID

		[Test]
		public void MultipleAsyncHandlersOfSameMessageDoesntThrow()
		{
			using (var bus = new Bus())
			{
				bus.AddHandler(new AsyncActionConsumer<Message1>(m => Task.FromResult(0)));
				bus.AddHandler(new AsyncActionConsumer<Message1>(m => Task.FromResult(1)));
			}
		}

		public class TheEvent : IEvent
		{
			public TheEvent()
			{
				CorrelationId = Guid.NewGuid().ToString("D");
				OccurredDateTime = DateTime.UtcNow;
			}

			public string CorrelationId { get; set; }
			public DateTime OccurredDateTime { get; set; }
		}

		[Test]
		public void EnsureInterfaceHandlerIsInvoked()
		{
			var bus = new Bus();
			Message1 receivedMessage = null;
			bus.AddHandler(new ActionConsumer<Message1>(m => receivedMessage = m));
			string text = null;
			bus.AddHandler(new ActionConsumer<IEvent>(_=> { text = "ding"; }));

			var message1 = new Message1 { CorrelationId = "1234" };
			bus.Handle(message1);
			bus.Handle(new TheEvent());
			Assert.AreSame(message1, receivedMessage);
			Assert.IsNotNull(receivedMessage);
			Assert.AreEqual(message1.CorrelationId, receivedMessage.CorrelationId);
			Assert.AreEqual("ding", text);
		}

		[Test]
		public void EnsureWithMultipleMessageTypesInterfaceHandlerIsInvoked()
		{
			var bus = new Bus();
			Message1 receivedMessage1 = null;
			bus.AddHandler(new ActionConsumer<Message1>(m => receivedMessage1 = m));
			Message2 receivedMessage2 = null;
			bus.AddHandler(new ActionConsumer<Message2>(m => receivedMessage2 = m));
			string text = null;
			bus.AddHandler(new ActionConsumer<IEvent>(_ => { text = "ding"; }));

			var message1 = new Message1 { CorrelationId = "1234" };
			bus.Handle(message1);
			bus.Handle(new TheEvent());
			Assert.AreSame(message1, receivedMessage1);
			Assert.IsNotNull(receivedMessage1);
			Assert.AreEqual(message1.CorrelationId, receivedMessage1.CorrelationId);
			Assert.AreEqual("ding", text);
		}

		public class Message1Specialization : Message1
		{
		}

		[Test]
		public void BaseTypeHandlerIsCalledCorrectly()
		{
			var bus = new Bus();
			Message1 receivedMessage1 = null;
			bus.AddHandler(new ActionConsumer<Message1>(m => receivedMessage1 = m));
			var message1 = new Message1Specialization { CorrelationId = "1234" };
			bus.Handle(message1);
			Assert.AreSame(message1, receivedMessage1);
		}

		public class Message1SpecializationSpecialization : Message1Specialization
		{
		}

		[Test]
		public void BaseBaseTypeHandlerIsCalledCorrectly()
		{
			var bus = new Bus();
			Message1 receivedMessage1 = null;
			bus.AddHandler(new ActionConsumer<Message1>(m => receivedMessage1 = m));
			var message1 = new Message1SpecializationSpecialization() { CorrelationId = "1234" };
			bus.Handle(message1);
			Assert.AreSame(message1, receivedMessage1);
		}

		[Test]
		public void RemoveUnsubscribedHandlerDoesNotThrow()
		{
			var bus = new Bus();
			Message1 receivedMessage1 = null;
			bus.RemoveHandler(new ActionConsumer<Message1>(m => receivedMessage1 = m));
		}

		[Test]
		public void RemoveLastSubscribedHandlerDoesNotThrow()
		{
			var bus = new Bus();
			Message1 receivedMessage1 = null;
			var token = bus.AddHandler(new ActionConsumer<Message1>(m => receivedMessage1 = m));
			bus.Handle(new Message1());
			bus.RemoveHandler(new ActionConsumer<Message1>(m => receivedMessage1 = m), token);
		}


		[Test]
		public void RemoveLastSubscribedHandlerClearsInternalDictionaries()
		{
			var bus = new Bus();
			Message1 receivedMessage1 = null;
			var token = bus.AddHandler(new ActionConsumer<Message1>(m => receivedMessage1 = m));
			bus.Handle(new Message1());
			bus.RemoveHandler(new ActionConsumer<Message1>(m => receivedMessage1 = m), token);
			Assert.AreEqual(0, bus._consumerInvokers.Count);
			Assert.AreEqual(0, bus._consumerInvokersDictionaries.Count);
		}

		[Test]
		public void InterleavedRemoveHandlerRemovesCorrectHandler()
		{
			string ordinal = null;
			var bus = new Bus();
			var actionConsumer1 = new ActionConsumer<Message1>(m => ordinal = "1");
			var token1 = bus.AddHandler(actionConsumer1);
			var actionConsumer2 = new ActionConsumer<Message1>(m => ordinal = "2");
			var token2 = bus.AddHandler(actionConsumer2);
			bus.Handle(new Message1());
			bus.RemoveHandler(actionConsumer1, token1);
			bus.Send(new Message1());
			Assert.AreEqual("2", ordinal);
		}

		[Test]
		public void RemoveHandlerWithNullTokenThrows()
		{
			var bus = new Bus();
			Assert.Throws<ArgumentNullException>(() => bus.RemoveHandler(new ActionConsumer<Message1>(m => { }), null));
		}

		[Test]
		public void RemoveHandlerWithTokenThatIsNotTokenTypeThrows()
		{
			var bus = new Bus();
			Assert.Throws<InvalidOperationException>(() => bus.RemoveHandler(new ActionConsumer<Message1>(m => { }), new object()));
		}

		[Test]
		public void RemoveHandlerTwiceSucceeds()
		{
			var bus = new Bus();
			var actionConsumer1 = new ActionConsumer<Message1>(m => { });
			var token1 = bus.AddHandler(actionConsumer1);
			bus.Send(new Message1());
			bus.RemoveHandler(actionConsumer1, token1);
			bus.RemoveHandler(actionConsumer1, token1);
		}

		public abstract class Message : IMessage
		{
			protected Message(string c)
			{
				CorrelationId = c;
			}

			public string CorrelationId { get; set; }
			public abstract Task<IEvent> RequestAsync(IBus bus);
		}

		public abstract class MessageBase<TMessage, TEvent> : Message
			where TEvent : IEvent where TMessage : IMessage
		{
			protected MessageBase() : base(Guid.NewGuid().ToString("D"))
			{
			}

			public override async Task<IEvent> RequestAsync(IBus bus)
			{
				IEvent result = await bus.RequestAsync<MessageBase<TMessage, TEvent>, TEvent>(this);
				return result;
			}
		}

		public abstract class EventBase : IEvent
		{
			protected EventBase(string correllationId)
			{
				CorrelationId = correllationId;
				OccurredDateTime = DateTime.UtcNow;
			}

			public string CorrelationId { get; set; }
			public DateTime OccurredDateTime { get; set; }
		}
		public class Command1 : MessageBase<Command1, Event1> { }
		public class Command2 : MessageBase<Command2, Event2> { }
		public class Command3 : MessageBase<Command3, Event3> { }
		public class Command4 : MessageBase<Command4, Event4> { }
		public class Command5 : MessageBase<Command5, Event5> { }
		public class Command6 : MessageBase<Command6, Event6> { }
		public class Command7 : MessageBase<Command7, Event7> { }
		public class Command8 : MessageBase<Command8, Event8> { }
		public class Event1 : EventBase { public Event1(string c) : base(c) { } }
		public class Event2 : EventBase { public Event2(string c) : base(c) { } }
		public class Event3 : EventBase { public Event3(string c) : base(c) { } }
		public class Event4 : EventBase { public Event4(string c) : base(c) { } }
		public class Event5 : EventBase { public Event5(string c) : base(c) { } }
		public class Event6 : EventBase { public Event6(string c) : base(c) { } }
		public class Event7 : EventBase { public Event7(string c) : base(c) { } }
		public class Event8 : EventBase { public Event8(string c) : base(c) { } }

		public class TracingBus : Bus
		{
			public override void Handle(IMessage message)
			{
				var @event = message as IEvent;
				if (@event != null)
				{
					LogEvent(@event);
					//Console.WriteLine($"{@event.GetType().Name} occured at {@event.OccurredDateTime.ToLocalTime()}");
				}
				bool wasProcessed;
				var stopwatch = Stopwatch.StartNew();
				var processingStartTime = DateTime.Now;
				try
				{
					Handle(message, out wasProcessed);
					var r = new Task<int>(() => 1);
				}
				catch (Exception ex)
				{
					LogException(ex);
					throw;
				}
				if (@event == null)
				{
					var timeSpan = stopwatch.Elapsed;
					LogOperationDuration(message.GetType(), processingStartTime, timeSpan, message.CorrelationId, wasProcessed);
				}
				else if (!wasProcessed)
				{
					throw new InvalidOperationException($"{@message.CorrelationId} of type {message.GetType().Name} was not processed.");
				}
			}

			public List<string> Log = new List<string>();
			private void LogOperationDuration(Type getType, DateTime processingStartTime, TimeSpan timeSpan, string messageCorrelationId, bool wasMessageProcessed)
			{
				lock (Log)
				{
					Log.Add($"{getType} {processingStartTime} {timeSpan} {messageCorrelationId} {wasMessageProcessed}");
				}
			}

			private void LogException(Exception exception)
			{
				lock (Log)
				{
					Log.Add(exception.Message);
				}
			}

			private void LogEvent(IEvent @event)
			{
				lock (Log)
				{
					Log.Add($"Event {@event.GetType().Name} occured {@event.CorrelationId}");
				}
			}
		}

		[Test, Explicit]
		public void D()
		{
			var t = typeof(Event1);
			var bus = new TracingBus();
			var rng = new Random();
			bus.AddHandler(new ActionConsumer<Command1>(m => { Thread.Sleep(rng.Next(0, 100)); bus.Send(new Event1(m.CorrelationId)); }));
			bus.AddHandler(new ActionConsumer<Command2>(m => { Thread.Sleep(rng.Next(0, 100)); bus.Send(new Event2(m.CorrelationId)); }));
			bus.AddHandler(new ActionConsumer<Command3>(m => { Thread.Sleep(rng.Next(0, 100)); bus.Send(new Event3(m.CorrelationId)); }));
			bus.AddHandler(new ActionConsumer<Command4>(m => { Thread.Sleep(rng.Next(0, 100)); bus.Send(new Event4(m.CorrelationId)); }));
			bus.AddHandler(new ActionConsumer<Command5>(m => { Thread.Sleep(rng.Next(0, 100)); bus.Send(new Event5(m.CorrelationId)); }));
			bus.AddHandler(new ActionConsumer<Command6>(m => { Thread.Sleep(rng.Next(0, 100)); bus.Send(new Event6(m.CorrelationId)); }));
			bus.AddHandler(new ActionConsumer<Command7>(m => { Thread.Sleep(rng.Next(0, 100)); bus.Send(new Event7(m.CorrelationId)); }));
			bus.AddHandler(new ActionConsumer<Command8>(m => { Thread.Sleep(rng.Next(0, 100)); bus.Send(new Event8(m.CorrelationId)); }));

			var commands = new List<Message>
#if false
				{
				new Command1(),
				new Command2(),
				new Command3(),
				new Command4(),
				new Command5(),
				new Command6(),
				new Command7(),
				new Command8(),
				new Command1(),
				new Command2(),
				new Command3(),
				new Command4(),
				new Command5(),
				new Command6(),
				new Command7(),
				new Command8(),
			};
#else
			{
				new Command1(),
				new Command1(),
				new Command2(),
				new Command2(),
				new Command3(),
				new Command3(),
				new Command4(),
				new Command4(),
				new Command5(),
				new Command5(),
				new Command6(),
				new Command6(),
				new Command7(),
				new Command7(),
				new Command8(),
				new Command8(),
			};
#endif
			var stopwatch = Stopwatch.StartNew();
			while (stopwatch.Elapsed.TotalMinutes < 2)
				Parallel.ForEach(commands, new ParallelOptions {MaxDegreeOfParallelism = commands.Count}, async command =>
				{
					command.CorrelationId = Guid.NewGuid().ToString("D");
					var result = await command.RequestAsync(bus);
				});

			foreach (var log in bus.Log) Trace.WriteLine(log);
			Trace.WriteLine("done");
		}
	}
}