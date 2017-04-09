using System;
using System.Diagnostics;
using System.Linq;
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
		private static string _handlerClassSuffix = "Handler";

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

		/// TODO: move to utility class
		public static ClassDeclarationSyntax MessageHandlerDeclaration(INamedTypeSymbol namedMessageTypeSymbol,
			SyntaxGenerator generator, BlockSyntax handleBodyBlock, SyntaxToken handleMethodParameterName)
		{
			var messageTypeSyntax = namedMessageTypeSymbol.ToTypeSyntax(generator);
			var eventHandlerDeclaration = SyntaxFactory.ClassDeclaration($"{namedMessageTypeSymbol.Name}{_handlerClassSuffix}")
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
#if DEBUG
				Debug.WriteLine(eventHandlerDeclaration.NormalizeWhitespace().ToString());
#endif // DEBUG
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
#if DEBUG
			Debug.WriteLine(eventHandlerDeclaration.NormalizeWhitespace().ToString());
#endif // DEBUG
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
	}
}