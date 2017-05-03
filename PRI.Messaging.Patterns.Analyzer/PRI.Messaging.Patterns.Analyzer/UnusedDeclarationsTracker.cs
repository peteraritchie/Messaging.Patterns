using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PRI.Messaging.Patterns.Analyzer
{
	public class UnusedDeclarationsTracker
	{
		private readonly ConcurrentDictionary<ISymbol, bool> _used =
			new ConcurrentDictionary<ISymbol, bool>(concurrencyLevel: 2, capacity: 100);

		//private readonly UnusedDeclarationsAnalyzer<TLanguageKindEnum> _owner;

		//public UnusedDeclarationsTracker(UnusedDeclarationsAnalyzer<TLanguageKindEnum> owner)
		//{
		//	_owner = owner;
		//}

		public void OnIdentifier(SyntaxNodeAnalysisContext context)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			var info = context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken);
			if (info.Symbol?.Kind == SymbolKind.Namespace)
			{
				// Avoid getting Locations for namespaces. That can be very expensive.
				return;
			}

			var hasLocations = info.Symbol?.OriginalDefinition?.Locations.Length > 0;
			if (!hasLocations)
			{
				return;
			}

			var inSource = info.Symbol?.OriginalDefinition?.Locations[0].IsInSource == true;
			if (!inSource || AccessibleFromOutside(info.Symbol.OriginalDefinition))
			{
				return;
			}

			_used.AddOrUpdate(info.Symbol.OriginalDefinition, true, (k, v) => true);
		}

		private IEnumerable<SyntaxNode> GetLocalDeclarationNodes(SyntaxNode node)
		{
			var locals = node as LocalDeclarationStatementSyntax;

			var variables = locals?.Declaration?.Variables;
			if (variables == null)
			{
				yield break;
			}

			foreach (var variable in variables)
			{
				yield return variable;
			}
		}

		public void OnLocalDeclaration(SyntaxNodeAnalysisContext context)
		{
			foreach (var node in GetLocalDeclarationNodes(context.Node))
			{
				var local = context.SemanticModel.GetDeclaredSymbol(node, context.CancellationToken);
				if (local == null)
				{
					continue;
				}

				_used.TryAdd(local, false);
			}
		}

		public void OnSymbol(SymbolAnalysisContext context)
		{
			context.CancellationToken.ThrowIfCancellationRequested();
			var symbol = context.Symbol;

			if (!AccessibleFromOutside(symbol))
			{
				_used.TryAdd(symbol, false);
			}

			var type = symbol as INamedTypeSymbol;
			if (type != null)
			{
				AddSymbolDeclarations(type.TypeParameters);
			}

			var method = symbol as IMethodSymbol;
			if (method != null)
			{
				AddParameters(method.DeclaredAccessibility, method.TypeParameters);
				AddParameters(method.DeclaredAccessibility, method.Parameters);
			}

			var property = symbol as IPropertySymbol;
			if (property != null)
			{
				AddParameters(property.DeclaredAccessibility, property.Parameters);

				if (!AccessibleFromOutside(property.GetMethod))
				{
					_used.TryAdd(property.GetMethod, false);
				}

				if (!AccessibleFromOutside(property.SetMethod))
				{
					_used.TryAdd(property.SetMethod, false);

					AddParameters(property.SetMethod.DeclaredAccessibility, property.SetMethod.Parameters);
				}
			}
		}

		private void AddParameters(Accessibility accessibility, IEnumerable<ISymbol> parameters)
		{
			// only add parameters if accessibility is explicitly set to private.
			if (accessibility != Accessibility.Private)
			{
				return;
			}

			AddSymbolDeclarations(parameters);
		}

		private void AddSymbolDeclarations(IEnumerable<ISymbol> symbols)
		{
			if (symbols == null)
			{
				return;
			}

			foreach (var symbol in symbols)
			{
				_used.TryAdd(symbol, false);
			}
		}

		public void OnCompilationEnd(CompilationAnalysisContext context)
		{
			foreach (var kv in _used.Where(kv => !kv.Value && (kv.Key.Locations.FirstOrDefault()?.IsInSource == true)))
			{
				context.CancellationToken.ThrowIfCancellationRequested();
				var symbol = kv.Key;

				// report visible error only if symbol is not local symbol
				if (!(symbol is ILocalSymbol))
				{
					// TODO: context.ReportDiagnostic(Diagnostic.Create(s_rule, symbol.Locations[0], symbol.Locations.Skip(1), symbol.Name));
				}

				// where code fix works
				foreach (var reference in symbol.DeclaringSyntaxReferences)
				{
					context.CancellationToken.ThrowIfCancellationRequested();

					// TODO: context.ReportDiagnostic(Diagnostic.Create(s_triggerRule, Location.Create(reference.SyntaxTree, reference.Span)));
				}
			}
		}

		private static bool AccessibleFromOutside(ISymbol symbol)
		{
			if (symbol == null ||
			    symbol.Kind == SymbolKind.Namespace)
			{
				return true;
			}

			if (symbol.DeclaredAccessibility == Accessibility.Private ||
			    symbol.DeclaredAccessibility == Accessibility.NotApplicable)
			{
				return false;
			}

			if (symbol.ContainingSymbol == null)
			{
				return true;
			}

			return AccessibleFromOutside(symbol.ContainingSymbol.OriginalDefinition);
		}
	}
}