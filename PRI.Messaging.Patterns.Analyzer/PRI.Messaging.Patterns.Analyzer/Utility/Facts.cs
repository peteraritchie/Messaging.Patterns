using System.Runtime.CompilerServices;

namespace PRI.Messaging.Patterns.Analyzer.Utility
{
	public static class Facts
	{
		public static string GetCurrentMethodName([CallerMemberName] string memberName = null)
		{
			return memberName;
		}
	}
}