using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace PRI.Messaging.Patterns.Analyzer
{
	/// <source>
	/// https://github.com/dotnet/roslyn/blob/38a37274ea303b976f64f4b2920d9f34113b85f7/src/Diagnostics/FxCop/Core/Shared/Extensions/DiagnosticExtensions.cs
	/// </source>
	public static class DiagnosticExtensions
	{
		#region CreateDiagnostics extensions
		public static IEnumerable<Diagnostic> CreateDiagnostics(
			this IEnumerable<SyntaxNode> nodes,
			DiagnosticDescriptor rule,
			params object[] args)
		{
			return nodes.Select(node => node.CreateDiagnostic(rule, args));
		}

		public static Diagnostic CreateDiagnostic(
			this SyntaxNode node,
			DiagnosticDescriptor rule,
			params object[] args)
		{
			return node.GetLocation().CreateDiagnostic(rule, args);
		}

		public static IEnumerable<Diagnostic> CreateDiagnostics(
			this IEnumerable<SyntaxToken> tokens,
			DiagnosticDescriptor rule,
			params object[] args)
		{
			return tokens.Select(token => token.CreateDiagnostic(rule, args));
		}

		public static Diagnostic CreateDiagnostic(
			this SyntaxToken token,
			DiagnosticDescriptor rule,
			params object[] args)
		{
			return token.GetLocation().CreateDiagnostic(rule, args);
		}

		public static IEnumerable<Diagnostic> CreateDiagnostics(
			this IEnumerable<SyntaxNodeOrToken> nodesOrTokens,
			DiagnosticDescriptor rule,
			params object[] args)
		{
			return nodesOrTokens.Select(nodeOrToken => nodeOrToken.CreateDiagnostic(rule, args));
		}

		public static Diagnostic CreateDiagnostic(
			this SyntaxNodeOrToken nodeOrToken,
			DiagnosticDescriptor rule,
			params object[] args)
		{
			return nodeOrToken.GetLocation().CreateDiagnostic(rule, args);
		}

		public static IEnumerable<Diagnostic> CreateDiagnostics(
			this IEnumerable<ISymbol> symbols,
			DiagnosticDescriptor rule,
			params object[] args)
		{
			return symbols.Select(symbol => symbol.CreateDiagnostic(rule, args));
		}

		public static Diagnostic CreateDiagnostic(
			this ISymbol symbol,
			DiagnosticDescriptor rule,
			params object[] args)
		{
			return symbol.Locations.CreateDiagnostic(rule, args);
		}

		public static IEnumerable<Diagnostic> CreateDiagnostics(
			this IEnumerable<Location> locations,
			DiagnosticDescriptor rule,
			params object[] args)
		{
			return locations.Select(location => location.CreateDiagnostic(rule, args));
		}

		public static Diagnostic CreateDiagnostic(
			this Location location,
			DiagnosticDescriptor rule,
			params object[] args)
		{
			return Diagnostic.Create(rule, !location.IsInSource ? null : location, args);
		}

		public static IEnumerable<Diagnostic> CreateDiagnostics(
			this IEnumerable<IEnumerable<Location>> setOfLocations,
			DiagnosticDescriptor rule,
			params object[] args)
		{
			return setOfLocations.Select(locations => locations.CreateDiagnostic(rule, args));
		}

		public static Diagnostic CreateDiagnostic(
			this IEnumerable<Location> locations,
			DiagnosticDescriptor rule,
			params object[] args)
		{
			var location = locations.First(l => l.IsInSource);
			var additionalLocations = locations.Where(l => l.IsInSource).Skip(1);
			return Diagnostic.Create(rule,
				location: location,
				additionalLocations: additionalLocations,
				messageArgs: args);
		}
		#endregion CreateDiagnostics

		public static bool IsSymbolOf(this IMethodSymbol methodSymbolInfo, MethodInfo mi)
		{
			return GetSymbolName(mi) ==
			       Helpers.GetSignature(methodSymbolInfo) &&
			       mi.DeclaringType.AssemblyQualifiedName ==
			       $"{methodSymbolInfo.ReducedFrom.ContainingType.ToString()}, {methodSymbolInfo.ContainingAssembly.Identity}";
		}

		public static string GetSymbolName(this MethodInfo mi)
		{
			// ¯\_(ツ)_/¯
			var normalizedReturnType = mi.ReturnType == typeof(void) ? mi.ReturnType.Name : mi.ReturnType.ToString();

			//if(mi.GetCustomAttribute<ExtensionAttribute>() == null)
			//{
			return Helpers.SquareToAngleBrackets(mi.ToString()
					.Replace($"{normalizedReturnType} {mi.Name}", $"{Helpers.GetCSharpAliasName(mi.ReturnType)} {mi.DeclaringType}.{mi.Name}"));
			//}
			//return Helpers.SquareToAngleBrackets(mi.ToString()
			//	.Replace(
			//		$"{mi.ReturnType} {mi.Name}[{string.Join(",", mi.GetGenericArguments().Select(p => p.Name))}]({mi.GetParameters()[0].ParameterType.FullName}, ",
			//		$"{mi.ReturnType} {mi.GetParameters()[0].ParameterType.FullName}.{mi.Name}("));
		}
	}
}