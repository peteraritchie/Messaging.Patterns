using PRI.Messaging.Primitives;

namespace PRI.Test
{
	public class ErrorEventHandler : IConsumer<ErrorEvent>
	{
		public void Handle(ErrorEvent errorEvent)
		{
			global::System.Diagnostics.Debug.WriteLine(errorEvent);
		}
	}
}

namespace System.Diagnostics
{
	public static class Debug
	{
		public static void WriteLine(object ob)
		{
			throw new NotImplementedException();
		}
	}
}