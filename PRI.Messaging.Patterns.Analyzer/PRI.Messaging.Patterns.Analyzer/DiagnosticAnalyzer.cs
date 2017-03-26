using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FindSymbols;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using TypeInfo = System.Reflection.TypeInfo;

namespace PRI.Messaging.Patterns.Analyzer
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class PRIMessagingPatternsAnalyzer : DiagnosticAnalyzer
	{
		//private static object reference = MetadataReference.CreateFromAssembly(System.Runtime.Loader.AssemblyLoadContext.typeof(IBus).GetTypeInfo().Assembly);
		public const string DiagnosticId = "PRIMessagingPatternsAnalyzer";

		// You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
		// See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
		private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
		private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
		private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
		private const string Category = "Naming";

		private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);
		private static ITypeSymbol _iBusSymbol;
		private static Type _type = typeof(IBus);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(Rule);

		public override void Initialize(AnalysisContext context)
		{
			// TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
			// See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
			context.RegisterSyntaxNodeAction(AnalyzeLocalDeclaration, SyntaxKind.LocalDeclarationStatement);
			context.RegisterSymbolAction(AnalyzeFieldPropertySymbol, SymbolKind.Field, SymbolKind.Property);

			context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
			context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
			context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);

		}

		private void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext obj)
		{
			var local = obj.Node as LocalDeclarationStatementSyntax;
			var type = local.Declaration.Type;
			var symbol = obj.SemanticModel.GetSymbolInfo(type).Symbol;
			if (symbol == null) return;
		}

		private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext analysisContext)
		{
			var methodDeclarationSyntax = analysisContext.Node as MethodDeclarationSyntax;
			foreach (var invocationSyntax in methodDeclarationSyntax.DescendantNodes(_=>true).OfType<InvocationExpressionSyntax>())
			{
				var expression = invocationSyntax.Expression;
				var x = analysisContext.SemanticModel.GetSymbolInfo(invocationSyntax);
				x = analysisContext.SemanticModel.GetSymbolInfo(invocationSyntax.Expression);
				var descendentSyntaxNodes = invocationSyntax.DescendantNodes();
				var descendentMemberAccessExpressionSyntaxNodes = invocationSyntax.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
				//analysisContext.SemanticModel.descendentMemberAccessExpressionSyntaxNodes.ElementAt(0)
				var descendantExpressionNodes = expression.DescendantNodes().ToArray();
				if (descendantExpressionNodes.Length < 2)
				{
					continue;
				}
				var invokedIdentifierName = descendantExpressionNodes.ElementAt(0) as IdentifierNameSyntax;
				if (invokedIdentifierName == null)
				{
					continue;
				}
				//Assembly
				var invokedIdentifierTypeInfo = analysisContext.SemanticModel.GetTypeInfo(invokedIdentifierName).Type;
				if (invokedIdentifierTypeInfo == null)
				{
					continue;
				}
				if (!ImplementsIBus(invokedIdentifierTypeInfo)) continue;

				var methodExpression = descendantExpressionNodes.ElementAt(1);
				var methodSymbolInfo = analysisContext.SemanticModel.GetSymbolInfo(methodExpression).Symbol as IMethodSymbol;
				if (methodSymbolInfo == null)
				{
					return;
				}
				if (IsRequestAsync(methodSymbolInfo))
				{
					// TODO: check to make sure there's an exception block catching ReceivedErrorEventException<TErrorEvent> or higher
					// TODO: offer to break out the continuation into a success event handler the exception block into an error event handler
					// hooking them up where other bus.AddHandlers are called and replace RequestAsync with Send
					// with TODOs to verify storage of state and retrieval of state
				}

				var text = invocationSyntax.ToString();

			}
			//foreach (var x in methodDeclarationSyntax.Body.Statements)
			//{
			//	var text = x.ToString();
			//}
		}

		private bool IsRequestAsync(IMethodSymbol methodSymbolInfo)
		{
			if (methodSymbolInfo.Arity == 0 || !methodSymbolInfo.IsGenericMethod) return false;
			var mis = typeof(BusExtensions).GetRuntimeMethods().Where(e=>e.Name == nameof(BusExtensions.RequestAsync));
			MethodInfo match = null;
			foreach (MethodInfo mi in mis)
			{
				var x =
					SquareToAngleBrackets(mi.ToString()
						.Replace($"{mi.ReturnType} {mi.Name}",
							$"{mi.ReturnType} {mi.DeclaringType}.{mi.Name}"));
				var y = GetSignature(methodSymbolInfo);
				if (x == y &&
					mi.DeclaringType.AssemblyQualifiedName == $"{methodSymbolInfo.ReducedFrom.ContainingType.ToString()}, {methodSymbolInfo.ContainingAssembly.Identity}")
				{
					match = mi;
					break;
				}
			}

			var matchingMethod =
				mis.SingleOrDefault(
					mi =>
						SquareToAngleBrackets(mi.ToString()
							.Replace($"{mi.ReturnType} {mi.Name}", $"{mi.ReturnType} {mi.DeclaringType}.{mi.Name}")) ==
						GetSignature(methodSymbolInfo) &&
						mi.DeclaringType.AssemblyQualifiedName == $"{methodSymbolInfo.ReducedFrom.ContainingType.ToString()}, {methodSymbolInfo.ContainingAssembly.Identity}");
			if (matchingMethod != null)
			{
				return true;
			}
			return false;
		}

		private static string GetSignature(IMethodSymbol methodSymbol)
		{
			return $"{methodSymbol.ReducedFrom.ReturnType} {methodSymbol.ReducedFrom}".Replace(", ", ",");
		}

		private static string SquareToAngleBrackets(string text)
		{
			var result = text.Replace('[', '<');
			result = result.Replace("`1", "");
			result = result.Replace("`2", "");
			result = result.Replace("`3", "");
			result = result.Replace(", ", ",");
			return result.Replace(']', '>');
		}

		private static bool ImplementsIBus(ITypeSymbol invokedIdentifierTypeInfo)
		{
			var type = _type;
			if (_iBusSymbol != null)
			{
				return invokedIdentifierTypeInfo.Interfaces.Contains(_iBusSymbol);
			}
			var iBusCandidateSymbol = invokedIdentifierTypeInfo.Interfaces
				.SingleOrDefault(e => e.ToString() == type.FullName);
			if (iBusCandidateSymbol == null)
			{
				return false;
			}
			if($"{iBusCandidateSymbol}, {iBusCandidateSymbol.ContainingAssembly.Identity}" == _type.AssemblyQualifiedName)
			{
				_iBusSymbol = iBusCandidateSymbol;
				return true;
			}
			return false;
		}

		private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext obj)
		{
			// TODO:
		}

		private void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext obj)
		{
			// TODO: Check initializer?
		}

		private static void AnalyzeFieldPropertySymbol(SymbolAnalysisContext context)
		{
			var fieldSymbol = context.Symbol as IFieldSymbol;
			ITypeSymbol type;
			if (fieldSymbol == null)
			{
				var propertySymbol = context.Symbol as IPropertySymbol;
				if (propertySymbol == null) return;
				type = propertySymbol.Type;
			}
			else
			{
				type = fieldSymbol.Type;
			}

			if (type.AllInterfaces.Any(i => i.Name == _type.Name))
				_iBusSymbol = type;
		}
	}
}
