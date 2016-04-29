using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PRI.Messaging.Patterns;
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
				await bus.RequestAsync<Message1, TheEvent>(
					new Message1 { CorrelationId = "12344321" });
			}
				);
			Assert.That(exception.ParamName, Is.EqualTo("bus"));
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
	}
}