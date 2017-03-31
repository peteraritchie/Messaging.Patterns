using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using PRI.Messaging.Patterns.Analyzer.Utility;

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

	public static IEnumerable<T> MergeSorted<T>(this SortedSet<T> first, SortedSet<T> second)
	{
		return first.AsOrdered().MergeSorted(second.AsOrdered(), Comparer<T>.Default);
	}

	public static IEnumerable<T> MergeSorted<T>(this SortedSet<T> first, IOrderedEnumerable<T> second)
	{
		return first.AsOrdered().MergeSorted(second, Comparer<T>.Default);
	}

	public static IEnumerable<T> MergeSorted<T>(this IOrderedEnumerable<T> first, SortedSet<T> second)
	{
		return first.MergeSorted(second.AsOrdered(), Comparer<T>.Default);
	}

	public static IEnumerable<T> MergeSorted<T>(this IOrderedEnumerable<T> first, IOrderedEnumerable<T> second)
	{
		return first.MergeSorted(second, Comparer<T>.Default);
	}

	public static IOrderedEnumerable<T> AsOrdered<T>(this SortedSet<T> set)
	{
		return new SortedCollectionOrderedEnumerable<T>(set);
	}

	public static IOrderedEnumerable<T2> AsOrdered<T1,T2>(this SortedSet<T1> set, Func<T1, T2> selector)
	{
		return new SortedCollectionOrderedEnumerable<T2>(set.Select(selector));
	}

	public static IEnumerable<T> MergeSorted<T>(this IOrderedEnumerable<T> first, IOrderedEnumerable<T> second, Comparer<T> comparer)
	{
		using (var firstEnumerator = first.GetEnumerator())
		using (var secondEnumerator = second.GetEnumerator())
		{

			var elementsLeftInFirst = firstEnumerator.MoveNext();
			var elementsLeftInSecond = secondEnumerator.MoveNext();
			while (elementsLeftInFirst || elementsLeftInSecond)
			{
				if (!elementsLeftInFirst)
				{
					do
					{
						yield return secondEnumerator.Current;
					} while (secondEnumerator.MoveNext());
					yield break;
				}

				if (!elementsLeftInSecond)
				{
					do
					{
						yield return firstEnumerator.Current;
					} while (firstEnumerator.MoveNext());
					yield break;
				}

				if (comparer.Compare(firstEnumerator.Current, secondEnumerator.Current) < 0)
				{
					yield return firstEnumerator.Current;
					elementsLeftInFirst = firstEnumerator.MoveNext();
				}
				else
				{
					yield return secondEnumerator.Current;
					elementsLeftInSecond = secondEnumerator.MoveNext();
				}
			}
		}
	}

	public class SortedCollectionOrderedEnumerable<T> : IOrderedEnumerable<T>
	{
		private readonly IEnumerable<T> _wrappedCollection;

		public SortedCollectionOrderedEnumerable(SortedSet<T> collection)
			: this((IEnumerable<T>)collection)
		{
			if (collection == null)
			{
				throw new ArgumentNullException(nameof(collection));
			}
		}

#if NOT_PORTABLE
		public SortedCollectionOrderedEnumerable(SortedList<T> collection)
			: this((IEnumerable<T>)collection)
		{
			if (collection == null)
			{
				throw new ArgumentNullException(nameof(collection));
			}
		}
#endif

		public SortedCollectionOrderedEnumerable(IEnumerable<T> wrappedCollection)
		{
			_wrappedCollection = wrappedCollection;
		}

		public IOrderedEnumerable<T> CreateOrderedEnumerable<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer, bool @descending)
		{
			throw new NotImplementedException();
		}

		public IEnumerator<T> GetEnumerator()
		{
			return _wrappedCollection.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	/// <summary>
	/// Generates the type syntax.
	/// </summary>
	public static TypeSyntax AsTypeSyntax(this Type type)
	{
		string name = type.Name.Replace('+', '.');
		var genericArgs = type.GenericTypeArguments;

		if (genericArgs != null && genericArgs.Any())
		{
			// Get the C# representation of the generic type minus its type arguments.
			name = name.Substring(0, name.IndexOf("`", StringComparison.Ordinal));

			// Generate the name of the generic type.
			return SyntaxFactory.GenericName(SyntaxFactory.Identifier(name),
				SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(genericArgs.Select(AsTypeSyntax)))
			);
		}
		else
			return SyntaxFactory.ParseTypeName(name);
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

	public static VariableDeclaratorSyntax[] GetReferencingVariableDeclarations(this SyntaxToken token, SemanticModel model)
	{
		var newSymbol = model.GetSymbolInfo(token.Parent).Symbol;
		if (newSymbol == null)
		{
			return EmptyCache<VariableDeclaratorSyntax>.EmptyArray;
		}
		var symbolDeclarators = newSymbol.DeclaringSyntaxReferences;
		if (symbolDeclarators.IsEmpty)
		{
			return EmptyCache<VariableDeclaratorSyntax>.EmptyArray;
		}
		var syntaxes = symbolDeclarators.Select(e => e.GetSyntax()).OfType<VariableDeclaratorSyntax>().ToArray();
		return syntaxes;
	}
}