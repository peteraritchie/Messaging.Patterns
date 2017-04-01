using System;
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

		private readonly Dictionary<string, KeyValuePair<string, Func<Diagnostic, Document, CancellationToken, Task<Document>>>> _documentDiagnosticInvocations
			= new Dictionary<string, KeyValuePair<string, Func<Diagnostic, Document, CancellationToken, Task<Document>>>>
			{
				{"MP0100", new KeyValuePair<string, Func<Diagnostic, Document, CancellationToken, Task<Document>>>("bleah", InvokeMp0100)},
				{"MP0101", new KeyValuePair<string, Func<Diagnostic, Document, CancellationToken, Task<Document>>>("bleah", InvokeMp0101)},
			};

		private static async Task<Document> InvokeMp0101(Diagnostic diagnostic, Document document,
			CancellationToken cancellationToken)
		{
			// 2: add a try/catch block around RequestAsync 
			// add block around RequestAsync call up to last
			// access to any variable the result is assigned to.
			// a: find span that would best fit the try
			// 
			// b: add the catch with default code:
			// catch(ReceivedErrorEventException<{3rd type parameter}> ex)
			// { throw new NotImplementedException(ex.Message); }
			// 3: catch...

			var model = await document.GetSemanticModelAsync(cancellationToken);
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			Func<IdentifierNameSyntax, SyntaxList<StatementSyntax>> generateCatchStatements =
				exceptionIdentifierName => SyntaxFactory.SingletonList<StatementSyntax>(
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
													SyntaxFactory.Token(SyntaxKind.GlobalKeyword)
														.WithLeadingTrivia(SyntaxFactory.Comment("// TODO: do something with ex.ErrorEvent"),
															SyntaxFactory.CarriageReturnLineFeed)
												),
												SyntaxFactory.IdentifierName(nameof(System))),
											SyntaxFactory.IdentifierName(nameof(System.Diagnostics))),
										SyntaxFactory.IdentifierName(nameof(Debug))),
									SyntaxFactory.IdentifierName(nameof(Debug.WriteLine))))
							.WithArgumentList(
								SyntaxFactory.ArgumentList(
									SyntaxFactory.SingletonSeparatedList(
										SyntaxFactory.Argument(
											SyntaxFactory.MemberAccessExpression(
												SyntaxKind.SimpleMemberAccessExpression,
												exceptionIdentifierName,
												SyntaxFactory.IdentifierName(nameof(ReceivedErrorEventException<IEvent>.ErrorEvent)))))))));
			// Find the type declaration identified by the diagnostic.
			var subjectToken = root.FindToken(diagnostic.Location.SourceSpan.Start);
			var invocation =
				subjectToken.Parent.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();

			// verify invocation is the one we want
			if (!(invocation.Parent is AwaitExpressionSyntax))
			{
				throw new InvalidOperationException($"{Facts.GetCurrentMethodName()} operates on an await statement, and await statement not found.");
			}

			var methodSymbolInfo = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

			if (methodSymbolInfo == null || methodSymbolInfo.TypeArguments.Length < 3)
			{
				throw new InvalidOperationException();
			}

			// assume, RequestAsync return is assigned to something (covered by other rule)
			// 1: get 3rd type parameter in invocation
			var errorEventType = methodSymbolInfo.TypeArguments.ElementAt(2);
			var generator = SyntaxGenerator.GetGenerator(document);
			var exceptionArgumentsInfo = new Dictionary<string, TypeSyntax>
			{
				{"ex", typeof(ReceivedErrorEventException<>).AsTypeSyntax(generator, errorEventType)}
			};

			return
				document.WithSyntaxRoot(Microsoft.CodeAnalysis.Formatting.Formatter.Format(
					subjectToken.GetAncestorStatement().TryCatchSafe(exceptionArgumentsInfo,
					generateCatchStatements,
					root,
					model), document.Project.Solution.Workspace, document.Project.Solution.Workspace.Options));
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
				if (!_documentDiagnosticInvocations.ContainsKey(diagnostic.Id))
				{
					continue;
				}
				// Register a code action that will invoke the fix.
				var documentDiagnosticInvocation = _documentDiagnosticInvocations[diagnostic.Id];
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
			if (!originalMethod.ReturnType.ToDisplayString(model).StartsWith($"{typeof(System.Threading.Tasks.Task)}", StringComparison.Ordinal))
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

			var compilationUnitSyntax = (CompilationUnitSyntax) root;

			var usings = compilationUnitSyntax.Usings;
			if (!usings.Any(e => e.Name.ToString().Equals(typeof(Task).Namespace)))
			{
				var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(typeof(Task).Namespace));
				root = compilationUnitSyntax.AddUsings(usingDirective);
			}
			return document.WithSyntaxRoot(root);
		}
	}
}