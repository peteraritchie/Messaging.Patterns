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
using PRI.Messaging.Patterns.Analyzer.Utility;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Primitives;

namespace PRI.Messaging.Patterns.Analyzer
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PRIMessagingPatternsAnalyzerCodeFixProvider)), Shared]
	public class PRIMessagingPatternsAnalyzerCodeFixProvider : CodeFixProvider
	{
		public sealed override ImmutableArray<string> FixableDiagnosticIds
			=> ImmutableArray.Create(PRIMessagingPatternsAnalyzer.RuleMp0100.Id, PRIMessagingPatternsAnalyzer.RuleMp0101.Id,
				PRIMessagingPatternsAnalyzer.RuleMp0102.Id/*, PRIMessagingPatternsAnalyzer.RuleMp0103.Id*/);

		// See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
		public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		private readonly Dictionary<string, KeyValuePair<string, Func<Diagnostic, Document, CancellationToken, Task<Document>>>> _documentDiagnosticInvocations
			= new Dictionary<string, KeyValuePair<string, Func<Diagnostic, Document, CancellationToken, Task<Document>>>>
			{
				{"MP0100", new KeyValuePair<string, Func<Diagnostic, Document, CancellationToken, Task<Document>>>("bleah", InvokeMp0100)},
				{"MP0101", new KeyValuePair<string, Func<Diagnostic, Document, CancellationToken, Task<Document>>>("bleah", InvokeMp0101)},
			};
		private readonly Dictionary<string, KeyValuePair<string, Func<Diagnostic, Solution, Document, CancellationToken, Task<Solution>>>> _solutionDiagnosticInvocations
			= new Dictionary<string, KeyValuePair<string, Func<Diagnostic, Solution, Document, CancellationToken, Task<Solution>>>>
			{
				{"MP0102", new KeyValuePair<string, Func<Diagnostic, Solution, Document, CancellationToken, Task<Solution>>>("bleah", InvokeMp0102)},
			};

		private static async Task<Solution> InvokeMp0102(Diagnostic diagnostic, Solution solution, Document document, CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			var subjectToken = root.FindToken(diagnostic.Location.SourceSpan.Start);
			var invocation =
				subjectToken.Parent.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();
			var model = await document.GetSemanticModelAsync(cancellationToken);
			var methodSymbolInfo = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

			if (methodSymbolInfo == null || methodSymbolInfo.TypeArguments.Length < 3)
			{
				throw new InvalidOperationException();
			}
			ITypeSymbol errorEventType, messageType, eventType;
			GetRequestAsyncInfo(methodSymbolInfo, out messageType, out eventType, out errorEventType);

			// TODO: create new document
			var namedEventTypeSymbol = (INamedTypeSymbol)eventType;
			var generator = SyntaxGenerator.GetGenerator(document);
			// need to create a new type from eventType that is non-constructed
			var typeSyntaxes = namedEventTypeSymbol.TypeParameters.Select(e=>(TypeSyntax)generator.TypeExpression(e));
			var classDeclaration = SyntaxFactory.ClassDeclaration($"{eventType.Name}Handler")
				.WithBaseList(
					SyntaxFactory.BaseList(
						SyntaxFactory.SeparatedList<BaseTypeSyntax>(new[]
						{
							SyntaxFactory.SimpleBaseType(
								SyntaxFactory.GenericName("IConsumer")
									.WithTypeArgumentList(SyntaxFactory.TypeArgumentList(
										SyntaxFactory.SeparatedList(new[]
										{
											(TypeSyntax) SyntaxFactory.GenericName(namedEventTypeSymbol.Name)
												.WithTypeArgumentList(
													SyntaxFactory.TypeArgumentList(
														SyntaxFactory.SeparatedList(
															namedEventTypeSymbol.ConstructedFrom.TypeArguments
																.Select(e => (TypeSyntax) generator.TypeExpression(e)))))
										})
										//.Select(e => SyntaxFactory.TypeParameter(e.GetFirstToken()))
										//new[] {eventTypeExpression}
									)))
						})))
						.WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)));

			if (namedEventTypeSymbol.IsGenericType)
			{
				classDeclaration =
					classDeclaration.WithTypeParameterList(
							SyntaxFactory.TypeParameterList(
								SyntaxFactory.SeparatedList(
									namedEventTypeSymbol.TypeParameters.Select(generator.TypeExpression)
										.Select(e => SyntaxFactory.TypeParameter(e.GetFirstToken())))))
						.AddConstraintClauses(
							namedEventTypeSymbol.TypeParameters
								.Select(e => SyntaxFactory.TypeParameterConstraintClause(
									SyntaxFactory.IdentifierName(e.Name),
									SyntaxFactory.SeparatedList(Get(e, generator)))).ToArray());
				string text = classDeclaration.NormalizeWhitespace().ToString();
			}
			throw new NotImplementedException();
			// add file to project

			return solution;
		}

		private static IEnumerable<TypeParameterConstraintSyntax> Get(ITypeParameterSymbol typeParameterSymbol, SyntaxGenerator generator)
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

		//private static TypeParameterConstraintClauseSyntax Get(ImmutableArray<ITypeParameterSymbol> typeParameterSymbols, SyntaxGenerator generator)
		//{
		//	foreach (var typeParameterSymbol in typeParameterSymbols)
		//	{
		//		generator.WithTypeConstraint(()
		//		yield return SyntaxFactory.TypeParameterConstraintClause(SyntaxFactory.IdentifierName(typeParameterSymbol.Name), typeParameterSymbol.ConstraintTypes.Select(e=>SyntaxFactory.TypeParameterConstraintClause(e)))
		//	}
		//	throw new NotImplementedException();
		//}


		private static void GetRequestAsyncInfo(IMethodSymbol methodSymbolInfo, out ITypeSymbol messageType, out ITypeSymbol eventType, out ITypeSymbol errorEventType)
		{
			messageType = methodSymbolInfo.TypeArguments.ElementAt(0);
			eventType = methodSymbolInfo.TypeArguments.ElementAt(1);
			errorEventType = methodSymbolInfo.TypeArguments.ElementAt(2);
		}

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
			ITypeSymbol errorEventType, messageType, eventType;
			GetRequestAsyncInfo(methodSymbolInfo, out messageType, out eventType, out errorEventType);
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
			var solution = context.Document.Project.Solution;

			foreach (var diagnostic in context.Diagnostics)
			{
				if (_documentDiagnosticInvocations.ContainsKey(diagnostic.Id))
				{
					// Register a code action that will invoke the fix.
					var documentDiagnosticInvocation = _documentDiagnosticInvocations[diagnostic.Id];
					context.RegisterCodeFix(
						CodeAction.Create(
							title: documentDiagnosticInvocation.Key,
							createChangedDocument: c => documentDiagnosticInvocation.Value(diagnostic, document, c),
							equivalenceKey: diagnostic.Id),
						diagnostic);
				}
				else if (_solutionDiagnosticInvocations.ContainsKey(diagnostic.Id))
				{
					var solutionDiagnosticInvocation = _solutionDiagnosticInvocations[diagnostic.Id];
					context.RegisterCodeFix(
						CodeAction.Create(
							title:"",
							createChangedSolution: c => solutionDiagnosticInvocation.Value(diagnostic, solution, document, c),
							equivalenceKey: diagnostic.Id),
						diagnostic);
				}
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