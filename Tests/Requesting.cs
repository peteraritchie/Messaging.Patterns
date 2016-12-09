using System;
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
	public class Requesting
	{
		[Test]
		public async Task RequestSucceeds()
		{
			IBus bus = new Bus();
			bus.AddHandler(new ActionConsumer<Message1>(m => bus.Publish(new TheEvent {CorrelationId = m.CorrelationId})));
			var response = await bus.RequestAsync<Message1, TheEvent>(new Message1 {CorrelationId = "12344321"});
			Assert.AreEqual("12344321", response.CorrelationId);
		}

		[Test]
		public async Task RequestWithDifferentCorrelationIdDoesNotSucceed()
		{
			IBus bus = new Bus();
			bus.AddHandler(new ActionConsumer<Message1>(m => bus.Publish(new TheEvent { CorrelationId = "0" })));
			var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
			Assert.ThrowsAsync<TaskCanceledException>(async () =>
			{
				var response = await bus.RequestAsync<Message1, TheEvent>(
					new Message1 {CorrelationId = "12344321"},
					cancellationTokenSource.Token);
			});
		}

		[Test]
		public void RequestWithErrorEventThrows()
		{
			IBus bus = new Bus();
			bus.AddHandler(new ActionConsumer<Message1>(m =>
			{
				bus.Publish(new TheErrorEvent {CorrelationId = m.CorrelationId});
			}));

			Assert.ThrowsAsync<ReceivedErrorEventException<TheErrorEvent>>(async () =>
			{
				await bus.RequestAsync<Message1, TheEvent, TheErrorEvent>(
					new Message1 {CorrelationId = "12344321"});
			}
				);
		}

		[Test]
		public async Task RequestWithErrorEventWithSuccessEventSucceedsWithCorrectEvent()
		{
			IBus bus = new Bus();
			bus.AddHandler(new ActionConsumer<Message1>(m =>
			{
				bus.Publish(new TheEvent {CorrelationId = m.CorrelationId});
			}));

			var @event = await bus.RequestAsync<Message1, TheEvent, TheErrorEvent>(
				new Message1 {CorrelationId = "12344321"});

			Assert.IsInstanceOf<TheEvent>(@event);
		}

		[Test]
		public void RequestWithErrorEventThrowsWithExceptionWithEvent()
		{
			IBus bus = new Bus();
			bus.AddHandler(new ActionConsumer<Message1>(m =>
			{
				bus.Publish(new TheErrorEvent { CorrelationId = m.CorrelationId });
			}));

			var exception = Assert.ThrowsAsync<ReceivedErrorEventException<TheErrorEvent>>(async () =>
			{
				await bus.RequestAsync<Message1, TheEvent, TheErrorEvent>(
					new Message1 { CorrelationId = "12344321" });
			}
				);
			Assert.IsNotNull(exception.ErrorEvent);
		}

		[Test]
		public void RequestWithErrorEventThrowsWithExceptionWithCorrectEvent()
		{
			var correlationId = "12344321";
			IBus bus = new Bus();
			bus.AddHandler(new ActionConsumer<Message1>(m =>
			{
				bus.Publish(new TheErrorEvent { CorrelationId = m.CorrelationId });
			}));

			var exception = Assert.ThrowsAsync<ReceivedErrorEventException<TheErrorEvent>>(async () =>
			{
				await bus.RequestAsync<Message1, TheEvent, TheErrorEvent>(
					new Message1 { CorrelationId = correlationId });
			}
				);
			Assert.AreEqual(correlationId, exception.ErrorEvent.CorrelationId);
		}

		[Test]
		public void RequestWithErrorEventTimesOut()
		{
			IBus bus = new Bus();
			bus.AddHandler(new ActionConsumer<Message1>(m =>
			{
				/* do nothing */
			}));

			var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));
			Assert.ThrowsAsync<TaskCanceledException>(async () =>
			{
				await bus.RequestAsync<Message1, TheEvent, TheErrorEvent>(
					new Message1 {CorrelationId = "12344321"},
					cancellationTokenSource.Token);
			}
				);
		}

		[Test]
		public void RequestTimesOut()
		{
			IBus bus = new Bus();
			bus.AddHandler(new ActionConsumer<Message1>(m =>
			{
				/* do nothing */
			}));

			var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));
			Assert.ThrowsAsync<TaskCanceledException>(async () =>
			{
				await bus.RequestAsync<Message1, TheEvent>(
					new Message1 {CorrelationId = "12344321"},
					cancellationTokenSource.Token);
			}
				);
		}

		[Test]
		public void RequestWithNullBusThrows()
		{
			IBus bus = null;
			var exception = Assert.ThrowsAsync<ArgumentNullException>(async () =>
			{
				// ReSharper disable once ExpressionIsAlwaysNull
				await bus.RequestAsync<Message1, TheEvent>(
					new Message1 { CorrelationId = "12344321" });
			}
				);
			Assert.That(exception.ParamName, Is.EqualTo("bus"));
		}

		[Test]
		public void ErrorEventRequestWithNullBusThrows()
		{
			IBus bus = null;
			var exception = Assert.ThrowsAsync<ArgumentNullException>(async () =>
			{
				// ReSharper disable once ExpressionIsAlwaysNull
				await bus.RequestAsync<Message1, TheEvent, TheErrorEvent>(
					new Message1 { CorrelationId = "12344321" });
			}
				);
			Assert.That(exception.ParamName, Is.EqualTo("bus"));
		}

		[Test]
		public void ErrorEventRequestWithNullMessageThrows()
		{
			IBus bus = new Bus();
			Message1 message1 = null;
			var exception = Assert.ThrowsAsync<ArgumentNullException>(async () =>
			{
				await bus.RequestAsync<Message1, TheEvent, TheErrorEvent>(message1);
			}
				);
			Assert.That(exception.ParamName, Is.EqualTo("message"));
		}

		[Test]
		public void RequestWithNullMessageThrows()
		{
			IBus bus = new Bus();
			Message1 message1 = null;
			var exception = Assert.ThrowsAsync<ArgumentNullException>(async () =>
			{
				await bus.RequestAsync<Message1, TheEvent>(message1);
			}
				);

			Assert.That(exception.ParamName, Is.EqualTo("message"));
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

		public class TheErrorEvent : IEvent
		{
			public TheErrorEvent()
			{
				CorrelationId = Guid.NewGuid().ToString("D");
				OccurredDateTime = DateTime.UtcNow;
			}

			public string CorrelationId { get; set; }
			public DateTime OccurredDateTime { get; set; }
		}
	}
}