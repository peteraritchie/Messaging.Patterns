using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace PRI.Messaging.Patterns.Analyzer.Test
{
	[TestClass]
	public class UnitTest : CodeFixVerifier
	{
		//No diagnostics expected to show up
		[TestMethod]
		public void BlankCodeHasNoDiagnostics()
		{
			var test = @"";

			VerifyCSharpDiagnostic(test);
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
    public class Command : IEvent
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
			#endregion test-code

			var expected = new DiagnosticResult
			{
				Id = "MP0103",
				Message = "Sending event with RequestAsync.",
				Severity = DiagnosticSeverity.Warning,
				Locations =
					new[]
					{
						new DiagnosticResultLocation("Test0.cs", line: 44, column: 48)
					}
			};
			
			VerifyCSharpDiagnostic(source, expected);
		}

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
		public void Something()
		{
			var bus = new Bus();
			var completedEvent = bus.RequestAsync<Command, CommandCompletedEvent<Command>>(new Command {CorrelationId = Guid.NewGuid().ToString(""D"")});
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
					new[] {
							new DiagnosticResultLocation("Test0.cs", line:32, column:29)
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
		public async Task Something()
		{
			var bus = new Bus();
			var completedEvent = await bus.RequestAsync<Command, CommandCompletedEvent<Command>>(new Command {CorrelationId = Guid.NewGuid().ToString(""D"")});
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
				Console.WriteLine(completedEvent.CorrelationId);
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
					new[] {
							new DiagnosticResultLocation("Test0.cs", line:40, column:30)
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
				var completedEvent = await bus.RequestAsync<Command, CommandCompletedEvent<Command>, ErrorEvent<Exception>>(command);
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
					new[] {
							new DiagnosticResultLocation("Test0.cs", line:39, column:30)
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
					new[] {
							new DiagnosticResultLocation("Test0.cs", line:40, column:35)
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
			VerifyCSharpFix(test, expectedFix, allowNewCompilerDiagnostics:true);
		}

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

			// updated original file
			string sourceFix = "";
			// message handler
			string CommandCompletedEventHandler = "";
			// event handler
			string ErrorEventHandler = "";
			VerifyCSharpDiagnostic(source, expected);

			// TODO: figure out how to get a fix to load the Debug namespace 
			VerifyCSharpFix(source, sourceFix, allowNewCompilerDiagnostics: true);
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