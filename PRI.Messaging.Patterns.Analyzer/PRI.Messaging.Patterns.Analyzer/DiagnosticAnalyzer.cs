using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using PRI.Messaging.Patterns.Exceptions;
using PRI.Messaging.Primitives;

namespace PRI.Messaging.Patterns.Analyzer
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class PRIMessagingPatternsAnalyzer : DiagnosticAnalyzer
	{
		public static readonly DiagnosticDescriptor RuleMp0100 = //Call to RequestAsync not awaited
			new DiagnosticDescriptor("MP0100",
				GetResourceString(nameof(Resources.Mp0100Title)),
				GetResourceString(nameof(Resources.Mp0100MessageFormat)),
				"Maintainability",
				DiagnosticSeverity.Warning,
				isEnabledByDefault: true,
				description: GetResourceString(nameof(Resources.Mp0100Description)));
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

		private static LocalizableResourceString GetResourceString(string name) 
			=> new LocalizableResourceString(name, Resources.ResourceManager, typeof(Resources));

		private ITypeSymbol _iBusSymbol;
		private static Type _type = typeof(IBus);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(RuleMp0100, RuleMp0101, RuleMp0102, RuleMp0103);

		public override void Initialize(AnalysisContext context)
		{
			// TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
			// See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
			context.RegisterSyntaxNodeAction(AnalyzeLocalDeclaration, SyntaxKind.LocalDeclarationStatement);
			context.RegisterSymbolAction(AnalyzeFieldPropertySymbol, SymbolKind.Field, SymbolKind.Property);

			context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
			context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
			context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);

		}

		private void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext obj)
		{
			var local = obj.Node as LocalDeclarationStatementSyntax;
			Debug.Assert(local != null, "local != null");
			var type = local.Declaration.Type;
			var symbol = obj.SemanticModel.GetSymbolInfo(type).Symbol;
			if (symbol == null) return;
		}

		private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext analysisContext)
		{
			AnalyzeMethodDeclaration(analysisContext, analysisContext.CancellationToken);
		}

		private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext analysisContext,
			CancellationToken cancellationToken)
		{
			var methodDeclarationSyntax = analysisContext.Node as MethodDeclarationSyntax;
			Debug.Assert(methodDeclarationSyntax != null, "methodDeclarationSyntax != null");
			foreach (
				var invocationSyntax in methodDeclarationSyntax.DescendantNodes(_ => true).OfType<InvocationExpressionSyntax>())
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
				var semanticModel = analysisContext.SemanticModel;
				var invokedIdentifierTypeInfo = semanticModel.GetTypeInfo(invokedIdentifierName, cancellationToken).Type;
				if (invokedIdentifierTypeInfo == null || invokedIdentifierTypeInfo.TypeKind == TypeKind.Error)
				{
					continue;
				}
				if (!ImplementsIBus(invokedIdentifierTypeInfo))
				{
					continue;
				}

				var methodExpression = descendantExpressionNodes.ElementAt(1);
				var methodSymbolInfo = semanticModel.GetSymbolInfo(methodExpression, cancellationToken).Symbol as IMethodSymbol;
				if (methodSymbolInfo == null)
				{
					return;
				}

				if (CodeAnalysisExtensions.IsRequestAsync(methodSymbolInfo))
				{
					if (!(invocationSyntax.Parent is AwaitExpressionSyntax))
					{
						var diagnostic = Diagnostic.Create(RuleMp0100,
							methodExpression.GetLocation());

						analysisContext.ReportDiagnostic(diagnostic);
					}
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
					var genericNameSyntax = (GenericNameSyntax) invocationSyntaxExpression.Name; //.TypeArgumentList
					if (parentTryStatement == null)
					{
						if (genericNameSyntax.TypeArgumentList.Arguments.Count > 2)
						{
							var diagnostic = Diagnostic.Create(RuleMp0101,
								methodExpression.GetLocation());

							analysisContext.ReportDiagnostic(diagnostic);
						}
					}
					else
					{
						// get catch type
						var exceptionType = typeof(ReceivedErrorEventException<>);
						var firstRightCatch = parentTryStatement.GetFirstCatchClauseByType(semanticModel, exceptionType, cancellationToken);
						if (firstRightCatch == null)
						{
							var diagnostic = Diagnostic.Create(RuleMp0101,
								methodExpression.GetLocation());

							analysisContext.ReportDiagnostic(diagnostic);
						}
						else
						{
							var errorType = semanticModel.GetTypeInfo(firstRightCatch.Declaration.Type, cancellationToken).Type as INamedTypeSymbol;
							if (errorType == null)
							{
								// TODO: diagnose?
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
									Debug.WriteLine("arguments are not right");
									// TODO: diagnose
								}
								else
								{
									var messageType =
										semanticModel.GetTypeInfo(genericNameSyntax.TypeArgumentList.Arguments[0], cancellationToken).Type;
									if (ImplementsInterface<IEvent>(messageType))
									{
										var diagnostic = Diagnostic.Create(RuleMp0103,
											methodExpression.GetLocation());

										analysisContext.ReportDiagnostic(diagnostic);
									}
									else
									{
										var diagnostic = Diagnostic.Create(RuleMp0102,
											methodExpression.GetLocation());

										analysisContext.ReportDiagnostic(diagnostic);
									}
								}
							}
						}
					}

					// TODO: check to make sure there's an exception block catching ReceivedErrorEventException<TErrorEvent> or higher
					// TODO: offer to break out the continuation into a success event handler the exception block into an error event handler
					// hooking them up where other bus.AddHandlers are called and replace RequestAsync with Send
					// with TODOs to verify storage of state and retrieval of state
				}
			}
		}

		private bool ImplementsInterface<TInterface>(ITypeSymbol typeSymbol)
		{
			var type = typeof(TInterface);
			if (!type.GetTypeInfo().IsInterface)
			{
				throw new ArgumentException("Type is not an interface", nameof(typeSymbol));
			}
			var typeCandidateSymbol = typeSymbol.Interfaces
				.SingleOrDefault(e => e.ToString() == type.FullName);
			if (typeCandidateSymbol == null)
			{
				return false;
			}
			return $"{typeCandidateSymbol}, {typeCandidateSymbol.ContainingAssembly.Identity}" == type.AssemblyQualifiedName;
		}

		private bool ImplementsIBus(ITypeSymbol invokedIdentifierTypeInfo)
		{
			var type = _type;
			if (_iBusSymbol != null)
			{
				return invokedIdentifierTypeInfo.Interfaces.Contains(_iBusSymbol);
			}
			var iBusCandidateSymbol = invokedIdentifierTypeInfo.Interfaces
				.SingleOrDefault(e => e.ToString() == type.FullName);
			if (iBusCandidateSymbol == null)
			{
				return false;
			}
			if($"{iBusCandidateSymbol}, {iBusCandidateSymbol.ContainingAssembly.Identity}" == _type.AssemblyQualifiedName)
			{
				_iBusSymbol = iBusCandidateSymbol;
				return true;
			}
			return false;
		}

		private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext obj)
		{
			// TODO:
		}

		private void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext obj)
		{
			// TODO: Check initializer?
		}

		private void AnalyzeFieldPropertySymbol(SymbolAnalysisContext context)
		{
			var fieldSymbol = context.Symbol as IFieldSymbol;
			ITypeSymbol type;
			if (fieldSymbol == null)
			{
				var propertySymbol = context.Symbol as IPropertySymbol;
				if (propertySymbol == null)
				{
					return;
				}
				type = propertySymbol.Type;
			}
			else
			{
				type = fieldSymbol.Type;
			}

			if (type.AllInterfaces.Any(i => i.Name == _type.Name))
			{
				_iBusSymbol = type;
			}
		}
	}
}
