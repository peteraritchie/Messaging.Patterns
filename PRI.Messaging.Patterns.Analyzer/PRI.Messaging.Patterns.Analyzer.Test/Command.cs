using System;
using System.Diagnostics;
using System.Threading.Tasks;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;

namespace Test
{
	public class Command : IMessage
	{
		public string CorrelationId { get; set; }
	}

	public class CommandCompletedEvent<TMessage> : IEvent where TMessage : IMessage
	{
		public TMessage Message { get; set; }
		public string CorrelationId { get; set; }
		public DateTime OccurredDateTime { get; set; }
	}

	public class CommandCompletedEventHandler<TMessage> : IConsumer<CommandCompletedEvent<TMessage>> where TMessage : IMessage
	{
		public void Handle(CommandCompletedEvent<TMessage> commandCompletedEvent)
		{
			DateTime occurredDateTime;
			occurredDateTime = commandCompletedEvent.OccurredDateTime;
			Debug.WriteLine(commandCompletedEvent.CorrelationId);
			Debug.WriteLine(occurredDateTime);
		}
	}

	public class ErrorEvent<TException> : IEvent where TException : Exception
	{
		public TException Exception { get; set; }
		public string CorrelationId { get; set; }
		public DateTime OccurredDateTime { get; set; }
	}

	// get name of event
	// get constraints, if event generic and has constraints
	// create class with name of event with "Handler" prepended to the name, or inserted between name and type parameters
	// implement Handle method
	// copy catch(ReceivedErrorEventException... body to Handle
	// replace reference to ReceivedErrorEventException.ErrorEvent with argument identifier
	public class ErrorEventHandler<TException> : IConsumer<ErrorEvent<TException>> where TException : Exception
	{
		public void Handle(ErrorEvent<TException> errorEvent)
		{
			global::System.Diagnostics.Debug.WriteLine(errorEvent);
		}
	}

	public class Fake
	{
		public void NoCatchDiagnosis() // consider async
		{
			var bus = new Bus();
			var completedEvent =
				bus.RequestAsync<Command, CommandCompletedEvent<Command>>(new Command {CorrelationId = Guid.NewGuid().ToString("D")});
		}

		public async Task<bool> RecommendAsyncDiagnosis()
		{
			var bus = new Bus();
			try
			{
				var command = new Command
				{
					CorrelationId = Guid.NewGuid().ToString("D")
				};
				var completedEvent = await bus.RequestAsync<Command, CommandCompletedEvent<Command>, ErrorEvent<Exception>>(command);
				Console.WriteLine(completedEvent.CorrelationId);
			}
			catch (ReceivedErrorEventException<ErrorEvent<Exception>> ex)
			{
				Console.WriteLine("got an error" + ex);
			}
			return true;
		}

		public async Task<bool> RecommendCatchDiagnosis1()
		{
			var bus = new Bus();
			var command = new Command
			{
				CorrelationId = Guid.NewGuid().ToString("D")
			};
			DateTime occurredDateTime;
			var completedEvent = await bus.RequestAsync<Command, CommandCompletedEvent<Command>, ErrorEvent<Exception>>(command);
			occurredDateTime = completedEvent.OccurredDateTime;
			Console.WriteLine(completedEvent.CorrelationId);
			Console.WriteLine(occurredDateTime);

			return true;
		}

		public async Task<bool> RecommendCatchFix1()
		{
			var bus = new Bus();
			var command = new Command
			{
				CorrelationId = Guid.NewGuid().ToString("D")
			};
			DateTime occurredDateTime;
			try
			{
				var completedEvent = await bus.RequestAsync<Command, CommandCompletedEvent<Command>, ErrorEvent<Exception>>(command);
				occurredDateTime = completedEvent.OccurredDateTime;
				Console.WriteLine(completedEvent.CorrelationId);
				Console.WriteLine(occurredDateTime);
			}
			catch (ReceivedErrorEventException<ErrorEvent<Exception>> ex)
			{
				// TODO: do something with ex.ErrorEvent
				global::System.Diagnostics.Trace.WriteLine(ex.ErrorEvent);
			}

			return true;
		}


		public async Task<bool> RecommendCatchDiagnosis2()
		{
			var bus = new Bus();
			var command = new Command
			{
				CorrelationId = Guid.NewGuid().ToString("D")
			};
			DateTime occurredDateTime;
			var completedEvent = await bus.RequestAsync<Command, CommandCompletedEvent<Command>, ErrorEvent<Exception>>(command);
			occurredDateTime = completedEvent.OccurredDateTime;
			Console.WriteLine(completedEvent.CorrelationId);
			Console.WriteLine(occurredDateTime);

			return true;
		}

		public async Task<bool> RecommendCatchFix2()
		{
			var bus = new Bus();
			var command = new Command
			{
				CorrelationId = Guid.NewGuid().ToString("D")
			};
			DateTime dateTime = DateTime.Now;
			try
			{
				var completedEvent = await bus.RequestAsync<Command, CommandCompletedEvent<Command>, ErrorEvent<Exception>>(command);
				Console.WriteLine(completedEvent.CorrelationId);
			}
			catch (ReceivedErrorEventException<ErrorEvent<Exception>> ex)
			{
				// TODO: do something with ex.ErrorEvent
				global::System.Diagnostics.Trace.WriteLine(ex.ErrorEvent);
				throw new NotImplementedException();
			}
			Console.WriteLine(dateTime);

			return true;
		}
	}
}