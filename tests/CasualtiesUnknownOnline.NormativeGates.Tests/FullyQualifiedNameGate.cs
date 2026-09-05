using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Roslyn syntax-tree gate for the repository convention that prefers
/// <c>using</c> directives/aliases over unnecessary fully qualified type names.
/// The gate deliberately reports only the outermost qualified name in a chain
/// and allows the documented exceptions: HotRepl-style strings are not syntax,
/// namespace declarations and using directives are not type references, and a
/// fully qualified name is allowed when the simple name collides with a member
/// declared in the enclosing type (for example a class with a method named
/// <c>Path</c> still needs <c>System.IO.Path</c> on some lines).
/// </summary>
internal sealed record FullyQualifiedNameViolation(string FilePath, int Line, string Text);

internal static class FullyQualifiedNameGate
{
	private static readonly string[] RootNamespaces =
	[
		"System",
		"Microsoft",
		"UnityEngine",
		"Newtonsoft",
		"BepInEx",
		"Steamworks",
		"CasualtiesUnknownOnline"
	];

	internal static IReadOnlyList<FullyQualifiedNameViolation> FindInFile(string filePath, string source)
	{
		var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
		var root = tree.GetRoot();
		var violations = new List<FullyQualifiedNameViolation>();

		foreach (var node in root.DescendantNodes().OfType<QualifiedNameSyntax>())
		{
			if (!IsOutermostQualifiedName(node)
				|| !StartsWithKnownRootNamespace(node.ToString())
				|| IsNonTypeNameContext(node))
			{
				continue;
			}

			if (WouldCollideWithEnclosingMember(node, node.Right.Identifier.ValueText, includeFinalMember: true))
			{
				continue;
			}

			violations.Add(CreateViolation(tree, filePath, node.ToString(), node.Span));
		}

		foreach (var node in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
		{
			if (!IsOutermostMemberAccess(node)
				|| !StartsWithKnownRootNamespace(node.ToString())
				|| IsNonTypeNameContext(node))
			{
				continue;
			}

			if (WouldCollideWithEnclosingMember(node, node.ToString(), includeFinalMember: false))
			{
				continue;
			}

			violations.Add(CreateViolation(tree, filePath, node.ToString(), node.Span));
		}

		return violations;
	}

	private static bool IsOutermostQualifiedName(QualifiedNameSyntax node) => node.Parent is not QualifiedNameSyntax;

	private static bool IsOutermostMemberAccess(MemberAccessExpressionSyntax node) => node.Parent is not MemberAccessExpressionSyntax;

	private static bool StartsWithKnownRootNamespace(string text)
	{
		foreach (var root in RootNamespaces)
		{
			if (text.StartsWith(root + ".", StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsNonTypeNameContext(SyntaxNode node)
	{
		foreach (var ancestor in node.Ancestors())
		{
			if (ancestor is UsingDirectiveSyntax)
			{
				return true;
			}

			if (ancestor is NamespaceDeclarationSyntax namespaceDeclaration && IsWithinNamespaceName(node, namespaceDeclaration.Name))
			{
				return true;
			}

			if (ancestor is FileScopedNamespaceDeclarationSyntax fileScopedNamespace && IsWithinNamespaceName(node, fileScopedNamespace.Name))
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsWithinNamespaceName(SyntaxNode node, SyntaxNode namespaceName) => node == namespaceName || node.Ancestors().Contains(namespaceName);

	private static bool WouldCollideWithEnclosingMember(SyntaxNode node, string qualifiedName, bool includeFinalMember)
	{
		var segments = qualifiedName.Split('.');
		var lastIndex = includeFinalMember ? segments.Length : segments.Length - 1;

		foreach (var ancestor in node.Ancestors().OfType<TypeDeclarationSyntax>())
		{
			for (var i = 1; i < lastIndex; i++)
			{
				if (HasMemberNamed(ancestor, segments[i]))
				{
					return true;
				}
			}
		}

		return false;
	}

	private static FullyQualifiedNameViolation CreateViolation(SyntaxTree tree, string filePath, string text, TextSpan span)
	{
		var line = tree.GetLineSpan(span).StartLinePosition.Line + 1;
		return new FullyQualifiedNameViolation(filePath, line, text);
	}

	private static bool HasMemberNamed(TypeDeclarationSyntax type, string name)
	{
		foreach (var member in type.Members)
		{
			switch (member)
			{
				case MethodDeclarationSyntax method when method.Identifier.ValueText == name:
				case PropertyDeclarationSyntax property when property.Identifier.ValueText == name:
				case EventDeclarationSyntax @event when @event.Identifier.ValueText == name:
				case TypeDeclarationSyntax nestedType when nestedType.Identifier.ValueText == name:
					return true;
				case FieldDeclarationSyntax field when field.Declaration.Variables.Any(v => v.Identifier.ValueText == name):
				case EventFieldDeclarationSyntax eventField when eventField.Declaration.Variables.Any(v => v.Identifier.ValueText == name):
					return true;
				case EnumDeclarationSyntax @enum when @enum.Identifier.ValueText == name:
					return true;
			}
		}

		return false;
	}
}
