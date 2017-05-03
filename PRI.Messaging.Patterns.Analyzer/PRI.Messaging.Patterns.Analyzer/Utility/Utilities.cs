using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using PRI.Messaging.Primitives;

namespace PRI.Messaging.Patterns.Analyzer.Utility
{
	internal static class Utilities
	{
		public static readonly string HandlerClassSuffix = "Handler";
		public static readonly string CommandMessageClassSuffix = "Command";

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

		public static ClassDeclarationSyntax MessageHandlerDeclaration(INamedTypeSymbol namedMessageTypeSymbol,
			SyntaxGenerator generator, BlockSyntax handleBodyBlock, SyntaxToken handleMethodParameterName)
		{
			var messageTypeSyntax = namedMessageTypeSymbol.ToTypeSyntax(generator);
			var eventHandlerDeclaration = SyntaxFactory.ClassDeclaration($"{namedMessageTypeSymbol.Name}{HandlerClassSuffix}")
				.WithModifiers(
					SyntaxFactory.TokenList(
						SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
				.WithBaseList(
					SyntaxFactory.BaseList(
						SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
							SyntaxFactory.SimpleBaseType(
								SyntaxFactory.GenericName($"{typeof(IConsumer<IMessage>).Namespace}.{nameof(IConsumer<IMessage>)}")
									.WithTypeArgumentList(
										SyntaxFactory.TypeArgumentList(
											SyntaxFactory.SingletonSeparatedList(
												messageTypeSyntax)))))));

			if (namedMessageTypeSymbol.IsGenericType)
			{
				eventHandlerDeclaration =
					eventHandlerDeclaration.WithTypeParameterList(
							SyntaxFactory.TypeParameterList(
								SyntaxFactory.SeparatedList(
									namedMessageTypeSymbol.TypeParameters.Select(generator.TypeExpression)
										.Select(e => SyntaxFactory.TypeParameter(e.GetFirstToken())))))
						.AddConstraintClauses(
							namedMessageTypeSymbol.TypeParameters.Select(typeParameterSymbol => SyntaxFactory.TypeParameterConstraintClause(
								SyntaxFactory.IdentifierName(typeParameterSymbol.Name),
								SyntaxFactory.SeparatedList(typeParameterSymbol.GetTypeParameterConstraints(generator)))).ToArray());
			}
			eventHandlerDeclaration = eventHandlerDeclaration
				.WithMembers(
					SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
						SyntaxFactory.MethodDeclaration(
								SyntaxFactory.PredefinedType(
									SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
								SyntaxFactory.Identifier(nameof(IConsumer<IMessage>.Handle)))
							.WithModifiers(
								SyntaxFactory.TokenList(
									SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
							.WithParameterList(
								SyntaxFactory.ParameterList(
									SyntaxFactory.SingletonSeparatedList(
										SyntaxFactory.Parameter(handleMethodParameterName)
											.WithType(messageTypeSyntax)
									)))
							.WithBody(handleBodyBlock)))
				.WithAdditionalAnnotations(Formatter.Annotation);
			return eventHandlerDeclaration;
		}

		public static void GetRequestAsyncInfo(MemberAccessExpressionSyntax requestAsyncMemberAccess, SemanticModel model, out INamedTypeSymbol messageType, out INamedTypeSymbol eventType, out INamedTypeSymbol errorEventType)
		{
			var methodSymbolInfo = model.GetSymbolInfo(requestAsyncMemberAccess).Symbol as IMethodSymbol;

			if (methodSymbolInfo == null || methodSymbolInfo.TypeArguments.Length < 3)
			{
				throw new InvalidOperationException();
			}
			messageType = (INamedTypeSymbol) methodSymbolInfo.TypeArguments.ElementAt(0);
			eventType = (INamedTypeSymbol) methodSymbolInfo.TypeArguments.ElementAt(1);
			errorEventType = (INamedTypeSymbol) methodSymbolInfo.TypeArguments.ElementAt(2);
		}

#if false
		private static bool IsMember(SyntaxNode[] childNodes, SemanticModel semanticModel,
			CancellationToken cancellationToken, Func<IMethodSymbol, MethodInfo> tryGetMatchingMethodInfo)
		{
			var methodExpression = childNodes.ElementAt(1);
			var methodSymbolInfo = semanticModel
				.GetSymbolInfo(methodExpression, cancellationToken)
				.Symbol as IMethodSymbol;
			if (methodSymbolInfo == null)
			{
				return false;
			}
			return tryGetMatchingMethodInfo(methodSymbolInfo) != null;
		}

		public static bool IsHandle(SyntaxNode[] childNodes, SemanticModel semanticModel, CancellationToken cancellationToken)
		{
			return IsMember(childNodes, semanticModel, cancellationToken,
				methodSymbolInfo => !methodSymbolInfo.IsGenericMethod && methodSymbolInfo.Arity == 0
					? Helpers.GetHandleInvocationMethodInfo(methodSymbolInfo)
					: null);
		}

		public static bool IsRequestAsync(SyntaxNode[] childNodes, SemanticModel semanticModel,
			CancellationToken cancellationToken)
		{
			return IsMember(childNodes, semanticModel, cancellationToken,
				methodSymbolInfo => methodSymbolInfo.IsGenericMethod && methodSymbolInfo.Arity > 1
					? Helpers.GetRequestAsyncInvocationMethodInfo(methodSymbolInfo)
					: null);
		}

		public static bool IsSend(SyntaxNode[] childNodes, SemanticModel semanticModel, CancellationToken cancellationToken)
		{
			return IsMember(childNodes, semanticModel, cancellationToken,
				methodSymbolInfo => methodSymbolInfo.IsGenericMethod && methodSymbolInfo.Arity == 1
					? Helpers.GetSendInvocationMethodInfo(methodSymbolInfo)
					: null);
		}

		public static bool IsPublish(SyntaxNode[] childNodes, SemanticModel semanticModel, CancellationToken cancellationToken)
		{
			return IsMember(childNodes, semanticModel, cancellationToken,
				methodSymbolInfo => methodSymbolInfo.IsGenericMethod && methodSymbolInfo.Arity == 1
					? Helpers.GetPublishInvocationMethodInfo(methodSymbolInfo)
					: null);
		}
#endif

		public static bool IsMember(SimpleNameSyntax name, SemanticModel semanticModel, CancellationToken cancellationToken, Func<IMethodSymbol, MethodInfo> tryGetMatchingMethodInfo)
		{
			var methodSymbolInfo = semanticModel
				.GetSymbolInfo(name, cancellationToken)
				.Symbol as IMethodSymbol;
			if (methodSymbolInfo == null)
			{
				return false;
			}
			return tryGetMatchingMethodInfo(methodSymbolInfo) != null;
		}

		public static bool IsMember(SimpleNameSyntax name, MethodInfo methodInfo, SemanticModel semanticModel,
			CancellationToken cancellationToken)
		{
			var methodSymbolInfo = semanticModel
				.GetSymbolInfo(name, cancellationToken)
				.Symbol as IMethodSymbol;
			if (methodSymbolInfo == null)
			{
				return false;
			}
			return methodSymbolInfo.IsSymbolOf(methodInfo);
		}

		public static bool IsSend(SimpleNameSyntax name, SemanticModel semanticModel, CancellationToken cancellationToken)
		{
			return IsMember(name, semanticModel, cancellationToken,
				methodSymbolInfo => methodSymbolInfo.IsGenericMethod && methodSymbolInfo.Arity == 1
					? Helpers.GetSendInvocationMethodInfo(methodSymbolInfo)
					: null);
		}

		public static bool IsHandle(SimpleNameSyntax name, SemanticModel semanticModel, CancellationToken cancellationToken)
		{
			return IsMember(name, semanticModel, cancellationToken,
				methodSymbolInfo => !methodSymbolInfo.IsGenericMethod && methodSymbolInfo.Arity == 0
					? Helpers.GetHandleInvocationMethodInfo(methodSymbolInfo)
					: null);
		}

		public static bool IsPublish(SimpleNameSyntax name, SemanticModel semanticModel, CancellationToken cancellationToken)
		{
			return IsMember(name, semanticModel, cancellationToken,
				methodSymbolInfo => methodSymbolInfo.IsGenericMethod && methodSymbolInfo.Arity == 1
					? Helpers.GetPublishInvocationMethodInfo(methodSymbolInfo)
					: null);
		}
	}
}