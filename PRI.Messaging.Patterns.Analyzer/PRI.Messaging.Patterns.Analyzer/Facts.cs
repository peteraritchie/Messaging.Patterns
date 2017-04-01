using System.Runtime.CompilerServices;

public static class Facts
{
	public static string GetCurrentMethodName([CallerMemberName] string memberName = null)
	{
		return memberName;
	}
}