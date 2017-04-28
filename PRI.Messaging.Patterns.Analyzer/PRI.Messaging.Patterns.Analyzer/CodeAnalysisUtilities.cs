using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using PRI.Messaging.Patterns.Analyzer;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

static internal class CodeAnalysisUtilities
{
	public static BlockSyntax NotImplementedBlock()
	{
		return Block(
			SingletonList<StatementSyntax>(
				ThrowStatement(
					ObjectCreationExpression(
							IdentifierName(nameof(NotImplementedException)))
						.WithArgumentList(
							ArgumentList()))));
	}

#if NO_0100
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
		var newInvocation = AwaitExpression(invocation)
			.WithAdditionalAnnotations(annotation);
		var root = await document.GetSyntaxRootAsync(cancellationToken);
		root = root.ReplaceNode(invocation, newInvocation);
		newInvocation = (AwaitExpressionSyntax) root.GetAnnotatedNodes(annotation).Single();
		// if newInvocation.Parent is equals and parent.parent is declaration, look for all uses of that symbol
		var oldParentMethodDeclaration = newInvocation
			.Ancestors()
			.OfType<MethodDeclarationSyntax>()
			.FirstOrDefault();
		var model = await document.GetSemanticModelAsync(cancellationToken);
		if (oldParentMethodDeclaration != null)
		{
			root = root.ReplaceNode(oldParentMethodDeclaration,
				parentMethodDeclaration
				.WithAsync(oldParentMethodDeclaration, model)
				.WithAdditionalAnnotations(Formatter.Annotation));
		}

		var compilationUnitSyntax = (CompilationUnitSyntax) root;

		var usings = compilationUnitSyntax.Usings;
		if (!usings.Any(e => e.Name.ToString().Equals(typeof(Task).Namespace)))
		{
			var usingDirective = UsingDirective(ParseName(typeof(Task).Namespace));
			root = compilationUnitSyntax.AddUsings(usingDirective);
		}
		return document.WithSyntaxRoot(root);
	}
#endif
}