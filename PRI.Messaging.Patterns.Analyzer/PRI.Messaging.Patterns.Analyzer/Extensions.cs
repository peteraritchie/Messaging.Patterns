using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

static internal class Extensions
{
	public static CatchClauseSyntax GetFirstCatchClauseByType(this TryStatementSyntax parentTryStatement, SemanticModel semanticModel, Type exceptionType)
	{
		foreach (var e in parentTryStatement.Catches)
		{
			var errorType = semanticModel.GetTypeInfo(e.Declaration.Type).Type as INamedTypeSymbol;
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

	public static string ToDisplayString(this TypeSyntax typeSyntax, SemanticModel model, SymbolDisplayFormat symbolDisplayFormat)
	{
		var namedTypeSymbol = (INamedTypeSymbol) model.GetTypeInfo(typeSyntax).Type;
		return namedTypeSymbol.ToDisplayString(symbolDisplayFormat);
	}

	public static string ToDisplayString(this TypeSyntax typeSyntax, SemanticModel model)
	{
		return typeSyntax.ToDisplayString(model, SymbolDisplayFormat);
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
		TextSpan textSpan, SemanticModel model)
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
			IEnumerable<ISymbol> symbolsAssigned = dependentAssignments.Select<SyntaxNode, ISymbol>(e => GetAssignmentSymbol(e, model));
			SortedSet<SyntaxToken> references = new SortedSet<SyntaxToken>(Comparer<SyntaxToken>
				.Create((token, syntaxToken) => tokenKeyComparer.Compare(
					token.SpanStart, syntaxToken.SpanStart)));
			//references.UnionWith(assignments.GetTokens());
			foreach (ISymbol symbol in symbolsAssigned)
			{
				var symbolReferences = Enumerable.Where<SyntaxToken>(GetSymbolReferences(containingMethod, symbol), e => e.SpanStart >= textSpan.Start);
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

	public static IEnumerable<SyntaxToken> GetSymbolReferences(this MemberDeclarationSyntax containingMethod, ISymbol symbol)
	{
		return containingMethod
			.DescendantTokens()
			.Where(e => e.Kind() == SyntaxKind.IdentifierToken && e.ValueText == symbol.Name);
	}

	public static TextSpan GetBoundingSpan(this IOrderedEnumerable<Location> locations)
	{
		return new TextSpan(locations.First().SourceSpan.Start, locations.Last().SourceSpan.End - locations.First().SourceSpan.Start + 1);
	}

	public static MemberDeclarationSyntax GetContainingMethod(this SyntaxNode invocationParent)
	{
		var parentMethod = invocationParent;
		while (parentMethod != null && !(parentMethod is MemberDeclarationSyntax))
		{
			parentMethod = parentMethod.Parent;
		}

		var containingMethod = parentMethod as MemberDeclarationSyntax;
		return containingMethod;
	}

	public static ISymbol GetAssignmentSymbol(this SyntaxNode parent, SemanticModel model)
	{
		var simpleAssignment = parent as AssignmentExpressionSyntax;

		ISymbol symbol = null;

		if (simpleAssignment != null)
		{
			ExpressionSyntax variable = simpleAssignment.Left;
			symbol = model.GetSymbolInfo(variable).Symbol;
		}
		else
		{
			var equals = parent as EqualsValueClauseSyntax;
			var variableDeclarator = @equals?.Parent as VariableDeclaratorSyntax;
			if (variableDeclarator != null)
			{
				symbol = model.GetDeclaredSymbol(variableDeclarator);
			}
		}
		return symbol;
	}
}