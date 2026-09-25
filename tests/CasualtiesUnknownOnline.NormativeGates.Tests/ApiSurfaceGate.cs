using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>One recorded public surface entry: its stable key and the canonical baseline line.</summary>
internal sealed record ApiSurfaceEntry(string Key, string Line);

/// <summary>A deliberate removal: the entry's key plus the reason the policy requires.</summary>
internal sealed record ApiSurfaceTombstone(string Key, string Reason);

/// <summary>One difference between the tree's surface and the reviewed baseline, or an extraction defect.</summary>
internal sealed record ApiSurfaceFinding(string Kind, string Detail);

/// <summary>The scanned surface, the number of source files it came from, and the extraction's own findings.</summary>
internal sealed record ApiSurfaceScan(
	IReadOnlyList<ApiSurfaceEntry> Entries,
	int FileCount,
	IReadOnlyList<ApiSurfaceFinding> Findings);

/// <summary>The comparison result, carrying both sides' counts so the gate can assert a census floor.</summary>
internal sealed class ApiSurfaceComparison(
	ApiSurfaceScan scan,
	IReadOnlyList<ApiSurfaceEntry> baseline,
	IReadOnlyList<ApiSurfaceTombstone> tombstones,
	IReadOnlyList<ApiSurfaceFinding> findings)
{
	internal ApiSurfaceScan Scan { get; } = scan;

	internal IReadOnlyList<ApiSurfaceEntry> Baseline { get; } = baseline;

	internal IReadOnlyList<ApiSurfaceTombstone> Tombstones { get; } = tombstones;

	internal IReadOnlyList<ApiSurfaceFinding> Findings { get; } = findings;

	internal int SurfaceTypeCount => Scan.Entries.Count(IsTypeEntry);

	internal int BaselineTypeCount => Baseline.Count(IsTypeEntry);

	internal bool IsClean => Findings.Count == 0;

	internal string Describe()
	{
		var builder = new StringBuilder();
		builder.Append("public surface: ").Append(Scan.Entries.Count).Append(" entries in ")
			.Append(Scan.FileCount).Append(" files; baseline: ").Append(Baseline.Count)
			.Append(" entries and ").Append(Tombstones.Count).Append(" tombstone(s)");
		foreach (var finding in Findings)
		{
			builder.Append(Environment.NewLine).Append("  ").Append(finding.Kind).Append(": ").Append(finding.Detail);
		}

		return builder.ToString();
	}

	private static bool IsTypeEntry(ApiSurfaceEntry entry) => entry.Key.StartsWith("type|", StringComparison.Ordinal);
}

/// <summary>
/// The public-surface gate behind the Mod API governance rule: the Abstractions
/// assembly is the only CUO assembly a mod may reference, so its public surface is
/// a reviewed artifact (<c>docs/contracts/abstractions-api-baseline.txt</c>) rather than
/// whatever the latest commit happens to declare. The gate re-derives the surface
/// from the project's source with Roslyn and compares it with that record: an
/// addition, a removal without a tombstone, a signature, modifier or
/// stability-level change, a malformed or duplicated line, and a public declaration
/// kind the gate does not model all fail.
///
/// Boundaries, stated rather than implied: the surface is the DECLARED source
/// surface (a compiler-synthesized member — a record's equality members — is
/// implied by its declaration and is not listed, while a primary constructor IS
/// listed because it is how a consumer builds the type); type references are
/// recorded by their simple name so that a <c>using</c> edit is not an API change;
/// a member's accessors and default values are recorded as written after the
/// whitespace normalization <c>dotnet format</c> enforces; declaration modifiers
/// are recorded because <c>static</c> versus instance is an API difference; and a
/// C# 14 <c>extension</c> block is modeled with its receiver folded into each
/// member's parameter list, so an extension member is visible to a caller. Attribute
/// lists other than <c>[ApiStability]</c> are not recorded; an attribute that changes
/// behavior belongs to a member whose signature or documentation changes with it.
/// </summary>
internal static class ApiSurfaceGate
{
	internal const string ProjectSourceDir = "src/CasualtiesUnknownOnline.Abstractions";
	internal const string BaselinePath = "docs/contracts/abstractions-api-baseline.txt";
	internal const string EmittedBaselinePath = "artifacts/api-surface/abstractions-api-baseline.txt";

	/// <summary>Census floors: roughly 60% of the measured tree (92 source files, 92 public types and 756 entries at 2026-09-20), so a scan that silently finds nothing cannot pass.</summary>
	internal const int SourceFileFloor = 60;
	internal const int TypeCensusFloor = 54;
	internal const int EntryCensusFloor = 450;

	internal const string RemovalMarker = "*REMOVED*";

	private static readonly string[] Levels = ["Stable", "Experimental", "Advanced", "Obsolete"];
	private static readonly string[] AccessibilityModifiers = ["public", "private", "protected", "internal"];

	private static readonly Regex WhitespaceRegex = new(@"\s+");
	private static readonly Regex QualifiedNameRegex = new(@"\b(?:[A-Za-z_][A-Za-z0-9_]*\.)+([A-Za-z_][A-Za-z0-9_]*)");
	private static readonly Regex PrimitiveRegex = new(@"\b(Int32|UInt32|Int64|UInt64|Int16|UInt16|Byte|SByte|Single|Double|Boolean|String|Char|Object|Void|Decimal)\b");

	private static readonly Dictionary<string, string> Primitives = new(StringComparer.Ordinal)
	{
		["Int32"] = "int",
		["UInt32"] = "uint",
		["Int64"] = "long",
		["UInt64"] = "ulong",
		["Int16"] = "short",
		["UInt16"] = "ushort",
		["Byte"] = "byte",
		["SByte"] = "sbyte",
		["Single"] = "float",
		["Double"] = "double",
		["Boolean"] = "bool",
		["String"] = "string",
		["Char"] = "char",
		["Object"] = "object",
		["Void"] = "void",
		["Decimal"] = "decimal"
	};

	internal static ApiSurfaceScan ScanTree()
	{
		var directory = RepositoryPaths.File(ProjectSourceDir);
		var files = Directory
			.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
			.Where(path => !IsBuildOutput(path))
			.OrderBy(path => path, StringComparer.Ordinal)
			.ToList();
		return ExtractFromSources(files.Select(path => (path, File.ReadAllText(path))));
	}

	internal static ApiSurfaceScan ExtractFromSources(IEnumerable<(string Path, string Source)> sources)
	{
		var entries = new List<ApiSurfaceEntry>();
		var findings = new List<ApiSurfaceFinding>();
		var fileCount = 0;
		foreach (var (path, source) in sources)
		{
			fileCount++;
			var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview), path).GetRoot();
			foreach (var type in root.DescendantNodes().Where(IsPublicType))
			{
				AddType(entries, findings, type);
			}
		}

		return new ApiSurfaceScan(
			[
				.. entries
					// A partial type declares the same type line in every part: identical declarations collapse
					// into one entry, while two DIFFERENT lines for one key stay a duplicate finding.
					.Distinct()
					.OrderBy(entry => entry.Key, StringComparer.Ordinal)
					.ThenBy(entry => entry.Line, StringComparer.Ordinal)
			],
			fileCount,
			findings);
	}

	internal static ApiSurfaceComparison Compare(ApiSurfaceScan scan, string baselineText)
	{
		var findings = new List<ApiSurfaceFinding>(scan.Findings);
		var tombstones = new List<ApiSurfaceTombstone>();
		var baseline = new List<ApiSurfaceEntry>();
		ParseBaseline(baselineText, baseline, tombstones, findings);

		var surfaceByKey = scan.Entries.ToLookup(entry => entry.Key, StringComparer.Ordinal);
		var baselineByKey = baseline.ToLookup(entry => entry.Key, StringComparer.Ordinal);
		var tombstoned = tombstones.Select(tombstone => tombstone.Key).ToHashSet(StringComparer.Ordinal);

		foreach (var group in scan.Entries.GroupBy(entry => entry.Key, StringComparer.Ordinal).Where(group => group.Count() > 1))
		{
			findings.Add(new ApiSurfaceFinding("DUPLICATE", $"the tree declares '{group.Key}' {group.Count()} times: {string.Join(" ; ", group.Select(entry => entry.Line))}"));
		}

		foreach (var group in baseline.GroupBy(entry => entry.Key, StringComparer.Ordinal).Where(group => group.Count() > 1))
		{
			findings.Add(new ApiSurfaceFinding("DUPLICATE", $"the baseline records '{group.Key}' {group.Count()} times"));
		}

		foreach (var group in tombstones.GroupBy(tombstone => tombstone.Key, StringComparer.Ordinal).Where(group => group.Count() > 1))
		{
			findings.Add(new ApiSurfaceFinding("DUPLICATE", $"the baseline tombstones '{group.Key}' {group.Count()} times"));
		}

		foreach (var entry in scan.Entries)
		{
			if (!baselineByKey.Contains(entry.Key))
			{
				findings.Add(new ApiSurfaceFinding("ADDED", entry.Line));
			}
		}

		foreach (var entry in baseline)
		{
			if (surfaceByKey.Contains(entry.Key))
			{
				var current = surfaceByKey[entry.Key].First();
				if (!string.Equals(current.Line, entry.Line, StringComparison.Ordinal))
				{
					findings.Add(new ApiSurfaceFinding("CHANGED", $"{entry.Line}{Environment.NewLine}            -> {current.Line}"));
				}

				continue;
			}

			if (!tombstoned.Contains(entry.Key))
			{
				findings.Add(new ApiSurfaceFinding("REMOVED", $"{entry.Line} (no '{RemovalMarker} {entry.Key} — <reason>' tombstone)"));
			}
		}

		foreach (var tombstone in tombstones)
		{
			if (baselineByKey.Contains(tombstone.Key))
			{
				findings.Add(new ApiSurfaceFinding("TOMBSTONE", $"'{tombstone.Key}' is tombstoned and still listed as a live entry"));
			}
			else if (surfaceByKey.Contains(tombstone.Key))
			{
				findings.Add(new ApiSurfaceFinding("TOMBSTONE", $"'{tombstone.Key}' is declared again; delete the tombstone and record the entry"));
			}
		}

		return new ApiSurfaceComparison(scan, baseline, tombstones, findings);
	}

	internal static string BaselineTextFor(IEnumerable<ApiSurfaceEntry> entries, IEnumerable<ApiSurfaceTombstone> tombstones)
	{
		var builder = new StringBuilder();
		builder.AppendLine("# CUO Mod API public surface baseline — the reviewed record of the ONLY assembly a mod may");
		builder.AppendLine("# reference. Policy: docs/en/reference/modification-policy.md. Gate: ApiSurfaceGateTests.");
		builder.AppendLine("#");
		builder.AppendLine("# One entry per line, '|'-separated; a leading '#' starts a comment.");
		builder.AppendLine("#   type|<level>|<fully qualified name>|<kind and modifiers>|<base list, '-' when there is none>");
		builder.AppendLine("#   member|<level>|<owner>.<name>(<parameter type list>)|<kind and modifiers>|<signature>");
		builder.AppendLine("# <level> is the ApiStabilityLevel from [ApiStability]; a type without it is Stable.");
		builder.AppendLine("# ANY line change is an API change: the gate fails until the reviewed line is updated here.");
		builder.AppendLine("# A removal deletes the line AND adds a tombstone, '*REMOVED* <key> — <reason>'.");
		builder.AppendLine("# Type references are recorded by their simple name (a using edit is not an API change),");
		builder.AppendLine("# whitespace is collapsed, and implicit enum values are recorded as 'implicit #<ordinal>'.");
		builder.AppendLine("# A C# 14 extension block folds its receiver into each member's parameter list.");
		builder.AppendLine();
		foreach (var entry in entries)
		{
			builder.AppendLine(entry.Line);
		}

		foreach (var tombstone in tombstones)
		{
			builder.AppendLine($"{RemovalMarker} {tombstone.Key} — {tombstone.Reason}");
		}

		return builder.ToString();
	}

	internal static void EmitForReview(ApiSurfaceScan scan, IEnumerable<ApiSurfaceTombstone> tombstones)
	{
		var path = RepositoryPaths.File(EmittedBaselinePath);
		var directory = Path.GetDirectoryName(path);
		if (directory is not null)
		{
			Directory.CreateDirectory(directory);
		}

		File.WriteAllText(path, BaselineTextFor(scan.Entries, tombstones), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	}

	private static void ParseBaseline(
		string baselineText,
		List<ApiSurfaceEntry> entries,
		List<ApiSurfaceTombstone> tombstones,
		List<ApiSurfaceFinding> findings)
	{
		var lineNumber = 0;
		foreach (var rawLine in baselineText.Split('\n'))
		{
			lineNumber++;
			var line = rawLine.TrimEnd('\r').Trim();
			if (line.Length == 0 || line.StartsWith('#'))
			{
				continue;
			}

			if (line.StartsWith(RemovalMarker, StringComparison.Ordinal))
			{
				var body = line[RemovalMarker.Length..].Trim();
				var separator = body.IndexOf(" — ", StringComparison.Ordinal);
				if (separator < 0)
				{
					findings.Add(new ApiSurfaceFinding("MALFORMED", $"line {lineNumber}: a tombstone must read '{RemovalMarker} <key> — <reason>'"));
					continue;
				}

				var reason = body[(separator + 3)..].Trim();
				if (reason.Length == 0)
				{
					findings.Add(new ApiSurfaceFinding("MALFORMED", $"line {lineNumber}: a tombstone must name its reason"));
					continue;
				}

				tombstones.Add(new ApiSurfaceTombstone(body[..separator].Trim(), reason));
				continue;
			}

			var fields = line.Split('|');
			var wellFormed = fields.Length >= 4
				&& (fields[0] == "type" || fields[0] == "member")
				&& Levels.Contains(fields[1])
				&& fields[2].Length > 0;
			if (!wellFormed)
			{
				findings.Add(new ApiSurfaceFinding("MALFORMED", $"line {lineNumber}: '{line}'"));
				continue;
			}

			entries.Add(new ApiSurfaceEntry($"{fields[0]}|{fields[2]}", line));
		}
	}

	private static bool IsBuildOutput(string path)
	{
		var separator = Path.DirectorySeparatorChar;
		return path.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
			|| path.Contains($"{separator}bin{separator}", StringComparison.Ordinal);
	}

	private static bool IsPublicType(SyntaxNode node)
	{
		if (node is not MemberDeclarationSyntax member
			|| node is not (TypeDeclarationSyntax or EnumDeclarationSyntax or DelegateDeclarationSyntax))
		{
			return false;
		}

		return IsEffectivelyPublic(member);
	}

	private static bool IsEffectivelyPublic(MemberDeclarationSyntax member)
	{
		if (!HasModifier(member, "public") && !IsImplicitlyPublicInterfaceMember(member))
		{
			return false;
		}

		// Only TYPE ancestors count: a namespace declaration is a MemberDeclarationSyntax too, and it
		// carries no accessibility — reading it as one would hide every type in the project.
		foreach (var ancestor in member.Ancestors().OfType<TypeDeclarationSyntax>())
		{
			if (!IsEffectivelyPublic(ancestor))
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsImplicitlyPublicInterfaceMember(MemberDeclarationSyntax member) =>
		member.Parent is InterfaceDeclarationSyntax
		&& !HasModifier(member, "private")
		&& !HasModifier(member, "protected")
		&& !HasModifier(member, "internal");

	private static bool HasModifier(SyntaxNode node, string modifier) => ModifiersOf(node).Contains(modifier);

	private static IReadOnlyList<string> ModifiersOf(SyntaxNode node) => node switch
	{
		MemberDeclarationSyntax member => member.Modifiers.Select(token => token.ValueText).ToList(),
		_ => []
	};

	/// <summary>The declared modifiers minus accessibility: accessibility already decides whether a member is surface, and <c>static</c> versus instance is an API difference a caller sees.</summary>
	private static string ContractModifiersOf(SyntaxNode node) =>
		string.Join(" ", ModifiersOf(node).Where(modifier => !AccessibilityModifiers.Contains(modifier)));

	private static string KindOf(SyntaxNode node, string kind)
	{
		var modifiers = ContractModifiersOf(node);
		return modifiers.Length == 0 ? kind : $"{modifiers} {kind}";
	}

	private static void AddType(List<ApiSurfaceEntry> entries, List<ApiSurfaceFinding> findings, SyntaxNode type)
	{
		var name = QualifiedNameOf(type);
		var level = LevelOf(type, "Stable", findings);
		var shape = TypeShapeOf(type);
		entries.Add(new ApiSurfaceEntry($"type|{name}", $"type|{level}|{name}|{KindOf(type, TypeKindOf(type))}|{shape}"));

		// A primary constructor is public surface too: it is the way a consumer builds the type, and no
		// ConstructorDeclarationSyntax exists for it, so it is recorded from the type's own parameter list.
		if (type is TypeDeclarationSyntax { ParameterList: not null } primary)
		{
			Add(entries, KindOf(type, "constructor"), $"{name}.ctor({Parameters(primary.ParameterList)})", $"-> {name}", level);
		}

		switch (type)
		{
			case EnumDeclarationSyntax @enum:
				AddEnumMembers(entries, @enum, name, level);
				break;
			case TypeDeclarationSyntax declaration:
				foreach (var member in declaration.Members)
				{
					AddMember(entries, findings, member, name, level, receiver: "");
				}

				break;
		}
	}

	private static void AddMember(
		List<ApiSurfaceEntry> entries,
		List<ApiSurfaceFinding> findings,
		MemberDeclarationSyntax member,
		string owner,
		string ownerLevel,
		string receiver)
	{
		if (!IsSurfaceMember(member))
		{
			return;
		}

		var level = LevelOf(member, ownerLevel, findings);
		switch (member)
		{
			case ExtensionBlockDeclarationSyntax extension:
				// A C# 14 extension block is a TypeDeclarationSyntax, so this case must precede it: it is
				// neither a named type nor a method, and its members are callable as extensions, so each one
				// is recorded with the block's receiver folded into its parameters.
				foreach (var nested in extension.Members)
				{
					AddMember(entries, findings, nested, owner, level, Parameters(extension.ParameterList));
				}

				break;
			case TypeDeclarationSyntax:
			case EnumDeclarationSyntax:
			case DelegateDeclarationSyntax:
				// Nested types are recorded as their own type entries by AddType.
				break;
			case MethodDeclarationSyntax method when method.ExplicitInterfaceSpecifier is null:
				Add(
					entries,
					KindOf(member, "method"),
					$"{owner}.{method.Identifier.ValueText}{TypeParametersOf(method.TypeParameterList)}({Parameters(method.ParameterList, receiver)})",
					$"-> {Normalize(method.ReturnType.ToString())}{ConstraintsOf(method.ConstraintClauses)}",
					level);
				break;
			case ConstructorDeclarationSyntax constructor:
				Add(entries, KindOf(member, "constructor"), $"{owner}.ctor({Parameters(constructor.ParameterList)})", $"-> {owner}", level);
				break;
			case PropertyDeclarationSyntax property:
				var propertyName = receiver.Length == 0
					? property.Identifier.ValueText
					: $"{property.Identifier.ValueText}({receiver})";
				Add(entries, KindOf(member, "property"), $"{owner}.{propertyName}", $"-> {Normalize(property.Type.ToString())} {Accessors(property.AccessorList, property.ExpressionBody)}", level);
				break;
			case IndexerDeclarationSyntax indexer:
				Add(entries, KindOf(member, "indexer"), $"{owner}.this[{Parameters(indexer.ParameterList)}]", $"-> {Normalize(indexer.Type.ToString())} {Accessors(indexer.AccessorList, indexer.ExpressionBody)}", level);
				break;
			case EventFieldDeclarationSyntax eventField:
				foreach (var variable in eventField.Declaration.Variables)
				{
					Add(entries, KindOf(member, "event"), $"{owner}.{variable.Identifier.ValueText}", $"-> {Normalize(eventField.Declaration.Type.ToString())}", level);
				}

				break;
			case EventDeclarationSyntax @event:
				Add(entries, KindOf(member, "event"), $"{owner}.{@event.Identifier.ValueText}", $"-> {Normalize(@event.Type.ToString())}", level);
				break;
			case FieldDeclarationSyntax field:
				foreach (var variable in field.Declaration.Variables)
				{
					var value = variable.Initializer is null ? "" : $" = {Normalize(variable.Initializer.Value.ToString())}";
					Add(entries, KindOf(member, "field"), $"{owner}.{variable.Identifier.ValueText}", $"-> {Normalize(field.Declaration.Type.ToString())}{value}", level);
				}

				break;
			case OperatorDeclarationSyntax @operator:
				Add(entries, KindOf(member, "operator"), $"{owner}.operator{@operator.OperatorToken.ValueText}({Parameters(@operator.ParameterList)})", $"-> {Normalize(@operator.ReturnType.ToString())}", level);
				break;
			case ConversionOperatorDeclarationSyntax conversion:
				Add(entries, KindOf(member, "operator"), $"{owner}.{conversion.ImplicitOrExplicitKeyword.ValueText}({Parameters(conversion.ParameterList)})", $"-> {Normalize(conversion.Type.ToString())}", level);
				break;
			default:
				// A declaration kind this gate does not model is recorded explicitly instead of skipped, and
				// its own member declarations are visited, so a future C# shape cannot hide public surface
				// the way the C# 14 extension block would have.
				Add(entries, KindOf(member, "unmodeled"), $"{owner}.{member.Kind()}", "-> this declaration kind is not modeled by the gate", level);
				foreach (var nested in member.ChildNodes().OfType<MemberDeclarationSyntax>())
				{
					AddMember(entries, findings, nested, owner, level, receiver);
				}

				break;
		}
	}

	private static void AddEnumMembers(List<ApiSurfaceEntry> entries, EnumDeclarationSyntax @enum, string owner, string level)
	{
		var implicitOrdinal = 0;
		foreach (var member in @enum.Members)
		{
			var value = member.EqualsValue is null
				? $"implicit #{implicitOrdinal}"
				: $"= {Normalize(member.EqualsValue.Value.ToString())}";
			implicitOrdinal++;
			Add(entries, "enumvalue", $"{owner}.{member.Identifier.ValueText}", value, level);
		}
	}

	private static void Add(List<ApiSurfaceEntry> entries, string kind, string key, string signature, string level) =>
		entries.Add(new ApiSurfaceEntry($"member|{key}", $"member|{level}|{key}|{kind}|{signature}"));

	private static bool IsSurfaceMember(MemberDeclarationSyntax member)
	{
		if (member is EnumMemberDeclarationSyntax)
		{
			return false;
		}

		if (HasModifier(member, "public"))
		{
			return true;
		}

		// An extension block is public surface when its containing class is; its own members are visited
		// through it. Interface members carry no accessibility modifier and are public by definition.
		return member is ExtensionBlockDeclarationSyntax
			|| (IsImplicitlyPublicInterfaceMember(member) && member is not ConstructorDeclarationSyntax);
	}

	private static string QualifiedNameOf(SyntaxNode type)
	{
		var segments = new List<string>();
		foreach (var ancestor in type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>())
		{
			segments.Insert(0, ancestor.Name.ToString());
		}

		var names = new List<string>();
		foreach (var ancestor in type.Ancestors().OfType<MemberDeclarationSyntax>())
		{
			if (ancestor is TypeDeclarationSyntax or EnumDeclarationSyntax)
			{
				names.Insert(0, NameOf(ancestor));
			}
		}

		names.Add(NameOf(type));
		segments.Add(string.Join("+", names));
		return string.Join(".", segments.Where(segment => segment.Length > 0));
	}

	private static string NameOf(SyntaxNode type) => type switch
	{
		TypeDeclarationSyntax declaration => declaration.Identifier.ValueText,
		EnumDeclarationSyntax @enum => @enum.Identifier.ValueText,
		DelegateDeclarationSyntax @delegate => @delegate.Identifier.ValueText,
		_ => "?"
	};

	private static string TypeKindOf(SyntaxNode type) => type switch
	{
		TypeDeclarationSyntax declaration => declaration.Keyword.ValueText,
		EnumDeclarationSyntax => "enum",
		DelegateDeclarationSyntax => "delegate",
		_ => "?"
	};

	private static string TypeShapeOf(SyntaxNode type) => type switch
	{
		TypeDeclarationSyntax declaration => declaration.BaseList is null || declaration.BaseList.Types.Count == 0
			? "-"
			: string.Join(", ", declaration.BaseList.Types.Select(baseType => Normalize(baseType.Type.ToString()))),
		DelegateDeclarationSyntax @delegate => $"-> {Normalize(@delegate.ReturnType.ToString())}({Parameters(@delegate.ParameterList)})",
		_ => "-"
	};

	private static string Parameters(BaseParameterListSyntax? list, string receiver = "")
	{
		var rendered = list is null ? "" : string.Join(", ", list.Parameters.Select(Parameter));
		if (receiver.Length == 0)
		{
			return rendered;
		}

		return rendered.Length == 0 ? receiver : $"{receiver}, {rendered}";
	}

	private static string Parameter(ParameterSyntax parameter)
	{
		var parts = parameter.Modifiers.Select(modifier => modifier.ValueText).ToList();
		parts.Add(Normalize(parameter.Type?.ToString() ?? "?"));
		parts.Add(parameter.Identifier.ValueText);
		if (parameter.Default is not null)
		{
			parts.Add($"= {Normalize(parameter.Default.Value.ToString())}");
		}

		return string.Join(" ", parts);
	}

	private static string TypeParametersOf(TypeParameterListSyntax? list) =>
		list is null ? "" : Normalize(list.ToString());

	private static string ConstraintsOf(SyntaxList<TypeParameterConstraintClauseSyntax> clauses) =>
		clauses.Count == 0 ? "" : " " + string.Join(" ", clauses.Select(clause => Normalize(clause.ToString())));

	private static string Accessors(AccessorListSyntax? list, ArrowExpressionClauseSyntax? expressionBody)
	{
		if (list is null || list.Accessors.Count == 0)
		{
			return expressionBody is null ? "" : "{ get; }";
		}

		var accessors = list.Accessors.Select(accessor =>
		{
			var accessibility = accessor.Modifiers
				.Where(modifier => modifier.ValueText is "private" or "protected" or "internal")
				.Select(modifier => modifier.ValueText + " ");
			return $"{string.Concat(accessibility)}{accessor.Keyword.ValueText};";
		});
		return $"{{ {string.Join(" ", accessors)} }}";
	}

	/// <summary>
	/// The declared stability level: the attribute when it resolves to a level, otherwise the inherited
	/// one. An <c>[ApiStability]</c> argument the gate cannot reduce to a level name is reported as a
	/// finding — a marker whose intent silently degrades to the default is the one failure this marker
	/// cannot afford.
	/// </summary>
	private static string LevelOf(SyntaxNode node, string inherited, List<ApiSurfaceFinding> findings)
	{
		foreach (var attribute in node.ChildNodes().OfType<AttributeListSyntax>().SelectMany(list => list.Attributes))
		{
			var name = attribute.Name.ToString();
			if (!name.EndsWith("ApiStability", StringComparison.Ordinal)
				&& !name.EndsWith("ApiStabilityAttribute", StringComparison.Ordinal))
			{
				continue;
			}

			var argument = attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression.ToString() ?? "";
			var level = argument.Split('.').Last().Trim();
			if (Levels.Contains(level))
			{
				return level;
			}

			findings.Add(new ApiSurfaceFinding(
				"MALFORMED",
				$"[ApiStability] argument '{argument}' does not name a level (Stable / Experimental / Advanced / Obsolete)"));
		}

		return inherited;
	}

	private static string Normalize(string text)
	{
		var normalized = QualifiedNameRegex.Replace(WhitespaceRegex.Replace(text, " ").Trim(), "$1");
		return PrimitiveRegex.Replace(normalized, match => Primitives[match.Groups[1].Value]);
	}
}
