using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

namespace PRI.Messaging.Patterns.Analyzer
{
	internal static class CodeAnalysisExtensions
	{
		public static CatchClauseSyntax GetFirstCatchClauseByType(this TryStatementSyntax parentTryStatement, SemanticModel semanticModel, Type exceptionType, CancellationToken cancellationToken = default(CancellationToken))
		{
			foreach (var e in parentTryStatement.Catches)
			{
				var errorType = semanticModel.GetTypeInfo(e.Declaration.Type, cancellationToken).Type as INamedTypeSymbol;
				if (errorType == null)
				{
					continue;
				}
				var fullName = errorType.ToDisplayString(SymbolDisplayFormat);
				if (exceptionType.FullName.Contains(fullName))
				{
					return e;
				}
			}
			return null;
		}

		private static readonly SymbolDisplayFormat SymbolDisplayFormat = new SymbolDisplayFormat(
			typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

		public static string ToDisplayString(this TypeSyntax typeSyntax, SemanticModel model, SymbolDisplayFormat symbolDisplayFormat, CancellationToken cancellationToken = default(CancellationToken))
		{
			var namedTypeSymbol = (INamedTypeSymbol) model.GetTypeInfo(typeSyntax, cancellationToken).Type;
			return namedTypeSymbol.ToDisplayString(symbolDisplayFormat);
		}

		public static string ToDisplayString(this TypeSyntax typeSyntax, SemanticModel model, CancellationToken cancellationToken = default(CancellationToken))
		{
			return typeSyntax.ToDisplayString(model, SymbolDisplayFormat, cancellationToken);
		}

		/// <summary>
		/// Generates the type syntax.
		/// </summary>
		public static TypeSyntax AsTypeSyntax(this Type type, SyntaxGenerator generator, params ITypeSymbol[] typeParams)
		{
			var typeInfo = type.GetTypeInfo();
			if (!typeInfo.IsGenericType) throw new ArgumentException(nameof(type));

			string name = type.Name.Replace('+', '.');

			if (type.IsConstructedGenericType)
			{
				name = type.GetGenericTypeDefinition().Name.Replace('+', '.');
			}
			// Get the C# representation of the generic type minus its type arguments.
			name = name.Substring(0, name.IndexOf("`", StringComparison.Ordinal));

			// Generate the name of the generic type.
			return SyntaxFactory.GenericName(SyntaxFactory.Identifier(name)).WithTypeArgumentList(
				SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(typeParams.Select(generator.TypeExpression).Cast<TypeSyntax>())));
		}

		public static SyntaxNode GetAncestorStatement(this SyntaxToken token)
		{
			var node = token.Parent;
			while (node != null && !(node is StatementSyntax))
			{
				node = node.Parent;
			}
			return node;
		}

		public static T ReplaceNodes<T>(this T root, IReadOnlyList<SyntaxNode> oldNodes, SyntaxNode newNode)
			where T : SyntaxNode
		{
			if (oldNodes == null)
			{
				throw new ArgumentNullException(nameof(oldNodes));
			}
			if (newNode == null)
			{
				throw new ArgumentNullException(nameof(newNode));
			}
			if (oldNodes.Count == 0)
			{
				throw new ArgumentException(nameof(oldNodes));
			}

			var newRoot = root.TrackNodes(oldNodes);

			var first = newRoot.GetCurrentNode(oldNodes[0]);

			newRoot = newRoot.ReplaceNode(first, newNode);

			var toRemove = oldNodes.Skip(1).Select(newRoot.GetCurrentNode);

			newRoot = newRoot.RemoveNodes(toRemove, SyntaxRemoveOptions.KeepNoTrivia);

			return newRoot as T;
		}

		public static TextSpan GetSpanOfAssignmentDependenciesInSpan(this MemberDeclarationSyntax containingMethod,
			TextSpan textSpan, SemanticModel model, CancellationToken cancellationToken = default(CancellationToken))
		{
			var resultSpan = textSpan;
			List<SyntaxNode> dependentAssignments = new List<SyntaxNode>();
			var tokenKeyComparer = Comparer<int>.Default;
			do
			{
				SyntaxNode[] assignments = containingMethod.DescendantNodes(resultSpan)
					.Where(e => (e is AssignmentExpressionSyntax || e is EqualsValueClauseSyntax) && !dependentAssignments.Contains(e)).ToArray();
				if (!assignments.Any()) // no newly found assignments, done
				{
					break;
				}
				dependentAssignments.AddRange(assignments);
				IEnumerable<ISymbol> symbolsAssigned = dependentAssignments.Select(e => GetAssignmentSymbol(e, model, cancellationToken));
				SortedSet<SyntaxToken> references = new SortedSet<SyntaxToken>(Comparer<SyntaxToken>.Create((token, syntaxToken) => tokenKeyComparer.Compare(
					token.SpanStart, syntaxToken.SpanStart)));

				foreach (ISymbol symbol in symbolsAssigned)
				{
					var symbolReferences = GetSymbolReferences(containingMethod, symbol)
						.Where(e => e.SpanStart >= textSpan.Start);
					references.UnionWith(symbolReferences);
				}
				resultSpan = GetBoundingSpan(references);
			} while (true);
			return resultSpan;
		}

		public static TextSpan GetBoundingSpan(this IEnumerable<SyntaxToken> symbolReferences)
		{
			var locations = symbolReferences
				.Select(token => token.GetAncestorStatement().GetLocation()) // ancestor statement or entire line?
				.OrderBy(e => e.SourceSpan.Start);
			return GetBoundingSpan(locations);
		}

		public static TextSpan GetBoundingSpan(this IOrderedEnumerable<Location> locations)
		{
			return new TextSpan(locations.First().SourceSpan.Start, locations.Last().SourceSpan.End - locations.First().SourceSpan.Start + 1);
		}

		public static IEnumerable<SyntaxToken> GetSymbolReferences(this MemberDeclarationSyntax containingMethod, ISymbol symbol)
		{
			return containingMethod
				.DescendantTokens()
				.Where(e => e.Kind() == SyntaxKind.IdentifierToken && e.ValueText == symbol.Name);
		}

		public static MemberDeclarationSyntax GetContainingMemberDeclaration(this SyntaxNode invocationParent)
		{
			var parentMethod = invocationParent;
			while (parentMethod != null && !(parentMethod is MemberDeclarationSyntax))
			{
				parentMethod = parentMethod.Parent;
			}

			var containingMethod = parentMethod as MemberDeclarationSyntax;
			return containingMethod;
		}

		public static ISymbol GetAssignmentSymbol(this SyntaxNode parent, SemanticModel model, CancellationToken cancellationToken = default(CancellationToken))
		{
			var simpleAssignment = parent as AssignmentExpressionSyntax;

			ISymbol symbol = null;

			if (simpleAssignment != null)
			{
				ExpressionSyntax variable = simpleAssignment.Left;
				symbol = model.GetSymbolInfo(variable, cancellationToken).Symbol;
			}
			else
			{
				var equals = parent as EqualsValueClauseSyntax;
				var variableDeclarator = @equals?.Parent as VariableDeclaratorSyntax;
				if (variableDeclarator != null)
				{
					symbol = model.GetDeclaredSymbol(variableDeclarator, cancellationToken);
				}
			}
			return symbol;
		}

		public static SyntaxNode TryCatchSafe(this SyntaxNode subjectStatement, Dictionary<string, TypeSyntax> exceptionArgumentsInfo,
			Func<IdentifierNameSyntax, SyntaxList<StatementSyntax>> generateCatchStatement, SyntaxNode root, SemanticModel model, CancellationToken cancellationToken = default(CancellationToken))
		{
			return TryCatchFinallySafe(subjectStatement,
				exceptionArgumentsInfo,
				generateCatchStatement,
				root,
				model,
				default(FinallyClauseSyntax), cancellationToken);
		}

		public static SyntaxNode TryCatchFinallySafe(this SyntaxNode subjectStatement, Dictionary<string, TypeSyntax> exceptionArgumentsInfo,
			Func<IdentifierNameSyntax, SyntaxList<StatementSyntax>> generateCatchStatement, SyntaxNode root, SemanticModel model, FinallyClauseSyntax finallyClauseSyntax,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			var containingMethod = subjectStatement.GetContainingMemberDeclaration();

			var tryTextSpan = containingMethod.GetSpanOfAssignmentDependenciesInSpan(
				subjectStatement.FullSpan, model, cancellationToken);

			var tryBlockStatements = containingMethod.SyntaxTree
				.GetRoot(cancellationToken).DescendantNodes()
				.Where(x => tryTextSpan.Contains(x.Span))
				.OfType<StatementSyntax>().ToArray();

			var catchClauses = exceptionArgumentsInfo.Select(i =>
			{
				var exceptionArgumentIdentifierName = SyntaxFactory.IdentifierName(i.Key);
				return SyntaxFactory.CatchClause()
					.WithDeclaration(
						SyntaxFactory.CatchDeclaration(i.Value)
							.WithIdentifier(exceptionArgumentIdentifierName.Identifier))
					.WithBlock(SyntaxFactory.Block(generateCatchStatement(exceptionArgumentIdentifierName))
					);
			});

			var tryStatement = SyntaxFactory.TryStatement(
				SyntaxFactory.Block(SyntaxFactory.List(tryBlockStatements)),
				SyntaxFactory.List(catchClauses),
				finallyClauseSyntax);

			var newMethod = containingMethod.ReplaceNodes(tryBlockStatements.ToImmutableList(), tryStatement);

			var compilationUnitSyntax = (CompilationUnitSyntax) root.ReplaceNode(containingMethod, newMethod);

			var usings = compilationUnitSyntax.Usings;
			if (!usings.Any(e => e.Name.ToString().Equals(typeof(Task).Namespace)))
			{
				var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(typeof(Task).Namespace));
				compilationUnitSyntax = compilationUnitSyntax.AddUsings(usingDirective);
			}
			if (!usings.Any(e => e.Name.ToString().Equals(typeof(Debug).Namespace)))
			{
				var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(typeof(Debug).Namespace));
				compilationUnitSyntax = compilationUnitSyntax.AddUsings(usingDirective);
			}
			return compilationUnitSyntax;
		}

		public static bool IsRequestAsync(IMethodSymbol methodSymbolInfo)
		{
			if (methodSymbolInfo.Arity == 0 || !methodSymbolInfo.IsGenericMethod) return false;
			var matchingMethod = Helpers.GetRequestAsyncInvocationMethodInfo(methodSymbolInfo);
			if (matchingMethod != null)
			{
				return true;
			}
			return false;
		}
	}
}