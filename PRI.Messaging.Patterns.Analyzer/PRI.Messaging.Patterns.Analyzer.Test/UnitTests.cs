using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using PRI.ProductivityExtensions.TemporalExtensions;
using TestHelper;

namespace PRI.Messaging.Patterns.Analyzer.Test
{
	[TestClass]
	public class UnitTest : CodeFixVerifier
	{
		private readonly ReaderWriterLockSlim _readerWriterLockSlim = new ReaderWriterLockSlim();
		private readonly ReaderWriterLock _readerWriterLock = new ReaderWriterLock();
		private readonly object _mutex = new object();
		private IDictionary<int, string> _dictionary;

		[TestMethod]
		public void T()
		{
			typeof(BusExtensions).GetMethods().Where(m=>m.Name == nameof(BusExtensions.RequestAsync)).First().GetSymbolName();
		}

		//No diagnostics expected to show up
		[TestMethod]
		public void BlankCodeHasNoDiagnostics()
		{
			var test = @"";

			VerifyCSharpDiagnostic(test);
		}

		internal class Accessor
		{
			public int Hits { get; set; }
			public int Misses { get; set; }
			public Func<int, string> GetDelegate { get; set; }
			public Action<int, string> AddDelegate { get; set; }
			public int Iterations { get; set; }
			public int MaxRange { get; set; }
			public int Seed { get; set; }

			public void Access()
			{
				var randomGenerator = new Random(Seed);

				for (int i = 0; i < Iterations; i++)
				{
					// give a wide spread so will have some duplicates and some unique
					var target = randomGenerator.Next(1, MaxRange);

					// attempt to grab the item from the cache
					var result = GetDelegate(target);

					// if the item doesn't exist, add it
					if (result == null)
					{
						AddDelegate(target, target.ToString());
						Misses++;
					}
					else
					{
						Hits++;
					}
				}
			}
		}

		[TestMethod, TestCategory("slow")]
		public void TConcurrentDictionary()
		{
			var iterations = 100000;
			var seed = 42;
			var maxRange = 5000000;

			Accessor accessor;
			{
				_dictionary = new Dictionary<int, string>();

				accessor = new Accessor
				{
					GetDelegate = GetLocked,
					AddDelegate = AddLocked,
					Iterations = iterations,
					MaxRange = maxRange,
					Seed = seed
				};
				var stopwatch = Stopwatch.StartNew();
				accessor.Access();
				Console.WriteLine($"{stopwatch.Elapsed.ToEnglishString()} lock");
			}
#if true
			{
				_dictionary = new Dictionary<int, string>();
				accessor = new Accessor
				{
					GetDelegate = GetRWLSlim,
					AddDelegate = AddRWLSlim,
					Iterations = iterations,
					MaxRange = maxRange,
					Seed = seed
				};
				var stopwatch = Stopwatch.StartNew();
				accessor.Access();
				Console.WriteLine($"{stopwatch.Elapsed.ToEnglishString()} ReaderWriterLockSlim");
			}
			{
				_dictionary = new ConcurrentDictionary<int, string>();

				accessor = new Accessor
				{
					GetDelegate = GetConcurrent,
					AddDelegate = AddConcurrent,
					Iterations = iterations,
					MaxRange = maxRange,
					Seed = seed
				};
				var stopwatch = Stopwatch.StartNew();
				accessor.Access();
				Console.WriteLine($"{stopwatch.Elapsed.ToEnglishString()} ConcurrentDictionary");
			}
			{
				_dictionary = new Dictionary<int, string>();
				accessor = new Accessor
				{
					GetDelegate = GetRWL,
					AddDelegate = AddRWL,
					Iterations = iterations,
					MaxRange = maxRange,
					Seed = seed
				};
				var stopwatch = Stopwatch.StartNew();
				accessor.Access();
				Console.WriteLine($"{stopwatch.Elapsed.ToEnglishString()} ReaderWriterLock");
			}
			{
				_dictionary = new ConcurrentDictionary<int, string>();

				accessor = new Accessor
				{
					GetDelegate = GetConcurrent,
					AddDelegate = AddConcurrent,
					Iterations = iterations,
					MaxRange = maxRange,
					Seed = seed
				};
				var stopwatch = Stopwatch.StartNew();
				accessor.Access();
				Console.WriteLine($"{stopwatch.Elapsed.ToEnglishString()} ConcurrentDictionary");
			}
#endif
		}

		private void AddConcurrent(int key, string val)
		{
			_dictionary[key] = val;
		}

		private string GetConcurrent(int key)
		{
			string result;
			_dictionary.TryGetValue(key, out result);
			return result;
		}

		private string GetLocked(int key)
		{
			lock (_mutex)
			{
				string val;
				return _dictionary.TryGetValue(key, out val) ? val : null;
			}
		}

		private void AddLocked(int key, string val)
		{
			lock (_mutex)
			{
				_dictionary[key] = val;
			}
		}

		private string GetRWLSlim(int key)
		{
			string val;
			_readerWriterLockSlim.EnterReadLock();
			if (!_dictionary.TryGetValue(key, out val))
			{
				val = null;
			}
			_readerWriterLockSlim.ExitReadLock();
			return val;
		}

		private void AddRWLSlim(int key, string val)
		{
			_readerWriterLockSlim.EnterWriteLock();
			_dictionary[key] = val;
			_readerWriterLockSlim.ExitWriteLock();
		}

		private string GetRWL(int key)
		{
			string val;
			_readerWriterLock.AcquireReaderLock(1000);
			if (!_dictionary.TryGetValue(key, out val))
			{
				val = null;
			}
			_readerWriterLock.ReleaseReaderLock();
			return val;
		}

		private void AddRWL(int key, string val)
		{
			_readerWriterLock.AcquireWriterLock(1000);
			_dictionary[key] = val;
			_readerWriterLock.ReleaseWriterLock();
		}

		[TestMethod]
		public void IProducerWithCopyCorrelationIdNoDiagnostic()
		{
			#region test-code

			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Test
{
    public class Command : IMessage
    {
        public string CorrelationId { get; set; }
    }
    public class Event : IEvent
    {
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }
    public class CommandHandler : IConsumer<Command>, IProducer<Event>
    {
        private IConsumer<Event> _consumer;
        public void Handle(Command command)
        {
            var consumer = _consumer;
            if(consumer == null) throw new InvalidOperationException();
            Event msg;
            msg = new Event {CorrelationId = command.CorrelationId};
            consumer.Handle(msg); // TODO: Test with conditional 
        }

        public void AttachConsumer(IConsumer<Event> consumer)
        {
            _consumer = consumer;
        }
    }
}";
			#endregion test-code

			VerifyCSharpDiagnostic(source);
		}

		[TestMethod]
		public void IProducerWithoutCopyCorrelationIdDiagnostic()
		{
			#region test-code

			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Test
{
    public class Command : IMessage
    {
        public string CorrelationId { get; set; }
    }
    public class Event : IEvent
    {
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }
    public class CommandHandler : IConsumer<Command>, IProducer<Event>
    {
        private IConsumer<Event> _consumer;
        public void Handle(Command command)
        {
            var consumer = _consumer;
            if(consumer == null) throw new InvalidOperationException();
            Event msg;
            msg = new Event();
            consumer.Handle(msg); // TODO: Test with conditional 
        }

        public void AttachConsumer(IConsumer<Event> consumer)
        {
            _consumer = consumer;
        }
    }
}";
			#endregion test-code
			var expected = new DiagnosticResult
			{
				Id = "MP0116",
				Message = "CommandHandler message handler Handles message but does not propagate correlation id.",
				Severity = DiagnosticSeverity.Warning,
				Locations =
					new[]
					{
						new DiagnosticResultLocation("Test0.cs", line: 23, column: 9)
					}
			};

			VerifyCSharpDiagnostic(source, expected);
		}

		[TestMethod]
		public void HandleMessageDiagnostic()
		{
			#region test-code

			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Test
{
    public class Command : IMessage
    {
        public string CorrelationId { get; set; }
    }
}";

			#endregion test-code

			var expected = new DiagnosticResult
			{
				Id = "MP0114",
				Message = "Test.Command is neither an argument of IConsumer<T> nor a argument to IBus.Send or IBus.Publish.",
				Severity = DiagnosticSeverity.Warning,
				Locations =
					new[]
					{
						new DiagnosticResultLocation("Test0.cs", line: 11, column: 18)
					}
			};

			VerifyCSharpDiagnostic(source, expected);
		}

		[TestMethod]
		public void MessageHandlerWithWrongSuffixDiagnostic()
		{
			#region test-code

			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Test
{
    public class Command : IMessage
    {
        public string CorrelationId { get; set; }
    }
    public class Event : IEvent
    {
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }
    public class CommandHandier : IConsumer<Command>
    {
        public void Handle(Command command)
        {
            new Bus().Publish(new Event());
        }
    }
}";

			#endregion test-code

			var expected = new DiagnosticResult
			{
				Id = "MP0111",
				Message = @"IConsumer<T> type identifier ""CommandHandier"" has incorrect suffix.",
				Severity = DiagnosticSeverity.Warning,
				Locations =
					new[]
					{
						new DiagnosticResultLocation("Test0.cs", line: 20, column: 18)
					}
			};

			VerifyCSharpDiagnostic(source, expected);
		}

		[TestMethod]
		public void MessageHandlerWithWrongStemDiagnostic()
		{
			#region test-code

			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Test
{
    public class Command : IMessage
    {
        public string CorrelationId { get; set; }
    }
    public class CommaHandler : IConsumer<Command>
    {
        public void Handle(Command command)
        {
        }
    }
}";

			#endregion test-code

			var expected = new DiagnosticResult
			{
				Id = "MP0113",
				Message = @"""CommaHandler"" is poorly named, consider naming it ""CommandHandler"".",
				Severity = DiagnosticSeverity.Warning,
				Category = "Naming",
				Locations =
					new[]
					{
						new DiagnosticResultLocation("Test0.cs", line: 15, column: 18)
					}
			};

			VerifyCSharpDiagnostic(source, expected);
		}

		[TestMethod]
		public void MessageHandlerWithCorrectSuffixDiagnostic()
		{
			#region test-code

			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Test
{
    public class Command : IMessage
    {
        public string CorrelationId { get; set; }
    }
    public class Event : IEvent
    {
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }
    public class CommandHandler : IConsumer<Command>
    {
        public void Handle(Command command)
        {
            new Bus().Publish(new Event());
        }
    }
}";

			#endregion test-code

			VerifyCSharpDiagnostic(source);
		}

		[TestMethod]
		public void RequestAsyncSendingEvent()
		{
			#region test-code

			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Test
{
    public class TheEvent : IEvent
    {
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class CommandCompletedEvent<TMessage> : IEvent where TMessage : IMessage
    {
        public TMessage Message { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class ErrorEvent<TException> : IEvent where TException : Exception
    {
        public TException Exception { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class Fake
    {
        public async Task<bool> RecommendAsyncDiagnosis()
        {
            var bus = new Bus();
            var command = new TheEvent
            {
                CorrelationId = Guid.NewGuid().ToString(""D"")
            };

            DateTime occurredDateTime;
            try
            {
                var completedEvent = await bus.RequestAsync<TheEvent, CommandCompletedEvent<TheEvent>, ErrorEvent<Exception>>(command);
                occurredDateTime = completedEvent.OccurredDateTime;
                Debug.WriteLine(completedEvent.CorrelationId);
                Debug.WriteLine(occurredDateTime);
            }
            catch (ReceivedErrorEventException<ErrorEvent<Exception>> ex)
            {
                // TODO: do something with ex.ErrorEvent
                global::System.Diagnostics.Debug.WriteLine(ex.ErrorEvent);
            }

            return true;
        }
    }
}";

			#endregion test-code

			var expected = new DiagnosticResult
			{
				Id = "MP0103",
				Message = "Sending event with RequestAsync.",
				Severity = DiagnosticSeverity.Warning,
				Locations =
					new[]
					{
						new DiagnosticResultLocation("Test0.cs", line: 44, column: 44)
					}
			};

			VerifyCSharpDiagnostic(source, expected);
		}

		[TestMethod]
		public void SendSendingEventDiagnostic()
		{
			#region test-code

			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Test
{
    public class TheEvent : IEvent
    {
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class Fake
    {
        public bool RecommendPublishDiagnosis()
        {
            var bus = new Bus();
            var command = new TheEvent
            {
                CorrelationId = Guid.NewGuid().ToString(""D"")
            };

            bus.Send(command);

            return true;
        }
    }
}";

			#endregion test-code

			var expected = new DiagnosticResult
			{
				Id = "MP0112",
				Message = @"IBus.Send called with and IEvent argument ""command"" instead of IBus.Publish.",
				Severity = DiagnosticSeverity.Error,
				Locations =
					new[]
					{
						new DiagnosticResultLocation("Test0.cs", line: 27, column: 13)
					}
			};

			VerifyCSharpDiagnostic(source, expected);
		}

		[TestMethod]
		public void MessageTypeUsedInHandler()
		{
			#region test-code

			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Test
{
    public class TheEvent : IEvent
    {
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class TheEventHandler : IConsumer<TheEvent>
    {
        public void Handle(TheEvent message)
        {
            throw new NotImplementedException();
        }
    }
}";

			#endregion test-code

			VerifyCSharpDiagnostic(source);
		}

		[TestMethod]
		public void MessageTypeUsedInPublish()
		{
			#region test-code

			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Test
{
    public class TheEvent : IEvent
    {
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }
    public class C
    {
        public void Something()
        {
            IBus bus = new Bus();
            bus.Publish(new TheEvent());
        }
    }
}";

			#endregion test-code

			VerifyCSharpDiagnostic(source);
		}


		[TestMethod]
		public void MessageTypeUsedInSend()
		{
			#region test-code

			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Test
{
    public class TheMessage : IMessage
    {
        public string CorrelationId { get; set; }
    }
    public class C
    {
        public void Something()
        {
            IBus bus = new Bus();
            bus.Send(new TheMessage());
        }
    }
}";

			#endregion test-code

			VerifyCSharpDiagnostic(source);
		}

		[TestMethod]
		public void SendSendingMessage()
		{
			#region test-code

			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;

namespace Test
{
	public class TheMessage : IMessage
	{
		public string CorrelationId { get; set; }
	}

	public class Fake
	{
		public bool RecommendPublishDiagnosis()
		{
			var bus = new Bus();
			var command = new TheMessage
			{
				CorrelationId = Guid.NewGuid().ToString(""D"")
			};

			bus.Send(command);

			return true;
		}
	}
}";

			#endregion test-code

			VerifyCSharpDiagnostic(source);
		}

#if SUPPORT_MP0100
		[TestMethod]
		public void AwaitRecommendationWithMethodNotReturningTask()
		{
		#region test-code

			var test = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;

namespace Test
{
    public class Command : IMessage
    {
        public string CorrelationId { get; set; }
    }

    public class CommandCompletedEvent : IEvent
    {
        public Command Command { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class Fake
    {
        public void Something()
        {
            var bus = new Bus();
            var completedEvent = bus.RequestAsync<Command, CommandCompletedEvent>(new Command {CorrelationId = Guid.NewGuid().ToString(""D"")});
        }
    }
}";

		#endregion test-code

			var expected = new DiagnosticResult
			{
				Id = "MP0100",
				Message = "Call to RequestAsync not awaited.",
				Severity = DiagnosticSeverity.Warning,
				Locations =
					new[]
					{
						new DiagnosticResultLocation("Test0.cs", line: 25, column: 38)
					}
			};

			VerifyCSharpDiagnostic(test, expected);

		#region fix

			var expectedFix = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Threading.Tasks;

namespace Test
{
    public class Command : IMessage
    {
        public string CorrelationId { get; set; }
    }

    public class CommandCompletedEvent : IEvent
    {
        public Command Command { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class Fake
    {
        public async Task Something()
        {
            var bus = new Bus();
            var completedEvent = await bus.RequestAsync<Command, CommandCompletedEvent>(new Command { CorrelationId = Guid.NewGuid().ToString(""D"") });
        }
    }
}";

		#endregion fix

			VerifyCSharpFix(test, expectedFix);
		}

		//Diagnostic and CodeFix both triggered and checked for
		[TestMethod]
		public void AwaitRecommendationWithMethodReturningTask()
		{
		#region test-code

			var test = @"using System;
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

    public class ErrorEvent<TException> : IEvent where TException : Exception
    {
        public TException Exception { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class Fake
    {
        public Task RecommendAsyncDiagnosis()
        {
            var bus = new Bus();
            try
            {
                var command = new Command
                {
                    CorrelationId = Guid.NewGuid().ToString(""D"")
                };
                var completedEvent = bus.RequestAsync<Command, CommandCompletedEvent<Command>, ErrorEvent<Exception>>(command);
                Console.WriteLine(completedEvent.Result.CorrelationId);
            }
            catch (ReceivedErrorEventException<ErrorEvent<Exception>> ex)
            {
                Console.WriteLine(""got an error"" + ex);
            }
        }
    }
}";

		#endregion test-code

			var expected = new DiagnosticResult
			{
				Id = "MP0100",
				Message = "Call to RequestAsync not awaited.",
				Severity = DiagnosticSeverity.Warning,
				Locations =
					new[]
					{
						new DiagnosticResultLocation("Test0.cs", line: 40, column: 42)
					}
			};

			VerifyCSharpDiagnostic(test, expected);

		#region fix

			var expectedFix = @"using System;
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

    public class ErrorEvent<TException> : IEvent where TException : Exception
    {
        public TException Exception { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class Fake
    {
        public async Task RecommendAsyncDiagnosis()
        {
            var bus = new Bus();
            try
            {
                var command = new Command
                {
                    CorrelationId = Guid.NewGuid().ToString(""D"")
                };
                Task<CommandCompletedEvent> completedEvent;
                completedEvent = await bus.RequestAsync<Command, CommandCompletedEvent<Command>, ErrorEvent<Exception>>(command);
                Console.WriteLine(completedEvent.CorrelationId);
            }
            catch (ReceivedErrorEventException<ErrorEvent<Exception>> ex)
            {
                Console.WriteLine(""got an error"" + ex);
            }
        }
    }
}";

		#endregion fix

			VerifyCSharpFix(test, expectedFix);
		}

		[TestMethod]
		public void AwaitRecommendationWithinTryCatchWithMethodReturningNonTask()
		{
		#region test-code

			var test = @"using System;
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

    public class ErrorEvent<TException> : IEvent where TException : Exception
    {
        public TException Exception { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class Fake
    {
        public bool RecommendAsyncDiagnosis()
        {
            var bus = new Bus();
            try
            {
                var command = new Command
                {
                    CorrelationId = Guid.NewGuid().ToString(""D"")
                };
                var completedEvent = bus.RequestAsync<Command, CommandCompletedEvent<Command>, ErrorEvent<Exception>>(command);
            }
            catch (ReceivedErrorEventException<ErrorEvent<Exception>> ex)
            {
                Console.WriteLine(""got an error"" + ex);
            }
            return true;
        }
    }
}";

		#endregion test-code

			var expected = new DiagnosticResult
			{
				Id = "MP0100",
				Message = "Call to RequestAsync not awaited.",
				Severity = DiagnosticSeverity.Warning,
				Locations =
					new[]
					{
						new DiagnosticResultLocation("Test0.cs", line: 39, column: 42)
					}
			};

			VerifyCSharpDiagnostic(test, expected);

		#region fix

			var expectedFix = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Threading.Tasks;

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

    public class ErrorEvent<TException> : IEvent where TException : Exception
    {
        public TException Exception { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class Fake
    {
        public async Task<bool> RecommendAsyncDiagnosis()
        {
            var bus = new Bus();
            try
            {
                var command = new Command
                {
                    CorrelationId = Guid.NewGuid().ToString(""D"")
                };
                var completedEvent = await bus.RequestAsync<Command, CommandCompletedEvent<Command>, ErrorEvent<Exception>>(command);
            }
            catch (ReceivedErrorEventException<ErrorEvent<Exception>> ex)
            {
                Console.WriteLine(""got an error"" + ex);
            }
            return true;
        }
    }
}";

		#endregion // fix

			VerifyCSharpFix(test, expectedFix);
		}
#endif

		[TestMethod]
		public void CatchRecommendationWithAsyncMethodReturningTask()
		{
			#region test-code

			var test = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;

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

	public class ErrorEvent<TException> : IEvent where TException : Exception
	{
		public TException Exception { get; set; }
		public string CorrelationId { get; set; }
		public DateTime OccurredDateTime { get; set; }
	}

	public class Fake
	{
		public async Task<bool> RecommendAsyncDiagnosis()
		{
			var bus = new Bus();
			var command = new Command
			{
				CorrelationId = Guid.NewGuid().ToString(""D"")
			};

			DateTime occurredDateTime;
			var completedEvent = await bus.RequestAsync<Command, CommandCompletedEvent<Command>, ErrorEvent<Exception>>(command);
			occurredDateTime = completedEvent.OccurredDateTime;
			Debug.WriteLine(completedEvent.CorrelationId);
			Debug.WriteLine(occurredDateTime);

			return true;
		}
	}
}";

			#endregion test-code

			var expected = new DiagnosticResult
			{
				Id = "MP0101",
				Message = "Call to RequestAsync with error event but no try/catch.",
				Severity = DiagnosticSeverity.Error,
				Locations =
					new[]
					{
						new DiagnosticResultLocation("Test0.cs", line: 40, column: 35)
					}
			};

			VerifyCSharpDiagnostic(test, expected);

			#region fix

			var expectedFix = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

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

    public class ErrorEvent<TException> : IEvent where TException : Exception
    {
        public TException Exception { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class Fake
    {
        public async Task<bool> RecommendAsyncDiagnosis()
        {
            var bus = new Bus();
            var command = new Command
            {
                CorrelationId = Guid.NewGuid().ToString(""D"")
            };

            DateTime occurredDateTime;
            try
            {
                var completedEvent = await bus.RequestAsync<Command, CommandCompletedEvent<Command>, ErrorEvent<Exception>>(command);
                occurredDateTime = completedEvent.OccurredDateTime;
                Debug.WriteLine(completedEvent.CorrelationId);
                Debug.WriteLine(occurredDateTime);
            }
            catch (ReceivedErrorEventException<ErrorEvent<Exception>> ex)
            {
                // TODO: do something with ex.ErrorEvent
                global::System.Diagnostics.Debug.WriteLine(ex.ErrorEvent);
            }

            return true;
        }
    }
}";

			#endregion // fix

			// TODO: figure out how to get a fix to load the Debug namespace 
			VerifyCSharpFix(test, expectedFix, allowNewCompilerDiagnostics: true);
		}

		[TestMethod]
		public void CommandSuffixRecommendation()
		{
			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;

namespace Test
{
    public class Gobbledygook : IMessage
    {
        public string CorrelationId { get; set; }
    }
    public class Usage
    {
        public void MakeUse()
        {
            var bus = new Bus();
            bus.Publish(new Gobbledygook());
        }
    }
}";
			var expected = new DiagnosticResult
			{
				Id = "MP0110",
				Message = @"Message type identifier ""Gobbledygook"" has incorrect suffix.",
				Severity = DiagnosticSeverity.Warning,
				Locations =
					new[]
					{
						new DiagnosticResultLocation("Test0.cs", line: 8, column: 18)
					}
			};
			VerifyCSharpDiagnostic(source, expected);
		}

		[TestMethod]
		public void EventSuffixRecommendation()
		{
			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;

namespace Test
{
    public class Gobbledygook : IEvent
    {
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }
    public class Usage
    {
        public void MakeUse()
        {
            var bus = new Bus();
            bus.Publish(new Gobbledygook());
        }
    }
}";
			var expected = new DiagnosticResult
			{
				Id = "MP0110",
				Message = @"Message type identifier ""Gobbledygook"" has incorrect suffix.",
				Severity = DiagnosticSeverity.Warning,
				Locations =
					new[]
					{
						new DiagnosticResultLocation("Test0.cs", line: 8, column: 18)
					}
			};
			VerifyCSharpDiagnostic(source, expected);
		}

		[TestMethod]
		public void CommandSuffixNotRecommendation()
		{
			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Primitives;
using PRI.Messaging.Patterns.Extensions.Bus;

namespace Test
{
    public class MyCommand : IMessage
    {
        public string CorrelationId { get; set; }
    }
    public class Usage
    {
        public void MakeUse()
        {
            var bus = new Bus();
            bus.Send(new MyCommand());
        }
    }
}";

			VerifyCSharpDiagnostic(source);
		}

		[TestMethod]
		public void EventSuffixNoRecommendation()
		{
			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Primitives;
using PRI.Messaging.Patterns.Extensions.Bus;

namespace Test
{
    public class MyEvent : IEvent
    {
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }
    public class Usage
    {
        public void MakeUse()
        {
            var bus = new Bus();
            bus.Publish(new MyEvent());
        }
    }
}";

			VerifyCSharpDiagnostic(source);
		}

		[TestMethod]
		public void MessageHandlerRecommendationGenericEvents()
		{
			#region test-code

			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

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

    public class ErrorEvent<TException> : IEvent where TException : Exception
    {
        public TException Exception { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class Fake
    {
        public async Task<bool> RecommendAsyncDiagnosis()
        {
            var bus = new Bus();
            var command = new Command
            {
                CorrelationId = Guid.NewGuid().ToString(""D"")
            };

            DateTime occurredDateTime;
            try
            {
                var completedEvent = await bus.RequestAsync<Command, CommandCompletedEvent<Command>, ErrorEvent<Exception>>(command);
                occurredDateTime = completedEvent.OccurredDateTime;
                Debug.WriteLine(completedEvent.CorrelationId);
                Debug.WriteLine(occurredDateTime);
            }
            catch (ReceivedErrorEventException<ErrorEvent<Exception>> ex)
            {
                global::System.Diagnostics.Debug.WriteLine(ex.ErrorEvent);
            }

            return true;
        }
    }
}";

			#endregion // test-code

			var expected = new DiagnosticResult
			{
				Id = "MP0102",
				Message = "Call to RequestAsync can be replaced with IConsumer<T> implementations.",
				Severity = DiagnosticSeverity.Warning,
				Locations =
					new[]
					{
						new DiagnosticResultLocation("Test0.cs", line: 43, column: 48)
					}
			};

			#region fixed original file

			string sourceFix = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

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

    public class ErrorEvent<TException> : IEvent where TException : Exception
    {
        public TException Exception { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class Fake
    {
        public bool RecommendAsyncDiagnosis()
        {
            var bus = new Bus();
            bus.AddHandler(new CommandCompletedEventHandler());
            bus.AddHandler(new ErrorEventHandler());
            var command = new Command
            {
                CorrelationId = Guid.NewGuid().ToString(""D"")
            };
            // TODO: store information about the message with CorrelationId for loading in handlers
            bus.Send(command);
            return true;
        }
    }
}";

			#endregion fixed original file

			// message handler
			string commandCompletedEventHandler = @"using System;
using PRI.Messaging.Primitives;

namespace Test
{
    public class CommandCompletedEventHandler<TMessage> : IConsumer<CommandCompletedEvent<TMessage>> where TMessage : IMessage
    {
        public void Handle(CommandCompletedEvent<TMessage> completedEvent)
        {
            // TODO: Load message information from a repository by CorrelationId
            DateTime occurredDateTime;
            occurredDateTime = completedEvent.OccurredDateTime;
            Debug.WriteLine((string)(completedEvent.CorrelationId));
            Debug.WriteLine((DateTime)(occurredDateTime));
        }
    }
}";
			// event handler
			string errorEventHandler = @"using System;
using PRI.Messaging.Primitives;

namespace Test
{
    public class ErrorEventHandler<TException> : IConsumer<ErrorEvent<TException>> where TException : Exception
    {
        public void Handle(ErrorEvent<TException> errorEvent)
        {
            // TODO: Load message information from a repository by CorrelationId
            global::System.Diagnostics.Debug.WriteLine((ErrorEvent<Exception>)(errorEvent));
        }
    }
}";
			VerifyCSharpDiagnostic(source, expected);

			// TODO: figure out how to get a fix to load the Debug namespace 
			VerifyCSharpFix(source, sourceFix, true,
				new KeyValuePair<string, string>("CommandCompletedEventHandler.cs", commandCompletedEventHandler),
				new KeyValuePair<string, string>("ErrorEventHandler.cs", errorEventHandler));
		}

		// TODO: unit test with reference that adds usings.
		[TestMethod]
		public void MessageHandlerRecommendation()
		{
			#region test-code

			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PRI { namespace Test
{
    public class Command : IMessage
    {
        public string CorrelationId { get; set; }
    }

    public class CommandCompletedEvent : IEvent
    {
        public Command Command { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class ErrorEvent : IEvent
    {
        public Exception Exception { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class Fake
    {
        public async Task<bool> RecommendHandlerDiagnosis()
        {
            IBus bus = new Bus();
            var command = new Command
            {
                CorrelationId = Guid.NewGuid().ToString(""D"")
            };

            DateTime occurredDateTime;
            try
            {
                var completedEvent = await bus.RequestAsync<Command, CommandCompletedEvent, ErrorEvent>(command);
                occurredDateTime = completedEvent.OccurredDateTime;
                Debug.WriteLine(completedEvent.CorrelationId);
                Debug.WriteLine(occurredDateTime);
            }
            catch (ReceivedErrorEventException<ErrorEvent> ex)
            {
                Debug.WriteLine(ex.ErrorEvent);
            }

            return true;
        }
    }
}}
namespace System.Diagnostics
{
    public static class Debug
    {
        public static void WriteLine<T>(T ob)
        {
            throw new NotImplementedException();
        }
    }
}";

			#endregion // test-code

			var expected = new DiagnosticResult
			{
				Id = "MP0102",
				Message = "Call to RequestAsync can be replaced with IConsumer<T> implementations.",
				Severity = DiagnosticSeverity.Warning,
				Locations =
					new[]
					{
						new DiagnosticResultLocation("Test0.cs", line: 43, column: 48)
					}
			};

			#region fixed original file

			string sourceFix = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PRI { namespace Test
{
    public class Command : IMessage
    {
        public string CorrelationId { get; set; }
    }

    public class CommandCompletedEvent : IEvent
    {
        public Command Command { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class ErrorEvent : IEvent
    {
        public Exception Exception { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class Fake
    {
            public bool RecommendHandlerDiagnosis()
            {
                IBus bus = new Bus();
                bus.AddHandler(new CommandCompletedEventHandler());
                bus.AddHandler(new ErrorEventHandler());
                var command = new Command
                {
                    CorrelationId = Guid.NewGuid().ToString(""D"")
                };
                // TODO: store information about the message with CorrelationId for loading in handlers
                bus.Send(command);
                return true;
            }
        }
}}
namespace System.Diagnostics
{
    public static class Debug
    {
        public static void WriteLine<T>(T ob)
        {
            throw new NotImplementedException();
        }
    }
}";

			#endregion fixed original file

			#region new files

			// message handler
			string commandCompletedEventHandler = @"using System;
using System.Diagnostics;
using PRI.Messaging.Primitives;

namespace PRI.Test
{
    public class CommandCompletedEventHandler : IConsumer<CommandCompletedEvent>
    {
        public void Handle(CommandCompletedEvent completedEvent)
        {
            // TODO: Load message information from a repository by CorrelationId
            DateTime occurredDateTime;
            occurredDateTime = completedEvent.OccurredDateTime;
            Debug.WriteLine<string>((string)(completedEvent.CorrelationId));
            Debug.WriteLine<DateTime>((DateTime)(occurredDateTime));
        }
    }
}";
			// event handler
			string errorEventHandler = @"using System.Diagnostics;
using PRI.Messaging.Primitives;

namespace PRI.Test
{
    public class ErrorEventHandler : IConsumer<ErrorEvent>
    {
        public void Handle(ErrorEvent errorEvent)
        {
            // TODO: Load message information from a repository by CorrelationId
            Debug.WriteLine<ErrorEvent>((ErrorEvent)(errorEvent));
        }
    }
}";

			#endregion new files

			VerifyCSharpDiagnostic(source, expected);

			// TODO: figure out how to get a fix to load the Debug namespace 
			VerifyCSharpFix(source, sourceFix, true,
				new KeyValuePair<string, string>("CommandCompletedEventHandler.cs", commandCompletedEventHandler),
				new KeyValuePair<string, string>("ErrorEventHandler.cs", errorEventHandler));
		}

		[TestMethod]
		public void MessageHandlerRecommendationNonInterfaceBusVariable()
		{
			#region test-code

			var source = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PRI { namespace Test
{
    public class Command : IMessage
    {
        public string CorrelationId { get; set; }
    }

    public class CommandCompletedEvent : IEvent
    {
        public Command Command { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class ErrorEvent : IEvent
    {
        public Exception Exception { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class Fake
    {
        public async Task<bool> RecommendHandlerDiagnosis()
        {
            var bus = new Bus();
            var command = new Command
            {
                CorrelationId = Guid.NewGuid().ToString(""D"")
            };

            DateTime occurredDateTime;
            try
            {
                var completedEvent = await bus.RequestAsync<Command, CommandCompletedEvent, ErrorEvent>(command);
                occurredDateTime = completedEvent.OccurredDateTime;
                Debug.WriteLine(completedEvent.CorrelationId);
                Debug.WriteLine(occurredDateTime);
            }
            catch (ReceivedErrorEventException<ErrorEvent> ex)
            {
                Debug.WriteLine(ex.ErrorEvent);
            }

            return true;
        }
    }
}}
namespace System.Diagnostics
{
    public static class Debug
    {
        public static void WriteLine<T>(T ob)
        {
            throw new NotImplementedException();
        }
    }
}";

			#endregion // test-code

			var expected = new DiagnosticResult
			{
				Id = "MP0102",
				Message = "Call to RequestAsync can be replaced with IConsumer<T> implementations.",
				Severity = DiagnosticSeverity.Warning,
				Locations =
					new[]
					{
						new DiagnosticResultLocation("Test0.cs", line: 43, column: 48)
					}
			};

			#region fixed original file

			string sourceFix = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PRI { namespace Test
{
    public class Command : IMessage
    {
        public string CorrelationId { get; set; }
    }

    public class CommandCompletedEvent : IEvent
    {
        public Command Command { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class ErrorEvent : IEvent
    {
        public Exception Exception { get; set; }
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class Fake
    {
            public bool RecommendHandlerDiagnosis()
            {
                var bus = new Bus();
                bus.AddHandler(new CommandCompletedEventHandler());
                bus.AddHandler(new ErrorEventHandler());
                var command = new Command
                {
                    CorrelationId = Guid.NewGuid().ToString(""D"")
                };
                // TODO: store information about the message with CorrelationId for loading in handlers
                bus.Send(command);
                return true;
            }
        }
}}
namespace System.Diagnostics
{
    public static class Debug
    {
        public static void WriteLine<T>(T ob)
        {
            throw new NotImplementedException();
        }
    }
}";

			#endregion fixed original file

			// message handler
			string commandCompletedEventHandler = @"using System;
using System.Diagnostics;
using PRI.Messaging.Primitives;

namespace PRI.Test
{
    public class CommandCompletedEventHandler : IConsumer<CommandCompletedEvent>
    {
        public void Handle(CommandCompletedEvent completedEvent)
        {
            // TODO: Load message information from a repository by CorrelationId
            DateTime occurredDateTime;
            occurredDateTime = completedEvent.OccurredDateTime;
            Debug.WriteLine<string>((string)(completedEvent.CorrelationId));
            Debug.WriteLine<DateTime>((DateTime)(occurredDateTime));
        }
    }
}";
			// event handler
			string errorEventHandler = @"using System.Diagnostics;
using PRI.Messaging.Primitives;

namespace PRI.Test
{
    public class ErrorEventHandler : IConsumer<ErrorEvent>
    {
        public void Handle(ErrorEvent errorEvent)
        {
            // TODO: Load message information from a repository by CorrelationId
            Debug.WriteLine<ErrorEvent>((ErrorEvent)(errorEvent));
        }
    }
}";
			VerifyCSharpDiagnostic(source, expected);

			// TODO: figure out how to get a fix to load the Debug namespace 
			VerifyCSharpFix(source, sourceFix, true,
				new KeyValuePair<string, string>("CommandCompletedEventHandler.cs", commandCompletedEventHandler),
				new KeyValuePair<string, string>("ErrorEventHandler.cs", errorEventHandler));
		}

		[TestMethod]
		public void CommandHandlerThatDoesNotPublishDiagnostic()
		{
			#region test-code

			var test = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;

namespace Test
{
    public class Command : IMessage
    {
        public string CorrelationId { get; set; }
    }

    public class CommandHandler : IConsumer<Command>
    {
        public void Handle(Command command)
        {
            command = command;
        }
    }
}";

			#endregion test-code

			var expected = new DiagnosticResult
			{
				Id = "MP0115",
				Message =
					"CommandHandler uses the Command Message convention to signify a state change request but does not publish events describing state changes.",
				Severity = DiagnosticSeverity.Warning,
				Locations =
					new[]
					{
						new DiagnosticResultLocation("Test0.cs", line: 15, column: 9)
					}
			};

			VerifyCSharpDiagnostic(test, expected);
		}

		[TestMethod]
		public void CommandHandlerThatPublishes()
		{
			#region test-code

			var test = @"using System;
using PRI.Messaging.Patterns;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;

namespace Test
{
    public class Command : IMessage
    {
        public string CorrelationId { get; set; }
    }

    public class Event : IEvent
    {
        public string CorrelationId { get; set; }
        public DateTime OccurredDateTime { get; set; }
    }

    public class CommandHandler : IConsumer<Command>
    {
        IBus bus;
        public CommandHandler(IBus bus)
        {
            this.bus = bus;
        }

        public void Handle(Command command)
        {
            bus.Publish(new Event
            {
                CorrelationId = command.CorrelationId,
                OccurredDateTime = DateTime.UtcNow
            });
        }
    }
}";

			#endregion test-code

			VerifyCSharpDiagnostic(test);
		}

		protected override CodeFixProvider GetCSharpCodeFixProvider()
		{
			return new PRIMessagingPatternsAnalyzerCodeFixProvider();
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
		{
			return new PRIMessagingPatternsAnalyzer();
		}
	}

}
