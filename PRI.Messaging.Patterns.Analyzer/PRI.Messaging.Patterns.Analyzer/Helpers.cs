using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using PRI.Messaging.Patterns.Extensions.Bus;

namespace PRI.Messaging.Patterns.Analyzer
{
	internal static class Helpers
	{
		public static MethodInfo GetRequestAsyncInvocationMethodInfo(IMethodSymbol methodSymbolInfo)
		{
			var mis = typeof(BusExtensions).GetRuntimeMethods().Where(e => e.Name == nameof(BusExtensions.RequestAsync));
			var matchingMethod =
				mis.SingleOrDefault(
					mi =>
						SquareToAngleBrackets(mi.ToString()
							.Replace($"{mi.ReturnType} {mi.Name}", $"{mi.ReturnType} {mi.DeclaringType}.{mi.Name}")) ==
						GetSignature(methodSymbolInfo) &&
						mi.DeclaringType.AssemblyQualifiedName ==
						$"{methodSymbolInfo.ReducedFrom.ContainingType.ToString()}, {methodSymbolInfo.ContainingAssembly.Identity}");
			return matchingMethod;
		}

		public static string SquareToAngleBrackets(string text)
		{
			var result = text.Replace('[', '<');
			result = result.Replace("`1", "");
			result = result.Replace("`2", "");
			result = result.Replace("`3", "");
			result = result.Replace(", ", ",");
			return result.Replace(']', '>');
		}

		public static string GetSignature(IMethodSymbol methodSymbol)
		{
			return $"{methodSymbol.ReducedFrom.ReturnType} {methodSymbol.ReducedFrom}".Replace(", ", ",");
		}
	}
}