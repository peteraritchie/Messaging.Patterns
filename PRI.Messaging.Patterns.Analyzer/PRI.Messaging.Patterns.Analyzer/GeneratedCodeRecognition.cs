using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using CSharpExtensions = Microsoft.CodeAnalysis.CSharpExtensions;

namespace PRI.Messaging.Patterns.Analyzer
{
	public static class GeneratedCodeRecognition
	{

		static WeakReference<ImmutableDictionary<SyntaxTree, bool>> cache = new WeakReference<ImmutableDictionary<SyntaxTree, bool>>(ImmutableDictionary<SyntaxTree, bool>.Empty);

		public static bool IsFromGeneratedCode(this SemanticModel semanticModel, CancellationToken cancellationToken)
		{
			ImmutableDictionary<SyntaxTree, bool> table;
			var tree = semanticModel.SyntaxTree;

			if (cache.TryGetTarget(out table))
			{
				if (table.ContainsKey(tree))
					return table[tree];
			}
			else
			{
				table = ImmutableDictionary<SyntaxTree, bool>.Empty;
			}

			var result = IsFileNameForGeneratedCode(tree.FilePath) || ContainsAutogeneratedComment(tree, cancellationToken);
			cache.SetTarget(table.Add(tree, result));

			return result;
		}

		public static bool IsFromGeneratedCode(this SyntaxNodeAnalysisContext context)
		{
			return IsFromGeneratedCode(context.SemanticModel, context.CancellationToken);
		}

		public static bool IsFromGeneratedCode(this SemanticModelAnalysisContext context)
		{
			return IsFromGeneratedCode(context.SemanticModel, context.CancellationToken);
		}

		static readonly string[] generatedCodeSuffixes = {
			"AssemblyInfo",
			".designer",
			".generated",
			".g",
			".g.i",
			".AssemblyAttributes"
		};

		static readonly string generatedCodePrefix = "TemporaryGeneratedFile_";
		const int generatedCodePrefixLength = 23;

		public static bool IsFileNameForGeneratedCode(string fileName)
		{
			if (fileName.StartsWith(generatedCodePrefix, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			string extension = Path.GetExtension(fileName);
			if (extension != string.Empty)
			{
				fileName = Path.GetFileNameWithoutExtension(fileName);

				foreach (var suffix in generatedCodeSuffixes)
				{
					if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
						return true;
				}
			}

			return false;
		}

		static bool ContainsAutogeneratedComment(SyntaxTree tree, CancellationToken cancellationToken = default(CancellationToken))
		{
			var root = tree.GetRoot(cancellationToken);
			if (root == null)
				return false;
			var firstToken = root.GetFirstToken();
			if (!firstToken.HasLeadingTrivia)
			{
				return false;
			}

			foreach (var trivia in firstToken.LeadingTrivia.Where(t => CSharpExtensions.IsKind((SyntaxTrivia) t, SyntaxKind.SingleLineCommentTrivia)).Take(2))
			{
				var str = trivia.ToString();
				if (str == "// This file has been generated by the GUI designer. Do not modify." ||
				    str == "// <auto-generated>" || str == "// <autogenerated>")
				{
					return true;
				}
			}
			return false;
		}
	}
}