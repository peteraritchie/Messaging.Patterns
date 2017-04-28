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
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using PRI.Messaging.Patterns.Analyzer.Utility;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace PRI.Messaging.Patterns.Analyzer
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PRIMessagingPatternsAnalyzerCodeFixProvider)), Shared]
	public class PRIMessagingPatternsAnalyzerCodeFixProvider : CodeFixProvider
	{
		public sealed override ImmutableArray<string> FixableDiagnosticIds
			=> ImmutableArray.Create(
#if NO_0100
				PRIMessagingPatternsAnalyzer.RuleMp0100.Id,
#endif
				PRIMessagingPatternsAnalyzer.RuleMp0101.Id,
				PRIMessagingPatternsAnalyzer.RuleMp0102.Id/*, PRIMessagingPatternsAnalyzer.RuleMp0103.Id*/);

		// See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
		public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		private readonly Dictionary<string, KeyValuePair<string, Func<Diagnostic, Document, CancellationToken, Task<Document>>>> _documentDiagnosticInvocations
			= new Dictionary<string, KeyValuePair<string, Func<Diagnostic, Document, CancellationToken, Task<Document>>>>
			{
#if NO_0100
				{"MP0100", new KeyValuePair<string, Func<Diagnostic, Document, CancellationToken, Task<Document>>>("bleah", InvokeMp0100)},
#endif
				{"MP0101", new KeyValuePair<string, Func<Diagnostic, Document, CancellationToken, Task<Document>>>("bleah", InvokeMp0101)},
			};
		private readonly Dictionary<string, KeyValuePair<string, Func<Diagnostic, Solution, Document, CancellationToken, Task<Solution>>>> _solutionDiagnosticInvocations
			= new Dictionary<string, KeyValuePair<string, Func<Diagnostic, Solution, Document, CancellationToken, Task<Solution>>>>
			{
				{"MP0102", new KeyValuePair<string, Func<Diagnostic, Solution, Document, CancellationToken, Task<Solution>>>("bleah", InvokeMp0102)},
			};


		/// TODO: this might be better as a visitor
		/// offer to break out the continuation into a success event handler the exception block into an error event handler
		/// TODO: hooking them up where other bus.AddHandlers are called
		/// and replace RequestAsync with Send
		/// with TODOs to verify storage of state and retrieval of state
		private static async Task<Solution> InvokeMp0102(Diagnostic diagnostic, Solution solution, Document document,
			CancellationToken cancellationToken)
		{
			// the diagnostic.Location here will be the span of the RequestAsync<> GenericNameSyntax object

			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			var model = await document.GetSemanticModelAsync(cancellationToken);
			var generator = SyntaxGenerator.GetGenerator(document);

			MemberAccessExpressionSyntax requestAsyncMemberAccess;
			SyntaxNode requestAsyncInvocationStatement;
			StatementSyntax[] requestAsyncAndDependantStatements;
			string fullContainingNamespaceName;
			SyntaxToken handleMethodParameterName;
			var containingMemberAnnotation = new SyntaxAnnotation(Guid.NewGuid().ToString("D"));
			var tryAnnotation = new SyntaxAnnotation(Guid.NewGuid().ToString("D"));
			var subjectNodeAnnotation = new SyntaxAnnotation(Guid.NewGuid().ToString("D"));
			INamedTypeSymbol errorEventType;
			INamedTypeSymbol eventType;
			BlockSyntax eventHandlerHandleMethodBody;
			SyntaxToken catchExceptionIdentifier = default(SyntaxToken);
			CatchClauseSyntax catchStatement = null;
			SyntaxToken errorEventHandlerMessageParameterIdentifier;
			{
				var subjectNode = root.FindNode(diagnostic.Location.SourceSpan);
				requestAsyncMemberAccess = subjectNode.Parent.AncestorsAndSelf().OfType<MemberAccessExpressionSyntax>().First();

				INamedTypeSymbol messageType;
				Utilities.GetRequestAsyncInfo(requestAsyncMemberAccess, model, out messageType, out eventType, out errorEventType);

				// Get span of code around RequestAsync for event handler
				handleMethodParameterName = Identifier(
					requestAsyncMemberAccess.GetAssignmentSymbol(model, cancellationToken).Name);

				document = document.ReplaceNode(subjectNode, subjectNode.WithAdditionalAnnotations(subjectNodeAnnotation), out root,
					out model);
				subjectNode = root.GetAnnotatedNodes(subjectNodeAnnotation).Single();

				var containingMember = subjectNode.GetContainingMemberDeclaration() as MethodDeclarationSyntax;
				Debug.Assert(containingMember != null, "containingMember != null");
				fullContainingNamespaceName =
					// ReSharper disable once PossibleNullReferenceException
					model.GetDeclaredSymbol(containingMember.Parent, cancellationToken).ContainingNamespace.GetFullNamespaceName();

				document = document.ReplaceNode(containingMember,
					containingMember
						.WithAdditionalAnnotations(containingMemberAnnotation)
						.WithAdditionalAnnotations(Formatter.Annotation),
					out root, out model);
				containingMember = (MethodDeclarationSyntax) root.GetAnnotatedNodes(containingMemberAnnotation).Single();
				subjectNode = root.GetAnnotatedNodes(subjectNodeAnnotation).Single();
				requestAsyncInvocationStatement = subjectNode.GetAncestorStatement();
				var eventHandlerStatementsSpan =
					containingMember.GetSpanOfAssignmentDependenciesAndDeclarationsInSpan(requestAsyncInvocationStatement.FullSpan,
						model);

				// Get catch block, and create error event handler
				// while ancestor parent is not try or member declaration, if try, check for correct catch.
				// if none found, throw
				{
					var tryCandidate = requestAsyncInvocationStatement.Ancestors().First(e => e is BlockSyntax).Parent;
					do
					{
						var tryStatement = tryCandidate as TryStatementSyntax;
						if (tryStatement != null)
						{
							var exceptionType = typeof(ReceivedErrorEventException<>);
							catchStatement = tryStatement.GetFirstCatchClauseByType(model, exceptionType, cancellationToken);
							if (catchStatement != null)
							{
								var errorType = model.GetTypeInfo(catchStatement.Declaration.Type, cancellationToken).Type as INamedTypeSymbol;
								// ReSharper disable once PossibleNullReferenceException
								if (errorEventType.ToString().Equals(errorType.TypeArguments[0].ToString()))
								{
									catchExceptionIdentifier = catchStatement.Declaration.Identifier;
									break;
								}
								catchStatement = null;
							}
						}
						tryCandidate = tryCandidate.Parent;
					} while (tryCandidate != null && !(tryCandidate is MemberDeclarationSyntax));

					if (catchStatement == null)
					{
						throw new InvalidOperationException();
					}

					errorEventHandlerMessageParameterIdentifier = GenerateUniqueParameterIdentifierForScope(errorEventType, model,
						catchStatement.Block);
					catchStatement = catchStatement.ReplaceNodes(
						catchStatement.Block.DescendantNodes().Where(e => e is ExpressionSyntax),
						(a, b) => Simplifier.Expand(a, model, document.Project.Solution.Workspace));

					var firstStatement = catchStatement.Block.Statements.FirstOrDefault();
					if (firstStatement != null)
					{
						catchStatement = catchStatement.ReplaceNode(firstStatement,
							firstStatement.WithLeadingTrivia(
								Comment("// TODO: Load message information from a repository by CorrelationId"),
								LineFeed));
					}
					else
					{
						catchStatement = catchStatement.WithBlock(catchStatement.Block.WithOpenBraceToken(
							Token(SyntaxKind.CloseBraceToken)
								.WithLeadingTrivia(Comment("// TODO: Load message information from a repository by CorrelationId"),
									LineFeed)));
					}

					foreach (var statement in catchStatement.Block.DescendantNodes(_ => true)
						.OfType<MemberAccessExpressionSyntax>()
						.Where(a =>
						{
							var i = a.Expression as IdentifierNameSyntax;
							return i != null && i.Identifier.ValueText == catchExceptionIdentifier.ValueText;
						})
						.ToArray())
					{
						catchStatement = catchStatement.ReplaceNode(statement,
							IdentifierName(errorEventHandlerMessageParameterIdentifier));
					}

					document = document.ReplaceNode(tryCandidate, tryCandidate.WithAdditionalAnnotations(tryAnnotation), out root,
						out model);
				}
				subjectNode = root.GetAnnotatedNodes(subjectNodeAnnotation).Single();
				requestAsyncInvocationStatement = subjectNode.GetAncestorStatement();
				requestAsyncAndDependantStatements = root.DescendantNodes()
					.OfType<StatementSyntax>()
					.Where(x => eventHandlerStatementsSpan.Contains(x.Span) && x != requestAsyncInvocationStatement)
					.ToArray();
				// expand in original document and add to block
				eventHandlerHandleMethodBody = Block(List(requestAsyncAndDependantStatements
					.Select(e => Simplifier.Expand(e, model, document.Project.Solution.Workspace))
					.ToArray()));
				eventHandlerHandleMethodBody = eventHandlerHandleMethodBody.ReplaceNode(eventHandlerHandleMethodBody.Statements.First(),
					eventHandlerHandleMethodBody.Statements.First().WithLeadingTrivia(
						Comment("// TODO: Load message information from a repository by CorrelationId"),
						LineFeed));
			}

			var options = document.Project.Solution.Workspace.Options;
			var namespaceIdentifierName = IdentifierName(fullContainingNamespaceName);

			// start of modifications
			ClassDeclarationSyntax eventHandlerDeclaration;
			{
#region create event handler declaration
				{
					eventHandlerDeclaration = Utilities.MessageHandlerDeclaration(
						eventType,
						generator, eventHandlerHandleMethodBody,
						handleMethodParameterName);

					var ns = (NamespaceDeclarationSyntax) generator
						.NamespaceDeclaration(namespaceIdentifierName)
						.WithAdditionalAnnotations(Formatter.Annotation);

					// create event handler document
					var filename = eventHandlerDeclaration.Identifier.ValueText + ".cs";

					// not thrilled about using text here, but some sort of disconnect between documents when
					// we get to AddImports and Reduce otherwise.
					var eventHandlerDocument = document.Project.AddDocument(filename,
						ns.AddMembers(eventHandlerDeclaration).NormalizeWhitespace().ToString());

					eventHandlerDocument = await ImportAdder.AddImportsAsync(eventHandlerDocument, options, cancellationToken);
					eventHandlerDocument = await Simplifier.ReduceAsync(eventHandlerDocument, options, cancellationToken);
					eventHandlerDocument = await Formatter.FormatAsync(eventHandlerDocument, options, cancellationToken);
					solution = eventHandlerDocument.Project.Solution.WithDocumentText(eventHandlerDocument.Id,
						await eventHandlerDocument.GetTextAsync(cancellationToken));
					document = solution.GetDocument(document.Id);
					root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
					model = await document.GetSemanticModelAsync(cancellationToken);
				}

#endregion create event handler declaration

				// replace the call to RequestAsync and dependant statements with call to Sendr 
				root = root.ReplaceNodes(requestAsyncAndDependantStatements,
					ExpressionStatement(
							InvocationExpression(
									MemberAccessExpression(
										SyntaxKind.SimpleMemberAccessExpression,
										requestAsyncMemberAccess.Expression, // "bus"
										IdentifierName(nameof(BusExtensions.Send))
											.WithAdditionalAnnotations(subjectNodeAnnotation)))
								.WithArgumentList(((InvocationExpressionSyntax) requestAsyncMemberAccess.Parent).ArgumentList))
						.WithLeadingTrivia(
							Comment("// TODO: store information about the message with CorrelationId for loading in handlers"),
							CarriageReturnLineFeed));

				// remove async and change return type
				var containingMember = (MethodDeclarationSyntax) root.GetAnnotatedNodes(containingMemberAnnotation).Single();
				root = root.ReplaceNode(containingMember, containingMember.WithoutAsync()
					.WithAdditionalAnnotations(Formatter.Annotation));
				// remove try/catch
				var @try = root.DescendantNodes(_ => true).Single(e => e.HasAnnotation(tryAnnotation));
				root = root.RemoveNode(@try, SyntaxRemoveOptions.KeepExteriorTrivia);

				document = document.WithSyntaxRoot(root);
			}

			// error event handler class:
			ClassDeclarationSyntax errorEventHandlerDeclaration;
			{
				errorEventHandlerDeclaration = Utilities.MessageHandlerDeclaration(
					errorEventType,
					generator, catchStatement.Block,
					errorEventHandlerMessageParameterIdentifier);
				var ns = (NamespaceDeclarationSyntax) generator
					.NamespaceDeclaration(namespaceIdentifierName)
					.WithAdditionalAnnotations(Formatter.Annotation);
				// create new document
				var errorEventHandlerDocument =
					document.Project.AddDocument(errorEventHandlerDeclaration.Identifier.ValueText + ".cs",
						ns.AddMembers(errorEventHandlerDeclaration).NormalizeWhitespace().ToString());
				document = errorEventHandlerDocument.Project.GetDocument(document.Id);

				errorEventHandlerDocument = await ImportAdder.AddImportsAsync(errorEventHandlerDocument, options, cancellationToken);
				errorEventHandlerDocument = await Simplifier.ReduceAsync(errorEventHandlerDocument, options, cancellationToken);
				errorEventHandlerDocument = await Formatter.FormatAsync(errorEventHandlerDocument,
					cancellationToken: cancellationToken);
				solution = errorEventHandlerDocument.Project.Solution.WithDocumentText(errorEventHandlerDocument.Id,
					await errorEventHandlerDocument.GetTextAsync(cancellationToken));
			}
			{
				document = solution.GetDocument(document.Id);
				model = await document.GetSemanticModelAsync(cancellationToken);
				root = await document
					.GetSyntaxRootAsync(cancellationToken)
					.ConfigureAwait(false);

				// go looking for a reference to c'tor of a IBus type.
				var busSend = root.GetAnnotatedNodes(subjectNodeAnnotation).Single();
				requestAsyncMemberAccess =
					busSend.Parent.AncestorsAndSelf()
						.OfType<MemberAccessExpressionSyntax>()
						.First();
				var busSymbol = model.GetTypeInfo(requestAsyncMemberAccess.Expression).Type as INamedTypeSymbol;
				Debug.Assert(busSymbol != null, "busSymbol != null");
				IMethodSymbol ctorSymbol;
				// ReSharper disable once PossibleNullReferenceException
				if (busSymbol.TypeKind == TypeKind.Interface)
				{
					var busImplementations = await SymbolFinder.FindImplementationsAsync(busSymbol, solution,
						cancellationToken: cancellationToken);
					foreach (INamedTypeSymbol impl in busImplementations.OfType<INamedTypeSymbol>())
					{
						// only implementations with public constructors
						ctorSymbol = impl.Constructors.SingleOrDefault(e => !e.IsStatic && e.Parameters.Length == 0);
						if (ctorSymbol != null)
						{
							busSymbol = impl;
							break;
						}
					}
				}
				ctorSymbol = busSymbol.Constructors.SingleOrDefault(e => !e.IsStatic && e.Parameters.Length == 0);
				var handlerSymbol = busSymbol.GetMembers(nameof(IBus.AddHandler)).Single();
				// ReSharper disable once PossibleUnintendedReferenceComparison
				if (handlerSymbol != default(ISymbol))
				{
					var references = (await SymbolFinder.FindReferencesAsync(handlerSymbol,
						solution, cancellationToken)).ToArray();
					if (references.Any(e => e.Locations.Any()))
					{
						// TODO: add AddHandlers at this location
						var definition = references
							.GroupBy(e => e.Definition)
							.OrderByDescending(g => g.Count())
							.First();
					}
					else
					{
						// no add handlers at the moment, let's just find where it was constructed.
						var locations =
							(await SymbolFinder.FindReferencesAsync(ctorSymbol, solution, cancellationToken))
							.SelectMany(e => e.Locations).ToArray();
						if (locations.Length != 0)
						{
							var referencedLocation = locations.First();
							{
								var referencedLocationDocument = referencedLocation.Document;
								var referencedRoot = await referencedLocationDocument.GetSyntaxRootAsync(cancellationToken);
								var node = referencedRoot.FindNode(referencedLocation.Location.SourceSpan);
								var statement = node.GetAncestorStatement();
								var busNode = statement.GetAssignmentToken().WithLeadingTrivia().WithTrailingTrivia();
								var busNodeName = IdentifierName(busNode);
								if (busNodeName != null)
								{
									var errorEventHandlerName =
										IdentifierName(errorEventHandlerDeclaration.Identifier);
									referencedLocationDocument = InsertAddHandlerCall(
										referencedLocationDocument,
										referencedLocation, busNodeName, errorEventHandlerName);
									var eventHandlerName =
										IdentifierName(eventHandlerDeclaration.Identifier);
									referencedLocationDocument = InsertAddHandlerCall(
										referencedLocationDocument,
										referencedLocation, busNodeName, eventHandlerName);
									solution = referencedLocationDocument.Project.Solution;
								}
							}
						}
					}
				}
				return solution;
			}
		}

		private static SyntaxToken GenerateUniqueParameterIdentifierForScope(ISymbol namedTypeSymbol, SemanticModel model, SyntaxNode block)
		{
			var baseName = $"{char.ToLower(namedTypeSymbol.Name[0])}{namedTypeSymbol.Name.Substring(1)}";
			var name = baseName;
			var symbols = model.LookupSymbols(block.SpanStart);
			int i = 1;
			while (symbols.Any(e => e.Name.Equals(name)))
			{
				name = $"{baseName}{i}";
				++i;
			}
			return Identifier(name);
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
				exceptionIdentifierName => SingletonList<StatementSyntax>(
					ExpressionStatement(
						InvocationExpression(
								MemberAccessExpression(
									SyntaxKind.SimpleMemberAccessExpression,
									MemberAccessExpression(
										SyntaxKind.SimpleMemberAccessExpression,
										MemberAccessExpression(
											SyntaxKind.SimpleMemberAccessExpression,
											AliasQualifiedName(
												IdentifierName(
													Token(SyntaxKind.GlobalKeyword)
														.WithLeadingTrivia(Comment("// TODO: do something with ex.ErrorEvent"),
															CarriageReturnLineFeed)
												),
												IdentifierName(nameof(System))),
											IdentifierName(nameof(System.Diagnostics))),
										IdentifierName(nameof(Debug))),
									IdentifierName(nameof(Debug.WriteLine))))
							.WithArgumentList(
								ArgumentList(
									SingletonSeparatedList(
										Argument(
											MemberAccessExpression(
												SyntaxKind.SimpleMemberAccessExpression,
												exceptionIdentifierName,
												IdentifierName(nameof(ReceivedErrorEventException<IEvent>.ErrorEvent)))))))));
			// Find the type declaration identified by the diagnostic.
			var subjectToken = root.FindToken(diagnostic.Location.SourceSpan.Start);
			var invocation =
				subjectToken.Parent.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();

			var subjectNode = root.FindNode(diagnostic.Location.SourceSpan);
			var requestAsyncMemberAccess =
				subjectNode.Parent.AncestorsAndSelf().OfType<MemberAccessExpressionSyntax>().First();

			// verify invocation is the one we want // rule should detect this exists
			if (!(invocation.Parent is AwaitExpressionSyntax))
			{
				throw new InvalidOperationException($"{Facts.GetCurrentMethodName()} operates on an await statement, and await statement not found.");
			}

			// assume, RequestAsync return is assigned to something (covered by other rule)
			// 1: get 3rd type parameter in invocation
			INamedTypeSymbol errorEventType, messageType, eventType;
			Utilities.GetRequestAsyncInfo(requestAsyncMemberAccess, model, out messageType, out eventType, out errorEventType);
			var generator = SyntaxGenerator.GetGenerator(document);
			var exceptionArgumentsInfo = new Dictionary<string, TypeSyntax>
			{
				{"ex", typeof(ReceivedErrorEventException<>).AsTypeSyntax(generator, errorEventType)}
			};

			return
				document.WithSyntaxRoot(Formatter.Format(
					subjectToken.GetAncestorStatement().TryCatchSafe(exceptionArgumentsInfo,
					generateCatchStatements,
					root,
					model), document.Project.Solution.Workspace, document.Project.Solution.Workspace.Options));
		}

#if NO_0100
		private static async Task<Document> InvokeMp0100(Diagnostic diagnostic, Document document, CancellationToken cancellationToken)
		{
			var diagnosticSpan = diagnostic.Location.SourceSpan;
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

			// Find the type declaration identified by the diagnostic.
			var invocation = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();
			document = await CodeAnalysisUtilities.InvokeAsync(document, invocation, cancellationToken);
			root = await document.GetSyntaxRootAsync(cancellationToken);
			var awaitExpression = root.FindNode(invocation.Span) as AwaitExpressionSyntax;
			invocation = (InvocationExpressionSyntax) awaitExpression.Expression;
			var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
			// TODO: find usages of the left identifier and remove ".Result" 
			if (invocation.Parent is EqualsValueClauseSyntax)
			{
				var symbol =
					semanticModel.GetDeclaredSymbol(((VariableDeclarationSyntax) invocation.Parent.Parent.Parent).Variables.First());
			}
			else
			{
				var assignmentExpression = invocation.Parent as AssignmentExpressionSyntax;
				Debug.Assert(assignmentExpression != null);
				// find references to assignmentExpression.Left
				var x = assignmentExpression.Left;
			}
			return document;
		}
#endif
		private static Document InsertAddHandlerCall(Document document, ReferenceLocation location, IdentifierNameSyntax busNodeName, IdentifierNameSyntax errorEventHandlerName)
		{
			var root = document.GetSyntaxRootAsync().Result;
			var generator = SyntaxGenerator.GetGenerator(document);
			var statement = root.FindNode(location.Location.SourceSpan).GetAncestorStatement();
			root = root.InsertNodesAfter(statement, new[]
			{
				ExpressionStatement(
					(ExpressionSyntax) generator.InvocationExpression(
						generator.MemberAccessExpression(
							IdentifierName(busNodeName.ToString()),
							nameof(IBus.AddHandler)),
						generator.ObjectCreationExpression(
							IdentifierName(errorEventHandlerName.ToString())))
				)
			});
			return document.WithSyntaxRoot(root);
		}

		public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var document = context.Document;

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
					var solution = context.Document.Project.Solution;
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
	}
}
