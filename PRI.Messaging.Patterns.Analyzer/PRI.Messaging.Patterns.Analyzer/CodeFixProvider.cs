using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using PRI.Messaging.Patterns.Analyzer.Utility;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Primitives;

namespace PRI.Messaging.Patterns.Analyzer
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PRIMessagingPatternsAnalyzerCodeFixProvider)), Shared]
	public class PRIMessagingPatternsAnalyzerCodeFixProvider : CodeFixProvider
	{
		public sealed override ImmutableArray<string> FixableDiagnosticIds
			=> ImmutableArray.Create(PRIMessagingPatternsAnalyzer.RuleMp0100.Id, PRIMessagingPatternsAnalyzer.RuleMp0101.Id/*,
				PRIMessagingPatternsAnalyzer.RuleMp0102.Id, PRIMessagingPatternsAnalyzer.RuleMp0103.Id*/);

		// See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
		public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		private readonly Dictionary<string, KeyValuePair<string, Func<Diagnostic, Document, CancellationToken, Task<Document>>>> documentDiagnosticInvocations
			= new Dictionary<string, KeyValuePair<string, Func<Diagnostic, Document, CancellationToken, Task<Document>>>>
			{
				{"MP0100", new KeyValuePair<string, Func<Diagnostic, Document, CancellationToken, Task<Document>>>("bleah", InvokeMp0100)},
				{"MP0101", new KeyValuePair<string, Func<Diagnostic, Document, CancellationToken, Task<Document>>>("bleah", InvokeMp0101)},
			};

		private static async Task<Document> InvokeMp0101(Diagnostic diagnostic, Document document, CancellationToken cancellationToken)
		{
			var diagnosticSpan = diagnostic.Location.SourceSpan;
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			// Find the type declaration identified by the diagnostic.
			var invocation =
				root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();
			var model = await document.GetSemanticModelAsync(cancellationToken);
			var invocationParent = invocation.Parent;
			if (!(invocationParent is AwaitExpressionSyntax))
			{
				throw new InvalidOperationException();
			}
			var symbol = GetAssignmentSymbol(invocationParent, model);

			if (symbol == null)
			{
				// possible?
				throw new InvalidOperationException();
			}
			var containingMethod = GetContainingMethod(invocationParent);
			var methodIdentifierTokens = containingMethod.DescendantTokens().Where(e => e.Kind() == SyntaxKind.IdentifierToken);

			// all references to the result of RequestAsync
			var resultReferences = methodIdentifierTokens
				.Where(token => token.ValueText == symbol.Name)
				.ToArray();

			// result reference locations
			var locations = resultReferences
				.Select(token => token.GetAncestorStatement().GetLocation()) // ancestor statement or entire line?
				.OrderBy(e=>e.SourceSpan.Start);

			var textSpan = GetBoundingSpan(locations);
			var candidateTextSpan = textSpan;
			
			// all *other* references in that candidate span
			var spanNewIdentifierTokens = containingMethod.DescendantTokens(candidateTextSpan)
					.Where(e=>e.Kind() == SyntaxKind.IdentifierToken && !resultReferences.Contains(e));

			var tokenKeyComparer = Comparer<int>.Default;
			var outerDeclarationIdentifierTokens = new SortedSet<SyntaxToken>(Comparer<SyntaxToken>
				.Create((token, syntaxToken) => tokenKeyComparer.Compare(
					token.GetLocation().SourceSpan.Start, syntaxToken.GetLocation().SourceSpan.Start)));

			foreach (var token in spanNewIdentifierTokens)
			{
				var syntaxes = token.GetReferencingVariableDeclarations(model);
				if (syntaxes.Length == 0)
				{
					continue;
				}
				foreach(var identifier in syntaxes.Select(e=>e.Identifier))
					outerDeclarationIdentifierTokens.Add(identifier);
			}
#if DEBUG
			// locations of result references and references to anything declared within locations of results references
			TextSpan tryTextSpan;
			if (outerDeclarationIdentifierTokens.Any())
			{
				var finalLocations = locations
					.Concat(outerDeclarationIdentifierTokens.Select(e => e.GetAncestorStatement().GetLocation()))
					.OrderBy(e => e.SourceSpan.Start);

				tryTextSpan = new TextSpan(finalLocations.First().SourceSpan.Start, finalLocations.Last().SourceSpan.End - finalLocations.First().SourceSpan.Start + 1);
			}
			else
			{
				tryTextSpan = candidateTextSpan;
			}
#endif
			//SyntaxFactory.Parse.Block(containingMethod.DescendantNodes(tryTextSpan).Select(e=>e.S))
			// verify invocation is the one we want
			var methodSymbolInfo = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

			if (methodSymbolInfo == null || methodSymbolInfo.TypeArguments.Length < 3)
			{
				System.Diagnostics.Debug.WriteLine("uh oh");
				throw new InvalidOperationException();
			}

			// assume, RequestAsync return is assigned to something (covered by other rule)
			// 1: get 3rd type parameter in invocation
			var errorEventType = methodSymbolInfo.TypeArguments.ElementAt(2);
			var generator = SyntaxGenerator.GetGenerator(document);
			var exceptionType = typeof(ReceivedErrorEventException<>).AsTypeSyntax(generator, errorEventType);

			var tryBlockStatements = containingMethod.SyntaxTree
				.GetRoot().DescendantNodes()
				.Where(x => tryTextSpan.Contains(x.Span))
				.OfType<StatementSyntax>();

			var syntaxTrivia = SyntaxFactory.Comment("// TODO: do something with ex.ErrorEvent");
			var exceptionArgumentIdentifierName = SyntaxFactory.IdentifierName("ex");

			var catchStatements = SyntaxFactory.SingletonList<StatementSyntax>(
				SyntaxFactory.ExpressionStatement(
				SyntaxFactory.InvocationExpression(
					SyntaxFactory.MemberAccessExpression(
						SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName(
							typeof(System.Diagnostics.Debug).FullName),
						SyntaxFactory.Token(SyntaxKind.DotToken),
						SyntaxFactory.IdentifierName(nameof(System.Diagnostics.Debug.WriteLine))),
					SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.Argument(SyntaxFactory.MemberAccessExpression(
							SyntaxKind.SimpleMemberAccessExpression,
							exceptionArgumentIdentifierName,
							SyntaxFactory.Token(SyntaxKind.DotToken),
							SyntaxFactory.IdentifierName(nameof(ReceivedErrorEventException<IEvent>.ErrorEvent))))))))
							.WithLeadingTrivia(syntaxTrivia)
			);
			catchStatements = SyntaxFactory.SingletonList<StatementSyntax>(
				SyntaxFactory.ExpressionStatement(
					SyntaxFactory.InvocationExpression(
							SyntaxFactory.MemberAccessExpression(
								SyntaxKind.SimpleMemberAccessExpression,
								SyntaxFactory.MemberAccessExpression(
									SyntaxKind.SimpleMemberAccessExpression,
									SyntaxFactory.MemberAccessExpression(
										SyntaxKind.SimpleMemberAccessExpression,
										SyntaxFactory.AliasQualifiedName(
											SyntaxFactory.IdentifierName(
												SyntaxFactory.Token(/*
													SyntaxFactory.TriviaList(
														SyntaxFactory.Comment("// TODO: do something with ex.ErrorEvent"),
														SyntaxFactory.CarriageReturnLineFeed),*/
													SyntaxKind.GlobalKeyword/*,
													SyntaxFactory.TriviaList()*/)
													.WithLeadingTrivia(SyntaxFactory.Comment("// TODO: do something with ex.ErrorEvent"), SyntaxFactory.CarriageReturnLineFeed)
													),
											SyntaxFactory.IdentifierName(nameof(System))),
										SyntaxFactory.IdentifierName(nameof(System.Diagnostics))),
									SyntaxFactory.IdentifierName(nameof(System.Diagnostics.Debug))),
								SyntaxFactory.IdentifierName(nameof(System.Diagnostics.Debug.WriteLine))))
						.WithArgumentList(
							SyntaxFactory.ArgumentList(
								SyntaxFactory.SingletonSeparatedList(
									SyntaxFactory.Argument(
										SyntaxFactory.MemberAccessExpression(
											SyntaxKind.SimpleMemberAccessExpression,
											exceptionArgumentIdentifierName,
											SyntaxFactory.IdentifierName(nameof(ReceivedErrorEventException<IEvent>.ErrorEvent)))))))));

			var tryBlock = SyntaxFactory.Block(SyntaxFactory.List(tryBlockStatements));
			var catchList = new SyntaxList<CatchClauseSyntax>();
			catchList = catchList.Add(SyntaxFactory.CatchClause(
				SyntaxFactory.CatchDeclaration(exceptionType,
					exceptionArgumentIdentifierName.Identifier),
				default(CatchFilterClauseSyntax),
				SyntaxFactory.Block(catchStatements)));

			catchList = new SyntaxList<CatchClauseSyntax>().Add(
				SyntaxFactory.CatchClause()
					.WithDeclaration(
						SyntaxFactory.CatchDeclaration(exceptionType)
							.WithIdentifier(exceptionArgumentIdentifierName.Identifier))
					.WithBlock(SyntaxFactory.Block(catchStatements)
					)
			);
			var tryStatement = SyntaxFactory.TryStatement(SyntaxFactory.Token(SyntaxKind.TryKeyword),
				tryBlock, catchList, default(FinallyClauseSyntax));

			var newMethod = containingMethod.ReplaceNodes(tryBlockStatements.ToImmutableList(), tryStatement);
			root = root.ReplaceNode(containingMethod, newMethod);

			// 2: add a try/catch block around RequestAsync 
			// add block around RequestAsync call up to last
			// access to any variable the result is assigned to.
			// a: find span that would best fit the try
			// 
			// b: add the catch with defualt code:
			// catch(ReceivedErrorEventException<{3rd type parameter}> ex)
			// { throw new NotImplementedException(ex.Message); }
			// 3: catch...

			var compilationUnitSyntax = root as CompilationUnitSyntax;

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
			Debug.WriteLine("");
			return document.WithSyntaxRoot(compilationUnitSyntax);
		}

		private static TextSpan GetBoundingSpan(IOrderedEnumerable<Location> locations)
		{
			return new TextSpan(locations.First().SourceSpan.Start, locations.Last().SourceSpan.End - locations.First().SourceSpan.Start + 1);
		}

		private static IEnumerable<StatementSyntax> GetStatements(SyntaxNode container, TextSpan tryTextSpan)
		{
			return container
				.DescendantNodes(tryTextSpan).Where(e => e is StatementSyntax || e is ExpressionSyntax)
				.Select(ToStatement);
		}

		private static StatementSyntax ToStatement(SyntaxNode node)
		{
			var x = node as ExpressionSyntax;
			return x != null ? SyntaxFactory.ExpressionStatement(x) : (StatementSyntax) node;
		}

		private static MemberDeclarationSyntax GetContainingMethod(SyntaxNode invocationParent)
		{
			var parentMethod = invocationParent;
			while (parentMethod != null && !(parentMethod is MemberDeclarationSyntax))
			{
				parentMethod = parentMethod.Parent;
			}

			var containingMethod = parentMethod as MemberDeclarationSyntax;
			return containingMethod;
		}

		private static ISymbol GetAssignmentSymbol(SyntaxNode parent, SemanticModel model)
		{
			var simpleAssignment = parent.Parent as AssignmentExpressionSyntax;

			ISymbol symbol = null;

			if (simpleAssignment != null)
			{
				ExpressionSyntax variable = simpleAssignment.Left;
				symbol = model.GetSymbolInfo(variable).Symbol;
			}
			else
			{
				var equals = parent.Parent as EqualsValueClauseSyntax;
				var variableDeclarator = @equals?.Parent as VariableDeclaratorSyntax;
				if (variableDeclarator != null)
				{
					symbol = model.GetDeclaredSymbol(variableDeclarator);
				}
			}
			return symbol;
		}

		private static async Task<Document> InvokeMp0100(Diagnostic diagnostic, Document document, CancellationToken cancellationToken)
		{
			var diagnosticSpan = diagnostic.Location.SourceSpan;
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

			// Find the type declaration identified by the diagnostic.
			var invocation = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();
			return await InvokeAsync(document, invocation, cancellationToken);
		}

		public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var document = context.Document;

			foreach (var diagnostic in context.Diagnostics)
			{
				if (!documentDiagnosticInvocations.ContainsKey(diagnostic.Id))
				{
					continue;
				}
				// Register a code action that will invoke the fix.
				var documentDiagnosticInvocation = documentDiagnosticInvocations[diagnostic.Id];
				context.RegisterCodeFix(
					CodeAction.Create(
						title: documentDiagnosticInvocation.Key,
						createChangedDocument: c => documentDiagnosticInvocation.Value(diagnostic, document, c),
						equivalenceKey: diagnostic.Id),
					diagnostic);
			}
			return Task.FromResult(true);
		}

		private static MethodDeclarationSyntax WithAsync(MethodDeclarationSyntax originalMethod, MethodDeclarationSyntax method, SemanticModel model)
		{
			if (!originalMethod.ReturnType.ToDisplayString(model).StartsWith("System.Threading.Tasks.Task", System.StringComparison.Ordinal))
			{
				var returnType = method.ReturnType.ToString();
				method = method.
					WithReturnType(SyntaxFactory.ParseTypeName(
						returnType == "void" ? "Task" : $"Task<{returnType}>")
						.WithTrailingTrivia(originalMethod.ReturnType.GetTrailingTrivia())
					);
			}
			method = method.WithModifiers(method.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword)));
			return method;
		}

		private static async Task<Document> InvokeAsync(Document document, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
		{
			var parentMethodDeclaration = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
			var annotation = new SyntaxAnnotation(Guid.NewGuid().ToString("D"));
			var newInvocation = SyntaxFactory.AwaitExpression(invocation)
				.WithAdditionalAnnotations(annotation);
			var root = await document.GetSyntaxRootAsync(cancellationToken);
			root = root.ReplaceNode(invocation, newInvocation);
			newInvocation = (AwaitExpressionSyntax) root.GetAnnotatedNodes(annotation).Single();
			var newParentMethodDeclaration = newInvocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
			var model = await document.GetSemanticModelAsync(cancellationToken);
			if (newParentMethodDeclaration != null)
			{
				root = root.ReplaceNode(newParentMethodDeclaration, WithAsync(parentMethodDeclaration, newParentMethodDeclaration, model));
			}

			var compilationUnitSyntax = root as CompilationUnitSyntax;

			var usings = compilationUnitSyntax.Usings;
			if (!usings.Any(e => e.Name.ToString().Equals(typeof(Task).Namespace)))
			{
				var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(typeof(Task).Namespace));
				root = compilationUnitSyntax.AddUsings(usingDirective);
			}
			return document.WithSyntaxRoot(root);
		}

		private async Task<Solution> MakeUppercaseAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
		{
			// Compute new uppercase name.
			var identifierToken = typeDecl.Identifier;
			var newName = identifierToken.Text.ToUpperInvariant();

			// Get the symbol representing the type to be renamed.
			var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
			var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

			// Produce a new solution that has all references to that type renamed, including the declaration.
			var originalSolution = document.Project.Solution;
			var optionSet = originalSolution.Workspace.Options;
			var newSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, typeSymbol, newName, optionSet, cancellationToken).ConfigureAwait(false);

			// Return the new solution with the now-uppercase type name.
			return newSolution;
		}
	}
}