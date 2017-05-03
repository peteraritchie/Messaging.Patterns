using System;
using System.Collections.Concurrent;
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
using PRI.Messaging.Patterns.Analyzer.Utility;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Patterns.Extensions.Consumer;
using PRI.Messaging.Primitives;

namespace PRI.Messaging.Patterns.Analyzer
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class PRIMessagingPatternsAnalyzer : DiagnosticAnalyzer
	{
		#region descriptors
#if SUPPORT_MP0100
		public static readonly DiagnosticDescriptor RuleMp0100 = //Call to RequestAsync not awaited
			new DiagnosticDescriptor("MP0100",
				GetResourceString(nameof(Resources.Mp0100Title)),
				GetResourceString(nameof(Resources.Mp0100MessageFormat)),
				"Maintainability",
				DiagnosticSeverity.Warning,
				isEnabledByDefault: true,
				description: GetResourceString(nameof(Resources.Mp0100Description)));
#endif // SUPPORT_MP0100
		public static readonly DiagnosticDescriptor RuleMp0101 = // Call to RequestAsync with error event but no try.catch.
			new DiagnosticDescriptor("MP0101",
				GetResourceString(nameof(Resources.Mp0101Title)),
				GetResourceString(nameof(Resources.Mp0101MessageFormat)),
				"Maintainability",
				DiagnosticSeverity.Error,
				isEnabledByDefault: true,
				description: GetResourceString(nameof(Resources.Mp0101Description)));

		public static readonly DiagnosticDescriptor RuleMp0102 = // consider not using RequestAsync
			new DiagnosticDescriptor("MP0102",
				GetResourceString(nameof(Resources.Mp0102Title)),
				GetResourceString(nameof(Resources.Mp0102MessageFormat)),
				"Maintainability",
				DiagnosticSeverity.Warning,
				isEnabledByDefault: true,
				description: GetResourceString(nameof(Resources.Mp0102Description)));

		public static readonly DiagnosticDescriptor RuleMp0103 =
			new DiagnosticDescriptor("MP0103",
				GetResourceString(nameof(Resources.Mp0103Title)),
				GetResourceString(nameof(Resources.Mp0103MessageFormat)),
				"Maintainability",
				DiagnosticSeverity.Warning,
				isEnabledByDefault: true,
				description: GetResourceString(nameof(Resources.Mp0103Description)));

		public static readonly DiagnosticDescriptor RuleMp0104 =
			new DiagnosticDescriptor("MP0104", // Message has no handler
				GetResourceString(nameof(Resources.Mp0104Title)),
				GetResourceString(nameof(Resources.Mp0104MessageFormat)),
				"Maintainability",
				DiagnosticSeverity.Warning,
				isEnabledByDefault: true,
				description: GetResourceString(nameof(Resources.Mp0104Description)));

		public static readonly DiagnosticDescriptor RuleMp0110 =
			new DiagnosticDescriptor("MP0110",
				GetResourceString(nameof(Resources.Mp0110Title)),
				GetResourceString(nameof(Resources.Mp0110MessageFormat)),
				"Naming",
				DiagnosticSeverity.Warning,
				isEnabledByDefault: true,
				description: GetResourceString(nameof(Resources.Mp0110Description)));

		public static readonly DiagnosticDescriptor RuleMp0111 =
			new DiagnosticDescriptor("MP0111",
				GetResourceString(nameof(Resources.Mp0111Title)),
				GetResourceString(nameof(Resources.Mp0111MessageFormat)),
				"Naming",
				DiagnosticSeverity.Warning,
				isEnabledByDefault: true,
				description: GetResourceString(nameof(Resources.Mp0111Description)));

		public static readonly DiagnosticDescriptor RuleMp0112 =
			new DiagnosticDescriptor("MP0112",
				GetResourceString(nameof(Resources.Mp0112Title)),
				GetResourceString(nameof(Resources.Mp0112MessageFormat)),
				"Maintainability",
				DiagnosticSeverity.Error,
				isEnabledByDefault: true,
				description: GetResourceString(nameof(Resources.Mp0112Description)));

		public static readonly DiagnosticDescriptor RuleMp0113 =
			new DiagnosticDescriptor("MP0113",
				GetResourceString(nameof(Resources.Mp0113Title)),
				GetResourceString(nameof(Resources.Mp0113MessageFormat)),
				"Naming",
				DiagnosticSeverity.Warning,
				isEnabledByDefault: true,
				description: GetResourceString(nameof(Resources.Mp0113Description)));

		public static readonly DiagnosticDescriptor RuleMp0114 =
			new DiagnosticDescriptor("MP0114",
				GetResourceString(nameof(Resources.Mp0114Title)),
				GetResourceString(nameof(Resources.Mp0114MessageFormat)),
				"Maintainability",
				DiagnosticSeverity.Warning,
				isEnabledByDefault: true,
				description: GetResourceString(nameof(Resources.Mp0114Description)));

		/// <summary>
		/// Command handlers should publish events describing state changes.
		/// </summary>
		public static readonly DiagnosticDescriptor RuleMp0115 =
			new DiagnosticDescriptor("MP0115",
				GetResourceString(nameof(Resources.Mp0115Title)),
				GetResourceString(nameof(Resources.Mp0115MessageFormat)),
				"Maintainability",
				DiagnosticSeverity.Warning,
				isEnabledByDefault: true,
				description: GetResourceString(nameof(Resources.Mp0115Description)));

		public static readonly DiagnosticDescriptor RuleMp0116 =
			new DiagnosticDescriptor("MP0116",
				GetResourceString(nameof(Resources.Mp0116Title)),
				GetResourceString(nameof(Resources.Mp0116MessageFormat)),
				"Maintainability",
				DiagnosticSeverity.Warning,
				isEnabledByDefault: true,
				description: GetResourceString(nameof(Resources.Mp0116Description)));

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(
#if SUPPORT_MP0100
				RuleMp0100, 
#endif
				RuleMp0101, RuleMp0102, RuleMp0103,
				RuleMp0104, RuleMp0110, RuleMp0111, RuleMp0112, RuleMp0113, RuleMp0114,
				RuleMp0115, RuleMp0116);
#endregion descriptors

		private static LocalizableResourceString GetResourceString(string name)
			=> new LocalizableResourceString(name, Resources.ResourceManager, typeof(Resources));

		private readonly ConcurrentDictionary<ISymbol, List<SyntaxNode>> _symbolUsage =
			new ConcurrentDictionary<ISymbol, List<SyntaxNode>>(concurrencyLevel: 2, capacity: 100);

#if DEBUG
		private static string NotText(bool value)
		{
			return value ? "" : "not ";
		}
#endif
		private static readonly MethodInfo BusPublishMethodInfo =
			typeof(BusExtensions).GetRuntimeMethods().Single(e1 => e1.Name == nameof(BusExtensions.Publish));
		private static readonly MethodInfo ConsumerPublishMethodInfo =
			typeof(BusExtensions).GetRuntimeMethods().Single(e1 => e1.Name == nameof(ConsumerExtensions.Publish));				
		private static readonly MethodInfo ConsumerHandleMethodInfo =
			typeof(IConsumer<>).GetRuntimeMethods().Single(e1 => e1.Name == nameof(IConsumer<IEvent>.Handle));				
		private static readonly MethodInfo SendMethodInfo =
			typeof(BusExtensions).GetRuntimeMethods().Single(e1 => e1.Name == nameof(BusExtensions.Send));
		private static readonly MethodInfo[] RequestAsyncMethodInfos =
			typeof(BusExtensions).GetRuntimeMethods()
			.Where(e1 => e1.Name == nameof(BusExtensions.RequestAsync))
			.ToArray();
		private static readonly MethodInfo HandleMethodInfo =
			typeof(IConsumer<>).GetRuntimeMethods().Single(e1 => e1.Name == nameof(IBus.Handle));

		public override void Initialize(AnalysisContext context)
		{
			// TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
			// See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information

			context.RegisterCompilationStartAction(AnalyzeCompilationStart);
			context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
			//context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
			//context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
			//context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);

			context.RegisterCompilationAction(analysisContext =>
			{
				try
				{
					foreach (var key in _symbolUsage.Keys)
					{
						var typeSymbol = key as ITypeSymbol;
						if (typeSymbol != null && typeSymbol.ImplementsInterface<IMessage>())
						{
							var compilation = analysisContext.Compilation;
							if (!_symbolUsage[key].Any(
								e =>
								{
									var semanticModel = compilation.GetSemanticModel(e.SyntaxTree);
									return e.IsGenericOfType(typeof(IConsumer<>), semanticModel) ||
										   e.IsTypeArgumentToMethod(semanticModel, analysisContext.CancellationToken,
											   RequestAsyncMethodInfos) ||
										   e.IsArgumentToMethod(semanticModel, analysisContext.CancellationToken,
											   BusPublishMethodInfo) ||
										   e.IsArgumentToMethod(semanticModel, analysisContext.CancellationToken,
											   RequestAsyncMethodInfos) ||
										   e.IsArgumentToMethod(semanticModel, analysisContext.CancellationToken,
											   HandleMethodInfo) ||
										   e.IsArgumentToMethod(semanticModel, analysisContext.CancellationToken,
											   SendMethodInfo);
								}))
							{
								analysisContext.ReportDiagnostic(typeSymbol.CreateDiagnostic(RuleMp0114, key.ToString()));
							}
						}
					}

				}
				catch (Exception ex)
				{
					Debug.WriteLine($"{ex.GetType().Name} : {ex.Message}");
				}
			});
			context.RegisterSyntaxNodeAction(analysisContext =>
			{
				var info = analysisContext.SemanticModel.GetSymbolInfo(analysisContext.Node, analysisContext.CancellationToken);
				var symbol = info.Symbol;
				if (info.Symbol == null)
				{
					if (info.CandidateReason == CandidateReason.None)
					{
						return;
					}
					var suitableCandidateSymbols = info.CandidateSymbols
						.Where(e =>
								(e.Kind == SymbolKind.Local || e.Kind == SymbolKind.NamedType || e.Kind != SymbolKind.Namespace) &&
								e.OriginalDefinition != null && e.Locations[0].IsInSource)
						.ToArray();
					if (!suitableCandidateSymbols.Any())
					{
						return;
					}
					symbol = suitableCandidateSymbols.First();
				}
				if (symbol?.Kind == SymbolKind.Local)
					symbol = analysisContext.SemanticModel.GetTypeInfo(analysisContext.Node).Type;
				if (symbol?.Kind == SymbolKind.Namespace || symbol?.OriginalDefinition == null || symbol?.OriginalDefinition?.Kind != SymbolKind.NamedType)
				{
					// Avoid getting Locations for namespaces. That can be very expensive.
					return;
				}
				var inSource = symbol?.OriginalDefinition?.Locations[0].IsInSource == true;
				if (!inSource)
				{
					return;
				}
				var hasLocations = symbol?.OriginalDefinition?.Locations.Length > 0;
				if (!hasLocations)
				{
					return;
				}

				_symbolUsage.AddOrUpdate(symbol.OriginalDefinition, new List<SyntaxNode> {analysisContext.Node.Parent}, (existingSymbol, existingValue) =>
				{
					existingValue.Add(analysisContext.Node.Parent);
					return existingValue;
				});
			}, SyntaxKind.IdentifierName, SyntaxKind.GenericName);
			context.RegisterSyntaxNodeAction(analysisContext =>
			{
				var symbol = analysisContext.SemanticModel.GetDeclaredSymbol(analysisContext.Node);
				if (symbol?.Kind == SymbolKind.Namespace)
				{
					// Avoid getting Locations for namespaces. That can be very expensive.
					return;
				}
				_symbolUsage.TryAdd(symbol, new List<SyntaxNode> { /*analysisContext.Node.Parent*/ });
#if DEBUG1
				var declaration = analysisContext.Node as TypeDeclarationSyntax;
				Debug.Assert(declaration != null);
				Debug.WriteLine($"==> {declaration.Keyword} {declaration.Identifier.Text} declared using symbol {symbol.Name}");
#endif
			}, SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
		}

		private void AnalyzeCompilationStart(CompilationStartAnalysisContext compilationContext)
		{
			var compilation = compilationContext.Compilation;
			compilationContext.RegisterSyntaxTreeAction(context =>
			{
				if (!compilation.SyntaxTrees.Contains(context.Tree))
					return;
				var semanticModel = compilation.GetSemanticModel(context.Tree);
				var root = context.Tree.GetRoot(context.CancellationToken);
				var model = compilationContext.Compilation.GetSemanticModel(context.Tree);
				if (model.IsFromGeneratedCode(compilationContext.CancellationToken))
					return;
				var visitor = new MessagePatternsSyntaxWalker(semanticModel, context);
				visitor.Visit(root);
			});
			compilationContext.RegisterSymbolAction(context =>
			{
				var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
				if (namedTypeSymbol.IsNamespace)
				{
					return;
				}

				CheckNaming(namedTypeSymbol, context);
			}, SymbolKind.NamedType);
		}

		private string[] validEventSuffixes_en = { "Event", "Response" };
		private string[] validMessageSuffixes_en = { "Message", "Request", Utilities.CommandMessageClassSuffix };

		private void CheckNaming(INamedTypeSymbol namedTypeSymbol, SymbolAnalysisContext context)
		{
			var name = namedTypeSymbol.Name;
			if (string.IsNullOrWhiteSpace(name))
			{
				return;
			}
			if (namedTypeSymbol.ImplementsInterface<IEvent>())
			{
				if (!validEventSuffixes_en.Any(e => name.EndsWith(e, StringComparison.Ordinal)))
				{
					context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(RuleMp0110, name));
				}
			}
			// IEvent derives from IMessage, so don't check if we've checked IEvent
			else if (namedTypeSymbol.ImplementsInterface<IMessage>())
			{
				if (!validMessageSuffixes_en.Any(e => name.EndsWith(e, StringComparison.Ordinal)))
				{
					context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(RuleMp0110, name));
				}
			}
			if(namedTypeSymbol.ImplementsInterface(typeof(IConsumer<>)))
			{
				if (!name.EndsWith(Utilities.HandlerClassSuffix, StringComparison.Ordinal))
				{
					context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(RuleMp0111, name));
				}
				else
				{
					foreach (var @interface in namedTypeSymbol.AllInterfaces.Where(e => e.IsGenericType && e.TypeParameters.Length == 1 && e.Name.StartsWith("IConsumer", StringComparison.Ordinal)))
					{
						var suggestedName = $"{@interface.TypeArguments.First().Name}Handler";
						if (!name.Equals(suggestedName, StringComparison.Ordinal))
						{
							context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(RuleMp0113, name, suggestedName));
						}
					}
				}
			}
		}

		private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext analysisContext)
		{
			AnalyzeMethodDeclaration(analysisContext, analysisContext.CancellationToken);
		}

		private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext analysisContext,
			CancellationToken cancellationToken)
		{
			try
			{
				var methodDeclarationSyntax = analysisContext.Node as MethodDeclarationSyntax;
				Debug.Assert(methodDeclarationSyntax != null, "methodDeclarationSyntax != null");

				var model = analysisContext.SemanticModel;
				foreach ( // TODO: re-use the invocation list below
					var invocationSyntax in methodDeclarationSyntax.DescendantNodes(_ => true)
					.OfType<InvocationExpressionSyntax>())
				{
					var invocationSyntaxExpression = invocationSyntax.Expression as MemberAccessExpressionSyntax;
					if (invocationSyntaxExpression == null)
					{
						continue;
					}
					var descendantExpressionNodes = invocationSyntaxExpression.DescendantNodes().ToArray();

					// assumes identifier in use is the first element (IBus), and the method invoked on it
					// is the second
					if (descendantExpressionNodes.Length < 2)
					{
						continue;
					}
					var invokedIdentifierName = descendantExpressionNodes.ElementAt(0) as IdentifierNameSyntax;
					if (invokedIdentifierName == null)
					{
						continue;
					}
					var invokedIdentifierTypeInfo = model.GetTypeInfo(invokedIdentifierName, cancellationToken).Type;
					if (invokedIdentifierTypeInfo == null || invokedIdentifierTypeInfo.TypeKind == TypeKind.Error)
					{
						continue;
					}
					if (!invokedIdentifierTypeInfo.ImplementsInterface<IBus>())
					{
						continue;
					}

					var methodExpression = descendantExpressionNodes.ElementAt(1);
					var methodSymbolInfo = model.GetSymbolInfo(methodExpression, cancellationToken).Symbol as IMethodSymbol;
					if (methodSymbolInfo == null)
					{
						return;
					}

					if (Utilities.IsRequestAsync(methodSymbolInfo))
					{
						if (AnalyzeRequestAsyncInvocation(analysisContext, cancellationToken, invocationSyntax, methodExpression,
							invocationSyntaxExpression, model))
						{
							return;
						}
					}
				}

				var methodSymbol = model.GetDeclaredSymbol(methodDeclarationSyntax);
				if (methodSymbol == null || !methodSymbol.IsImplementationOf(HandleMethodInfo) ||
				    !methodSymbol.ContainingType.IsCommandMessageType())
				{
					return;
				}

				IEnumerable<ArgumentSyntax> messageArgs;
				string sentText;
				{
					var invocationsInMethod = methodDeclarationSyntax.Invocations().ToArray();
					var busPublishInvocations = invocationsInMethod
						.Where(e => e.IsInvocationOfMethod(BusPublishMethodInfo,
							model,
							analysisContext.CancellationToken))
						.ToArray();
					var consumerPublishInvocations = invocationsInMethod
						.Where(e =>
							e.IsInvocationOfMethod(ConsumerPublishMethodInfo, model,
								analysisContext.CancellationToken))
						.ToArray();
					var consumerHandleOfEventInvocations = invocationsInMethod
						.Where(e =>
							e.IsInvocationOfMethod(ConsumerHandleMethodInfo, model,
								analysisContext.CancellationToken) && e.Parent != null
							&& model.GetSymbolInfo(e).Symbol.ContainingType.IsGenericType
							&& model.GetSymbolInfo(e)
								.Symbol.ContainingType.TypeArguments.Any(t => t.ImplementsInterface<IEvent>()))
						.ToArray();

					if (!busPublishInvocations.Any()
					    && !consumerPublishInvocations.Any()
					    && !consumerHandleOfEventInvocations.Any())
					{
						analysisContext.ReportDiagnostic(methodDeclarationSyntax.CreateDiagnostic(RuleMp0115,
							((TypeDeclarationSyntax) methodDeclarationSyntax.Parent).GetIdentifier().ToString()));
					}
					messageArgs = consumerHandleOfEventInvocations
						.SelectMany(e => e.ArgumentList.Arguments)
						.Concat(busPublishInvocations
							.SelectMany(e => e.ArgumentList.Arguments)
							.Concat(consumerPublishInvocations
								.SelectMany(e => e.ArgumentList.Arguments)));
					sentText = consumerHandleOfEventInvocations.Concat(busPublishInvocations)
						.Concat(consumerPublishInvocations)
						.Select(e => e.MemberName().ToString())
						.First();
				}

				// if any of the message args do not have CorrelationId assigned from message.CorrelationId
				// then diagnose.
				var assignmentsInMethod = methodDeclarationSyntax
					.Assignments()
					.Where(e=>e.Right is MemberAccessExpressionSyntax).ToArray();

				bool correlationIdCopied = false;
				foreach (var arg in messageArgs)
				{
					var argSymbol = model.GetSymbolInfo((IdentifierNameSyntax) arg.Expression).Symbol;
					ITypeSymbol argType = null;
					if (argSymbol != null)
					{
						argType = argSymbol.GetTypeSymbol();
					}
					if (argType == null)
					{
						continue;
					}
					var members = model.LookupSymbols(0, argType);
					var correlationIdSymbol = members.FirstOrDefault(e => e.Name == nameof(IMessage.CorrelationId));
					if (!(from assignment
						in assignmentsInMethod
						where assignment.GetAssignedSymbol(model).Equals(correlationIdSymbol)
						let memberAccessExpression = assignment.Right as MemberAccessExpressionSyntax
						let assignedValueSymbol = assignment.GetAssignedValue(model)
						let assignedValueObjectSymbol = model.GetSymbolInfo(memberAccessExpression.Expression).Symbol
						where assignedValueSymbol.Name.Equals(correlationIdSymbol.Name)
						      && assignedValueObjectSymbol.Equals(methodSymbol.Parameters[0])
						select assignedValueSymbol).Any())
					{
						continue;
					}
					correlationIdCopied = true;
					break;
				}

				if(!correlationIdCopied)
				{
					analysisContext.ReportDiagnostic(methodDeclarationSyntax.CreateDiagnostic(RuleMp0116,
						((TypeDeclarationSyntax)methodDeclarationSyntax.Parent).GetIdentifier().ToString(),
						SimplePluralize(sentText)));
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}
		}

		private string SimplePluralize(string text)
		{
			return string.Concat(text, text.EndsWith("e", StringComparison.OrdinalIgnoreCase) 
				? "s"
				: "es");
		}

		private static IEnumerable<T2> Traverse<T1, T2>(T1 obj, Func<T1, T1> next, Func<T1, T2> value) where T1 : class
		{
			while (obj != null)
			{
				yield return value(obj);
				obj = next(obj);
			}
		}

		// TODO: move GetAssignedSymbol to helper/extension
		/// <summary>
		/// get assignee
		/// </summary>
		/// <param name="assignment"></param>
		/// <param name="model"></param>
		private static ISymbol GetAssignedSymbolx(AssignmentExpressionSyntax assignment, SemanticModel model)
		{
			while (assignment != null && assignment.Kind() == SyntaxKind.SimpleAssignmentExpression)
			{
				if (assignment.Kind() == SyntaxKind.SimpleAssignmentExpression)
				{
					if (assignment.Parent is InitializerExpressionSyntax &&
					    assignment.Parent.Kind() == SyntaxKind.ObjectInitializerExpression)
					{
						var oce = assignment.Parent.Parent as ObjectCreationExpressionSyntax;
						return GetAssignedSymbol(oce, model);
					}

					var mae = assignment.Left as MemberAccessExpressionSyntax;
					if (mae != null && mae.Kind() == SyntaxKind.SimpleMemberAccessExpression)
					{
						var identifierName = mae.Expression as IdentifierNameSyntax;
						return model.GetSymbolInfo(identifierName).Symbol;
					}
				}

				assignment = assignment.Parent as AssignmentExpressionSyntax;
			}
			return default(ISymbol);
		}

		// TODO: move GetAssignedSymbol to helper/extension
		private static ISymbol GetAssignedSymbol(ObjectCreationExpressionSyntax objectCreationExpression, SemanticModel model)
		{
			var evc = objectCreationExpression?.Parent as EqualsValueClauseSyntax;
			if (evc != null)
			{
				var vd = (VariableDeclaratorSyntax) evc.Parent;
				if (vd != null)
				{
					return model.GetDeclaredSymbol(vd);
				}
			}
			else
			{
				var ae = objectCreationExpression?.Parent as AssignmentExpressionSyntax;
				if (ae != null)
				{
					return model.GetSymbolInfo(ae.Left).Symbol;
				}
			}
			return default(ISymbol);
		}

		private static bool AnalyzeRequestAsyncInvocation(SyntaxNodeAnalysisContext analysisContext,
			CancellationToken cancellationToken, InvocationExpressionSyntax invocationSyntax, SyntaxNode methodExpression,
			MemberAccessExpressionSyntax invocationSyntaxExpression, SemanticModel semanticModel)
		{
#if SUPPORT_MP0100
			if (!(invocationSyntax.Parent is AwaitExpressionSyntax))
			{
				// if assigning to variable
				if (invocationSyntax.Parent is EqualsValueClauseSyntax)
				{
					if (((VariableDeclarationSyntax) invocationSyntax.Parent.Parent.Parent).Type
						.IsVar)
					{
						var diagnostic = Diagnostic.Create(RuleMp0100,
							methodExpression.GetLocation());
						// else
						analysisContext.ReportDiagnostic(diagnostic);
						return true;
					} // TODO: else with non-fixable diagnostic
				}
				else
				{
					var assignmentExpression = invocationSyntax.Parent as AssignmentExpressionSyntax;
					if (assignmentExpression != null && ((TypeSyntax) assignmentExpression.Left).IsVar)
					{
						var diagnostic = Diagnostic.Create(RuleMp0100,
							methodExpression.GetLocation());

						analysisContext.ReportDiagnostic(diagnostic);
						return true;
					} // TODO: else with non-fixable diagnostic
				}
			}
#endif
			var parent = invocationSyntax.Parent;
			TryStatementSyntax parentTryStatement = null;
			while (!(parent is MethodDeclarationSyntax))
			{
				if (parent is BlockSyntax)
				{
					parentTryStatement = parent.Parent as TryStatementSyntax;
					if (parentTryStatement != null)
					{
						break;
					}
				}
				parent = parent.Parent;
			}
			var genericNameSyntax = (GenericNameSyntax) invocationSyntaxExpression.Name;
			if (parentTryStatement == null)
			{
				if (genericNameSyntax.TypeArgumentList.Arguments.Count > 2)
				{
					var diagnostic = Diagnostic.Create(RuleMp0101,
						methodExpression.GetLocation());

					analysisContext.ReportDiagnostic(diagnostic);
					return true;
				}
			}
			else
			{
				// get catch type
				var exceptionType = typeof(ReceivedErrorEventException<>);
				var firstRightCatch = parentTryStatement.GetFirstCatchClauseByType(semanticModel, exceptionType,
					cancellationToken);
				if (firstRightCatch == null)
				{
					var diagnostic = Diagnostic.Create(RuleMp0101,
						methodExpression.GetLocation());

					analysisContext.ReportDiagnostic(diagnostic);
					return true;
				}
				else
				{
					var errorType =
						semanticModel.GetTypeInfo(firstRightCatch.Declaration.Type, cancellationToken).Type as INamedTypeSymbol;
					if (errorType == null)
					{
						var diagnostic = Diagnostic.Create(RuleMp0101,
							methodExpression.GetLocation());

						analysisContext.ReportDiagnostic(diagnostic);
					}
					else
					{
						var catchErrorEventType = errorType.TypeArguments[0];
						var argumentErrorEventType =
							semanticModel.GetTypeInfo(genericNameSyntax.TypeArgumentList.Arguments[2], cancellationToken).Type;
						// check that the type parameter to the error type is the same as the error
						// event parameter in the RequestAsync invocation
						if (!argumentErrorEventType.Equals(catchErrorEventType))
						{
							var diagnostic = Diagnostic.Create(RuleMp0101,
								methodExpression.GetLocation());

							analysisContext.ReportDiagnostic(diagnostic);
						}
						else
						{
							var messageType =
								semanticModel.GetTypeInfo(genericNameSyntax.TypeArgumentList.Arguments[0], cancellationToken).Type;
							if (!messageType.ImplementsInterface<IEvent>())
							{
								var diagnostic = Diagnostic.Create(RuleMp0102,
									methodExpression.GetLocation());

								analysisContext.ReportDiagnostic(diagnostic);
							}
						}
					}
				}
			}
			return false;
		}

		public class MessagePatternsSyntaxWalker : CSharpSyntaxWalker
		{

			private readonly SemanticModel _semanticModel;
			private readonly SyntaxTreeAnalysisContext _context;

			public MessagePatternsSyntaxWalker(SemanticModel semanticModel, SyntaxTreeAnalysisContext context)
			{
				_semanticModel = semanticModel;
				_context = context;
			}

#if false
			public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
			{
				var methodSymbol = _semanticModel.GetDeclaredSymbol(node);
				bool isHandleImplementation = methodSymbol.ContainingType
					.AllInterfaces
					 .SelectMany(@interface => @interface.GetMembers().OfType<IMethodSymbol>())
					 .Any(method => methodSymbol.Equals(methodSymbol.ContainingType.FindImplementationForInterfaceMember(method)));
				if(!isHandleImplementation)
				{
					base.VisitMethodDeclaration(node);
				}
				var methodCalls = node.DescendantNodes().OfType<InvocationExpressionSyntax>()
					.Where(IsInterestingInvocation).ToArray();
				Debug.WriteLine(isHandleImplementation);
			}

			private bool IsInterestingInvocation(InvocationExpressionSyntax e)
			{
				return e.IsInvocationOfPublish(_semanticModel, _context.CancellationToken) || e.IsInvocationOfHandle(_semanticModel, _context.CancellationToken) || e.IsInvocationOfSend(_semanticModel, _context.CancellationToken);
			}
#endif

			public override void VisitInvocationExpression(InvocationExpressionSyntax node)
			{
				var invocationSyntaxExpression = node.Expression as MemberAccessExpressionSyntax;
				if (invocationSyntaxExpression == null)
				{
					base.VisitInvocationExpression(node);
					return;
				}
				var childNodes = invocationSyntaxExpression.ChildNodes().ToArray();
				// assumes identifier in use is the first element (IBus), and the method invoked on it
				// is the second
				if (childNodes.Length < 2)
				{
					base.VisitInvocationExpression(node);
					return;
				}
				var invokedIdentifierName = childNodes.ElementAt(0) as IdentifierNameSyntax;
				if (invokedIdentifierName == null)
				{
					base.VisitInvocationExpression(node);
					return;
				}
				var invokedIdentifierTypeInfo = _semanticModel.GetTypeInfo(invokedIdentifierName, _context.CancellationToken).Type;
				if (invokedIdentifierTypeInfo == null || invokedIdentifierTypeInfo.TypeKind == TypeKind.Error)
				{
					base.VisitInvocationExpression(node);
					return;
				}

				if (!invokedIdentifierTypeInfo.ImplementsInterface<IBus>())
				{
					base.VisitInvocationExpression(node);
					return;
				}
				
				VisitIBusInvocationExpression(node, childNodes);

				base.VisitInvocationExpression(node);
			}

			private void VisitIBusInvocationExpression(InvocationExpressionSyntax node, SyntaxNode[] childNodes)
			{
				if (node.IsInvocationOfSend(_semanticModel, _context.CancellationToken))
				{
					var argumentExpression = node.ArgumentList.Arguments[0].Expression;
					var typeInfo = _semanticModel.GetTypeInfo(argumentExpression);
					if (typeInfo.Type != null && typeInfo.Type.ImplementsInterface<IEvent>())
					{
						_context.ReportDiagnostic(node.CreateDiagnostic(RuleMp0112, argumentExpression.ToString()));
					}
				}
				else if (node.IsInvocationOfMethod(RequestAsyncMethodInfos, _semanticModel, _context.CancellationToken))
				{
					var argumentExpression = node.ArgumentList.Arguments[0].Expression;
					var typeInfo = _semanticModel.GetTypeInfo(argumentExpression);
					if (typeInfo.Type != null && typeInfo.Type.ImplementsInterface<IEvent>())
					{
						_context.ReportDiagnostic(node.CreateDiagnostic(RuleMp0103, argumentExpression.ToString()));
					}
				}
			}
		}

		//private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext obj)
		//{
		//	// TODO:
		//}

		//private void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext obj)
		//{
		//	// TODO: Check initializer?
		//}
	}
}



