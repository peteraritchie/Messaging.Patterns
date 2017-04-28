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
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using PRI.Messaging.Patterns.Analyzer.Utility;

namespace PRI.Messaging.Patterns.Analyzer
{
	public static class CodeAnalysisExtensions
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
			if (!typeInfo.IsGenericType)
			{
				throw new ArgumentException(nameof(type));
			}

			string name = type.Name.Replace('+', '.');

			if (type.IsConstructedGenericType)
			{
				name = type.GetGenericTypeDefinition().Name.Replace('+', '.');
			}
			// Get the C# representation of the generic type minus its type arguments.
			name = name.Substring(0, name.IndexOf("`", StringComparison.Ordinal));

			// Generate the name of the generic type.
			return SyntaxFactory.GenericName(SyntaxFactory.Identifier(name)).WithTypeArgumentList(
				SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(typeParams.Select(generator.TypeExpression)
				.Cast<TypeSyntax>())));
		}

		public static SyntaxNode GetAncestorStatement(this SyntaxNode givenNode)
		{
			if (givenNode == null)
			{
				throw new ArgumentNullException(nameof(givenNode));
			}
			var node = givenNode.Parent;
			while (node != null && !(node is StatementSyntax))
			{
				node = node.Parent;
			}
			return node;
		}

		public static SyntaxNode GetAncestorStatement(this SyntaxToken token)
		{
			if (token.Kind() == SyntaxKind.None)
			{
				throw new ArgumentNullException(nameof(token));
			}
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

			return newRoot;
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

		public static TextSpan GetSpanOfAssignmentDependenciesAndDeclarationsInSpan(this MemberDeclarationSyntax containingMethod,
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
					var symbolReferences = GetSymbolReferences(containingMethod, symbol);
					references.UnionWith(symbolReferences);
				}
				resultSpan = GetBoundingSpan(references);
			} while (true);
			return resultSpan;
		}

		public static string GetSpanText(this Location location)
		{
			if (location == null)
			{
				throw new ArgumentNullException(nameof(location));
			}
			if (location == Location.None || location.SourceTree == null || location.SourceSpan.IsEmpty)
			{
				throw new ArgumentException(nameof(location));
			}
			return location.SourceTree.ToString().Substring(location.SourceSpan.Start, location.SourceSpan.Length);
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
			if(containingMethod == null)
			{
				throw new InvalidOperationException();
			}
			return containingMethod;
		}

		public static ISymbol GetAssignmentSymbol(this SyntaxNode parent, SemanticModel model, CancellationToken cancellationToken = default(CancellationToken))
		{
			while (true)
			{
				var localDeclaration = parent as LocalDeclarationStatementSyntax;
				if (localDeclaration != null)
				{
					parent = localDeclaration.Declaration.Variables.First().Initializer;
					continue;
				}
				var memberAccess = parent as MemberAccessExpressionSyntax;
				if (memberAccess != null)
				{
					parent = parent.Parent.Parent;
					continue;
				}
				var awaitExpression = parent as AwaitExpressionSyntax;
				if (awaitExpression != null)
				{
					parent = parent.Parent;
					continue;
				}
				var simpleAssignment = parent as AssignmentExpressionSyntax;

				ISymbol symbol = null;

				if (simpleAssignment != null)
				{
					ExpressionSyntax variable = simpleAssignment.Left;
					symbol = model.GetSymbolInfo(variable, cancellationToken).Symbol;
				}
				else
				{
					var equalsValueClause = parent as EqualsValueClauseSyntax;
					var variableDeclarator = equalsValueClause?.Parent as VariableDeclaratorSyntax;
					if (variableDeclarator != null)
					{
						symbol = model.GetDeclaredSymbol(variableDeclarator, cancellationToken);
					}
				}
				return symbol;
			}
		}

		public static SyntaxToken GetAssignmentToken(this SyntaxNode parent)
		{
			while (true)
			{
				var localDeclaration = parent as LocalDeclarationStatementSyntax;
				if (localDeclaration != null)
				{
					return localDeclaration.Declaration.Variables.First().Identifier;
				}
				var memberAccess = parent as MemberAccessExpressionSyntax;
				if (memberAccess != null)
				{
					parent = parent.Parent.Parent;
					continue;
				}
				var awaitExpression = parent as AwaitExpressionSyntax;
				if (awaitExpression != null)
				{
					parent = parent.Parent;
					continue;
				}
				var simpleAssignment = parent as AssignmentExpressionSyntax;

				if (simpleAssignment != null)
				{
					// TODO: support AssignmentExpressionSyntax in GetAssignmentToken
					var xxx = simpleAssignment.Left;
					throw new NotImplementedException();
				}
				else
				{
					var equalsValueClause = parent as EqualsValueClauseSyntax;
					var variableDeclarator = equalsValueClause?.Parent as VariableDeclaratorSyntax;
					return variableDeclarator?.Identifier ?? default(SyntaxToken);
				}
			}
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
					finallyClauseSyntax)
				.WithAdditionalAnnotations(Formatter.Annotation);

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

		public static TypeSyntax ToTypeSyntax(this INamedTypeSymbol namedTypeSymbol, SyntaxGenerator generator)
		{
			if(namedTypeSymbol.IsGenericType)
			{
				return SyntaxFactory.GenericName(namedTypeSymbol.Name)
					.WithTypeArgumentList(
						SyntaxFactory.TypeArgumentList(
							SyntaxFactory.SeparatedList(
								namedTypeSymbol.ConstructedFrom.TypeArguments
									.Select(e => (TypeSyntax) generator.TypeExpression(e)))));
			}
			return SyntaxFactory.ParseTypeName(namedTypeSymbol.Name);
		}

		public static IEnumerable<TypeParameterConstraintSyntax> GetTypeParameterConstraints(
			this ITypeParameterSymbol typeParameterSymbol, SyntaxGenerator generator)
		{
			foreach (var type in typeParameterSymbol.ConstraintTypes)
				yield return SyntaxFactory.TypeConstraint((TypeSyntax) generator.TypeExpression(type));
			if (typeParameterSymbol.HasConstructorConstraint)
			{
				yield return SyntaxFactory.ConstructorConstraint();
			}
			if (typeParameterSymbol.HasReferenceTypeConstraint)
			{
				yield return SyntaxFactory.ClassOrStructConstraint(SyntaxKind.ClassConstraint);
			}
			if (typeParameterSymbol.HasValueTypeConstraint)
			{
				yield return SyntaxFactory.ClassOrStructConstraint(SyntaxKind.StructConstraint);
			}
		}

#if NO_0100
		/// <summary>
		/// Create a new MethodDeclaration that returns Task or Task{T} and has async modifier.
		/// </summary>
		/// <param name="originalMethod"></param>
		/// <param name="method"></param>
		/// <param name="model"></param>
		/// <returns></returns>
		public static MethodDeclarationSyntax WithAsync(this MethodDeclarationSyntax originalMethod, MethodDeclarationSyntax method, SemanticModel model)
		{
			if (!originalMethod.ReturnType.ToDisplayString(model).StartsWith($"{typeof(Task)}", StringComparison.Ordinal))
			{
				var returnType = method.ReturnType.ToString();
				method = method.
					WithReturnType(SyntaxFactory.ParseTypeName(
							returnType == "void" ? "Task" : $"Task<{returnType}>")
						.WithTrailingTrivia(originalMethod.ReturnType.GetTrailingTrivia())
					);
			}
			return method
				.WithModifiers(method.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword)))
				.WithAdditionalAnnotations(Formatter.Annotation);
		}
#endif

		public static MethodDeclarationSyntax WithoutAsync(this MethodDeclarationSyntax method)
		{
			var genericName = method.ReturnType as GenericNameSyntax;
			// if not generic type is either void or Task, in either case switch to void.
			method = method.WithReturnType(genericName != null
				? genericName.TypeArgumentList.Arguments[0]
				: SyntaxFactory.PredefinedType(
					SyntaxFactory.Token(SyntaxKind.VoidKeyword)));
			return method.WithModifiers(
				method.Modifiers.Remove(
					method.Modifiers.Single(e => e.IsKind(SyntaxKind.AsyncKeyword))));
		}

		public static bool IsGenericOfType(this SyntaxNode node, Type type, SemanticModel semanticModel)
		{
			if (node == null)
			{
				throw new ArgumentNullException(nameof(node));
			}
			if (type == null)
			{
				throw new ArgumentNullException(nameof(type));
			}
			if (semanticModel == null)
			{
				throw new ArgumentNullException(nameof(semanticModel));
			}
			if (!(node is TypeArgumentListSyntax))
			{
				return false;
			}
			SyntaxNode genericNameSyntax = null;
			if (node.Parent is GenericNameSyntax)
			{
				genericNameSyntax = node.Parent;
			}
			else if (node.Parent.Parent is GenericNameSyntax)
			{
				genericNameSyntax = node.Parent.Parent;
			}
			var simpleBaseTypeSyntax = genericNameSyntax?.Parent as SimpleBaseTypeSyntax;
			if (simpleBaseTypeSyntax == null || !(genericNameSyntax.Parent.Parent is BaseListSyntax))
			{
				return false;
			}
			var containingGeneric = simpleBaseTypeSyntax.Type as GenericNameSyntax;
			return containingGeneric != null && containingGeneric.IsConstructedGenericOf(type, semanticModel);
		}

		public static bool IsTypeArgumentToMethod(this SyntaxNode node, SemanticModel semanticModel,
			CancellationToken cancellationToken,
			IEnumerable<MethodInfo> methodInfos)
		{
			if (methodInfos == null)
			{
				return false;
			}
			if (!(node is TypeArgumentListSyntax))
			{
				return false;
			}
			if (!(node.Parent.Parent is MemberAccessExpressionSyntax))
			{
				return false;
			}
			if (!(node.Parent is GenericNameSyntax))
			{
				return false;
			}
			var methodInfoArray = methodInfos as MethodInfo[] ?? methodInfos.ToArray();
			if (methodInfoArray.All(e => e.Name != ((GenericNameSyntax) node.Parent).Identifier.Text))
			{
				return false;
			}
			return methodInfoArray.Any(
				methodInfo =>
					((InvocationExpressionSyntax) node.Parent.Parent.Parent).ArgumentList.Arguments[0].Expression.IsArgumentToMethod(
						semanticModel, cancellationToken, methodInfo));
		}

		public static bool IsArgumentToMethod(this SyntaxNode node, SemanticModel semanticModel,
			CancellationToken cancellationToken,
			IEnumerable<MethodInfo> methodInfos)
		{
			return methodInfos.Any(methodInfo => node.IsArgumentToMethod(semanticModel, cancellationToken, methodInfo));
		}

		public static bool IsArgumentToMethod(this SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken,
			MethodInfo methodInfo)
		{
			if (!(node is ObjectCreationExpressionSyntax) && !(node is IdentifierNameSyntax) && !(node is ArgumentSyntax))
			{
				return false;
			}
			var argument = node as ArgumentSyntax ?? node.Parent as ArgumentSyntax;
			var argumentList = argument?.Parent as ArgumentListSyntax;
			var invocationExpression = argumentList?.Parent as InvocationExpressionSyntax;
			if (invocationExpression == null)
			{
				return false;
			}
			return invocationExpression.IsInvocationOfMethod(
					methodInfo, semanticModel, cancellationToken);
		}

		public static bool IsImplementationOf(this IMethodSymbol methodSymbol, MethodInfo methodInfo)
		{
			return methodSymbol.ContainingType.ImplementsInterface(methodInfo.DeclaringType) &&
			       methodSymbol.Parameters.Length == methodInfo.GetParameters().Length && methodSymbol.Name == methodInfo.Name;
		}

		public static bool Invokes(this MethodDeclarationSyntax methodDeclaration, MethodInfo methodInfo,
			SemanticModel semanticModel, CancellationToken cancellationToken)
		{
			return methodDeclaration
				.Body
				.Statements
				.OfType<ExpressionStatementSyntax>()
				.Any(e => e.Expression.Kind() == SyntaxKind.InvocationExpression &&
				          ((InvocationExpressionSyntax) e.Expression).IsInvocationOfMethod(methodInfo, semanticModel,
					          cancellationToken));
		}

		public static bool IsCommandMessageType(this INamedTypeSymbol symbol)
		{
			return symbol != null && symbol.Name.StartsWith(Utilities.CommandMessageClassSuffix, StringComparison.Ordinal);
		}

		private static bool IsInvocationImpl(InvocationExpressionSyntax node, SemanticModel semanticModel,
			Func<SimpleNameSyntax, SemanticModel, CancellationToken, bool> predicate, CancellationToken cancellationToken)
		{
			var memberAccessExpression = node.Expression as MemberAccessExpressionSyntax;
			if (memberAccessExpression != null)
			{
				return predicate(memberAccessExpression.Name, semanticModel, cancellationToken);
			}
			var memberBindingExpression = node.Expression as MemberBindingExpressionSyntax;
			if (memberBindingExpression != null)
			{
				return predicate(memberBindingExpression.Name, semanticModel, cancellationToken);
			}
			return false;
		}

		public static bool IsInvocationOfMethod(this InvocationExpressionSyntax invocationExpression,
			IEnumerable<MethodInfo> methodInfos, SemanticModel semanticModel, CancellationToken cancellationToken)
		{
			return methodInfos.Any(m => invocationExpression.IsInvocationOfMethod(m, semanticModel, cancellationToken));
		}

		public static bool IsInvocationOfMethod(this InvocationExpressionSyntax invocationExpression, MethodInfo methodInfo, SemanticModel semanticModel, CancellationToken cancellationToken)
		{
			return IsInvocationImpl(invocationExpression, semanticModel,
				(name, model, c) => Utilities.IsMember(name, methodInfo, model, c), cancellationToken);
		}

		public static bool IsInvocationOfPublish(this InvocationExpressionSyntax node, SemanticModel semanticModel, CancellationToken cancellationToken)
		{
			return IsInvocationImpl(node, semanticModel, Utilities.IsPublish, cancellationToken);
		}

		public static bool IsInvocationOfSend(this InvocationExpressionSyntax node, SemanticModel semanticModel, CancellationToken cancellationToken)
		{
			return IsInvocationImpl(node, semanticModel, Utilities.IsSend, cancellationToken);
		}

		public static bool IsInvocationOfHandle(this InvocationExpressionSyntax node, SemanticModel semanticModel, CancellationToken cancellationToken)
		{
			return IsInvocationImpl(node, semanticModel, Utilities.IsHandle, cancellationToken);
		}

		public static bool IsConstructedGenericOf(this GenericNameSyntax typeSyntax, Type type, SemanticModel semanticModel)
		{
			var symbolInfo = semanticModel.GetSymbolInfo(typeSyntax);
			var typeSymbol = symbolInfo.Symbol as ITypeSymbol;
			if (typeSymbol != null)
			{
				return typeSymbol.IsOfType(type);
			}
			return false;
		}

		public static string GetFullNamespaceName(this INamespaceSymbol @namespace)
		{
			return @namespace.ToString();
		}

		public static SyntaxToken GetIdentifier(this MemberDeclarationSyntax memberDeclarationSyntax)
		{
			var method = memberDeclarationSyntax as MethodDeclarationSyntax;
			if (method != null) return method.Identifier;
			var @delegate = memberDeclarationSyntax as DelegateDeclarationSyntax;
			if (@delegate != null) return @delegate.Identifier;
			var type = memberDeclarationSyntax as TypeDeclarationSyntax;
			if (type != null) return type.Identifier;
			var constructor = memberDeclarationSyntax as ConstructorDeclarationSyntax;
			if (constructor != null) return constructor.Identifier;
			var enumMember = memberDeclarationSyntax as EnumMemberDeclarationSyntax;
			if (enumMember != null) return enumMember.Identifier;
			return default(SyntaxToken);
		}

		public static Document ReplaceNode(this Document document, SyntaxNode original, SyntaxNode replacement, out SyntaxNode root, out SemanticModel model)
		{
			var currentRoot = document.GetSyntaxRootAsync().Result;
			var newRoot = currentRoot.ReplaceNode(original, replacement);
			var newDocument = document.WithSyntaxRoot(newRoot);
			root = newDocument.GetSyntaxRootAsync().Result;
			model = newDocument.GetSemanticModelAsync().Result;
			return newDocument;
		}

		public static bool ImplementsInterface<TInterface>(this TypeDeclarationSyntax typeDeclaration,
			SemanticModel model)
		{
			var type = typeof(TInterface);
			if (!type.GetTypeInfo().IsInterface)
			{
				throw new ArgumentException("Type is not an interface", nameof(TInterface));
			}
			return typeDeclaration?.BaseList != null &&
			       typeDeclaration.BaseList.Types.Select(baseType => model.GetTypeInfo(baseType.Type))
				       .Any(typeInfo => typeInfo.Type.IsOfType<TInterface>()
				                        || typeInfo.Type.ImplementsInterface<TInterface>());
		}

		public static bool ImplementsInterface(this ITypeSymbol typeSymbol, Type type)
		{
			if (!type.GetTypeInfo().IsInterface)
			{
				throw new ArgumentException("Type is not an interface", nameof(typeSymbol));
			}

			if (typeSymbol.Interfaces.Concat(new[] {typeSymbol})
				.Any(e => e.IsOfType(type)))
			{
				return true;
			}

			return typeSymbol.AllInterfaces.Concat(new[] {typeSymbol})
				.Any(e => e.IsOfType(type));
		}

		public static bool ImplementsInterface<TInterface>(this ITypeSymbol typeSymbol)
		{
			return ImplementsInterface(typeSymbol, typeof(TInterface));
		}

		public static bool IsOfType<T>(this ITypeSymbol typeSymbol)
		{
			return IsOfType(typeSymbol, typeof(T));
		}

		public static bool IsOfType(this ITypeSymbol typeSymbol, Type type)
		{
			if (type.GetTypeInfo().IsGenericTypeDefinition)
			{
				return
					$"{typeSymbol.ContainingNamespace}.{typeSymbol.OriginalDefinition.MetadataName}, {typeSymbol.ContainingAssembly.Identity}" ==
					type.AssemblyQualifiedName;
			}
			return typeSymbol.ToString() == type.FullName &&
			       $"{typeSymbol}, {typeSymbol.ContainingAssembly.Identity}" == type.AssemblyQualifiedName;
		}
	}
}