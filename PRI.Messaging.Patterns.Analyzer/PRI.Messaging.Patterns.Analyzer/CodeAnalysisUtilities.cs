using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PRI.Messaging.Patterns.Analyzer;

static internal class CodeAnalysisUtilities
{
	public static BlockSyntax NotImplementedBlock()
	{
		return SyntaxFactory.Block(
			SyntaxFactory.SingletonList<StatementSyntax>(
				SyntaxFactory.ThrowStatement(
					SyntaxFactory.ObjectCreationExpression(
							SyntaxFactory.IdentifierName(nameof(NotImplementedException)))
						.WithArgumentList(
							SyntaxFactory.ArgumentList()))));
	}

	/// <summary>
	/// Replace invocation expression with awaited invocation.
	/// </summary>
	/// <param name="document"></param>
	/// <param name="invocation"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	public static async Task<Document> InvokeAsync(Document document,
		InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
	{
		var parentMethodDeclaration = CodeAnalysisExtensions.GetContainingMethodDeclaration(invocation);
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
			root = root.ReplaceNode(newParentMethodDeclaration, parentMethodDeclaration.WithAsync(newParentMethodDeclaration, model));
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