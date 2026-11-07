using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>The derived wire surface: the files the scan read and the canonical lines it produced.</summary>
internal sealed class WireSurfaceScan
{
	internal required int FileCount { get; init; }

	internal required IReadOnlyList<string> Lines { get; init; }
}

/// <summary>What the derived surface and the recorded baseline disagree about, by key.</summary>
internal sealed class WireSurfaceComparison
{
	internal required IReadOnlyList<string> Added { get; init; }

	internal required IReadOnlyList<string> Removed { get; init; }

	internal required IReadOnlyList<string> Changed { get; init; }

	internal bool IsClean => Added.Count == 0 && Removed.Count == 0 && Changed.Count == 0;

	internal string Describe()
	{
		var lines = new List<string>();
		lines.AddRange(Added.Select(line => $"added:   {line}"));
		lines.AddRange(Removed.Select(line => $"removed: {line}"));
		lines.AddRange(Changed.Select(line => $"changed: {line}"));
		return lines.Count == 0 ? "the surface is clean" : string.Join(Environment.NewLine, lines);
	}
}

/// <summary>
/// Derives the CUO wire surface from the protocol declarations and keeps it beside the protocol version.
/// <para>
/// The wire is what protobuf reads: the <c>[ProtoContract]</c> types, their <c>[ProtoMember]</c> tags and types,
/// the enums whose numbers ride inside messages, and the <c>NetMsg</c> ids. Names, doc comments, log text and
/// declaration order are not part of it — a rename is not a wire change, a retag is.
/// </para>
/// <para>
/// The drift comparison is INERT while <c>ProtocolVersion.Current</c> is declared as the identifier
/// <c>UnreleasedBaseline</c> (decision 241's freeze). The release changes that initializer to a literal, which is
/// the release day's deliberate act; from then on a surface move must land with the matching baseline and version
/// change or the gate fails naming the lines.
/// </para>
/// </summary>
internal static class WireSurfaceGate
{
	internal const string BaselinePath = "docs/contracts/wire-surface-baseline.txt";

	/// <summary>Where an out-of-date derivation is written for review; never compared, never committed.</summary>
	internal const string CandidatePath = "artifacts/wire-surface/wire-surface-baseline.txt";

	internal const string VersionSourcePath = "src/CasualtiesUnknownOnline.Runtime/Protocol/ProtocolVersion.cs";

	internal const string ProtocolDirectory = "src/CasualtiesUnknownOnline.Runtime/Protocol";

	/// <summary>The one discriminator registry that lives outside <c>Protocol/</c>.</summary>
	internal const string StreamRegistryPath =
		"src/CasualtiesUnknownOnline.Runtime/Session/AdaptiveSync/AdaptiveStreamId.cs";

	internal const int MinimumFiles = 140;

	internal const int MinimumMessageTypes = 130;

	internal const int MinimumMemberLines = 650;

	internal const int MinimumIdLines = 100;

	internal const int MinimumEnums = 40;

	internal static WireSurfaceScan ScanTree()
	{
		var protocolDirectory = RepositoryPaths.File(ProtocolDirectory);
		if (!Directory.Exists(protocolDirectory))
		{
			throw new InvalidOperationException(
				$"{ProtocolDirectory} is missing — the wire-surface scan surface is stale, not clean");
		}

		var files = Directory.EnumerateFiles(protocolDirectory, "*.cs", SearchOption.AllDirectories).ToList();

		var streamRegistry = RepositoryPaths.File(StreamRegistryPath);
		if (!File.Exists(streamRegistry))
		{
			throw new InvalidOperationException(
				$"{StreamRegistryPath} is missing — the wire-surface scan surface is stale, not clean");
		}

		files.Add(streamRegistry);
		files.Sort(StringComparer.Ordinal);

		var lines = new List<string>();
		foreach (var file in files)
		{
			lines.AddRange(DeriveFromSource(File.ReadAllText(file)));
		}

		return new WireSurfaceScan { FileCount = files.Count, Lines = lines };
	}

	internal static IReadOnlyList<string> DeriveFromSource(string sourceText)
	{
		var root = Parse(sourceText);
		var lines = new List<string>();

		foreach (var declaration in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
		{
			lines.AddRange(DeriveEnum(declaration));
		}

		foreach (var declaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
		{
			if (declaration is not (ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax))
			{
				continue;
			}

			if (!HasAttribute(declaration.AttributeLists, "ProtoContract"))
			{
				continue;
			}

			lines.Add($"msg|{declaration.Identifier.ValueText}");

			foreach (var member in declaration.Members)
			{
				var protoMember = FindAttribute(GetAttributeLists(member), "ProtoMember");
				if (protoMember is null)
				{
					continue;
				}

				var tag = FirstArgumentText(protoMember);
				var typeSyntax = member switch
				{
					PropertyDeclarationSyntax property => property.Type,
					FieldDeclarationSyntax field => field.Declaration.Type,
					_ => null,
				};

				if (tag is null || typeSyntax is null)
				{
					continue;
				}

				var (wireType, cardinality) = NormalizeType(typeSyntax);
				var dataFormat = DataFormatOf(protoMember);
				lines.Add(dataFormat is null
					? $"member|{declaration.Identifier.ValueText}|{tag}|{wireType}|{cardinality}"
					: $"member|{declaration.Identifier.ValueText}|{tag}|{wireType}|{cardinality}|{dataFormat}");
			}
		}

		return lines;
	}

	internal static string Render(WireSurfaceScan scan)
	{
		var sorted = scan.Lines
			.OrderBy(KindRank)
			.ThenBy(SecondaryKey, StringComparer.Ordinal)
			.ToArray();

		var builder = new StringBuilder();
		builder.AppendLine("# CUO wire surface baseline — derived from the protocol declarations by WireSurfaceGate; do not edit by hand.");
		builder.AppendLine("# Gate: WireSurfaceGateTests. The protocol version is read from ProtocolVersion.Current at gate time and never copied here.");
		builder.AppendLine("# One entry per line, '|'-separated; a leading '#' starts a comment.");
		builder.AppendLine("#   id|<NetMsg name>=<number>");
		builder.AppendLine("#   msg|<type>");
		builder.AppendLine("#   member|<type>|<tag>|<wire type>|<cardinality>[|<dataformat>]");
		builder.AppendLine("#   enum|<type>|<value>=<number>");
		builder.AppendLine("# A removal deletes the line AND adds a tombstone, '*REMOVED* <key> — <reason>'.");
		builder.AppendLine("# The pre-release freeze keeps the drift comparison inert until Current stops naming UnreleasedBaseline (decision 241).");
		builder.AppendLine();

		foreach (var line in sorted)
		{
			builder.AppendLine(line);
		}

		return builder.ToString();
	}

	internal static WireSurfaceComparison Compare(string derivedText, string baselineText)
	{
		var derived = ParseEntries(derivedText);
		var baseline = ParseEntries(baselineText);

		var added = derived
			.Where(entry => !baseline.ContainsKey(entry.Key))
			.Select(entry => entry.Value)
			.ToList();

		var removed = baseline
			.Where(entry => !derived.ContainsKey(entry.Key))
			.Select(entry => entry.Value)
			.ToList();

		var changed = derived
			.Where(entry => baseline.TryGetValue(entry.Key, out var recorded)
				&& !string.Equals(recorded, entry.Value, StringComparison.Ordinal))
			.Select(entry => entry.Value)
			.ToList();

		return new WireSurfaceComparison { Added = added, Removed = removed, Changed = changed };
	}

	internal static bool IsInert() => !IsArmedFor(File.ReadAllText(RepositoryPaths.File(VersionSourcePath)));

	internal static bool IsArmedFor(string versionSourceText)
	{
		var root = Parse(versionSourceText);
		var protocolVersion = root.DescendantNodes()
			.OfType<ClassDeclarationSyntax>()
			.FirstOrDefault(type => string.Equals(type.Identifier.ValueText, "ProtocolVersion", StringComparison.Ordinal))
			?? throw new InvalidOperationException("ProtocolVersion is missing — the arming surface is stale, not clean");

		var current = protocolVersion.Members
			.OfType<FieldDeclarationSyntax>()
			.SelectMany(field => field.Declaration.Variables)
			.FirstOrDefault(variable => string.Equals(variable.Identifier.ValueText, "Current", StringComparison.Ordinal))
			?? throw new InvalidOperationException("ProtocolVersion.Current is missing — the arming surface is stale, not clean");

		return current.Initializer?.Value is not IdentifierNameSyntax identifier
			|| !string.Equals(identifier.Identifier.ValueText, "UnreleasedBaseline", StringComparison.Ordinal);
	}

	internal static void EmitForReview(WireSurfaceScan scan)
	{
		var path = RepositoryPaths.File(CandidatePath);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, Render(scan), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	}

	private static IEnumerable<string> DeriveEnum(EnumDeclarationSyntax declaration)
	{
		var name = declaration.Identifier.ValueText;
		var isMessageIds = string.Equals(name, "NetMsg", StringComparison.Ordinal);
		long next = 0;

		foreach (var member in declaration.Members)
		{
			if (member.EqualsValue is { Value: var valueExpression })
			{
				next = EnumValueOf(valueExpression, next);
			}

			yield return isMessageIds
				? $"id|{member.Identifier.ValueText}={next}"
				: $"enum|{name}|{member.Identifier.ValueText}={next}";

			next++;
		}
	}

	private static long EnumValueOf(ExpressionSyntax expression, long previous)
	{
		var text = expression.ToString();
		if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
			&& long.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexadecimal))
		{
			return hexadecimal;
		}

		return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : previous + 1;
	}

	private static (string WireType, string Cardinality) NormalizeType(TypeSyntax type) => type switch
	{
		// protobuf encodes int? and int identically (both may be absent); the annotation is not a wire type.
		NullableTypeSyntax nullable => NormalizeType(nullable.ElementType),
		ArrayTypeSyntax { ElementType: PredefinedTypeSyntax { Keyword.ValueText: "byte" } } => ("bytes", "single"),
		ArrayTypeSyntax array => (NormalizeElement(array.ElementType), "repeated"),
		GenericNameSyntax { Identifier.ValueText: "List" or "IList" or "IReadOnlyList" } generic
			=> (NormalizeElement(generic.TypeArgumentList.Arguments[0]), "repeated"),
		PredefinedTypeSyntax predefined => (WireTypeOf(predefined.Keyword.ValueText), "single"),
		IdentifierNameSyntax identifier => (identifier.Identifier.ValueText, "single"),
		QualifiedNameSyntax qualified => (qualified.Right.Identifier.ValueText, "single"),
		GenericNameSyntax generic => (generic.Identifier.ValueText, "single"),
		_ => (type.ToString(), "single"),
	};

	private static string NormalizeElement(TypeSyntax type) => NormalizeType(type).WireType;

	private static string WireTypeOf(string keyword) => keyword switch
	{
		"int" => "int32",
		"uint" => "uint32",
		"long" => "int64",
		"ulong" => "uint64",
		"short" => "int32",
		"ushort" => "uint32",
		"byte" => "uint32",
		"sbyte" => "int32",
		"bool" => "bool",
		"float" => "float",
		"double" => "double",
		"string" => "string",
		_ => keyword,
	};

	private static string? DataFormatOf(AttributeSyntax attribute)
	{
		if (attribute.ArgumentList is null)
		{
			return null;
		}

		foreach (var argument in attribute.ArgumentList.Arguments)
		{
			if (argument.NameEquals is null
				|| !string.Equals(argument.NameEquals.Name.Identifier.ValueText, "DataFormat", StringComparison.Ordinal))
			{
				continue;
			}

			var text = argument.Expression.ToString();
			var separator = text.LastIndexOf('.');
			var name = separator >= 0 ? text[(separator + 1)..] : text;
			return name switch
			{
				"FixedSize" => "fixed",
				"ZigZag" => "zigzag",
				"TwosComplement" => "twos",
				"Group" => "group",
				_ => null,
			};
		}

		return null;
	}

	private static string? FirstArgumentText(AttributeSyntax attribute) =>
		attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression.ToString();

	private static bool HasAttribute(SyntaxList<AttributeListSyntax> lists, string name) =>
		FindAttribute(lists, name) is not null;

	private static AttributeSyntax? FindAttribute(SyntaxList<AttributeListSyntax> lists, string name) =>
		lists.SelectMany(list => list.Attributes)
			.FirstOrDefault(attribute => string.Equals(SimpleNameOf(attribute.Name), name, StringComparison.Ordinal));

	private static string SimpleNameOf(NameSyntax name) => name switch
	{
		IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
		QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
		_ => name.ToString(),
	};

	private static SyntaxList<AttributeListSyntax> GetAttributeLists(MemberDeclarationSyntax member) => member switch
	{
		PropertyDeclarationSyntax property => property.AttributeLists,
		FieldDeclarationSyntax field => field.AttributeLists,
		_ => default,
	};

	private static int KindRank(string line) => KindOf(line) switch
	{
		"id" => 0,
		"msg" => 1,
		"member" => 2,
		"enum" => 3,
		_ => 4,
	};

	private static string SecondaryKey(string line)
	{
		var parts = line.Split('|');
		return parts[0] switch
		{
			"id" => $"id|{PaddedNumber(ValueAfterEquals(parts[1]))}|{parts[1]}",
			"msg" => $"msg|{parts[1]}",
			"member" => $"member|{parts[1]}|{PaddedNumber(parts[2])}",
			"enum" => $"enum|{parts[1]}|{PaddedNumber(ValueAfterEquals(parts[2]))}",
			_ => line,
		};
	}

	private static string PaddedNumber(string text) =>
		long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
			? value.ToString("D12", CultureInfo.InvariantCulture)
			: text;

	private static string ValueAfterEquals(string text)
	{
		var separator = text.IndexOf('=');
		return separator >= 0 ? text[(separator + 1)..] : "0";
	}

	private static string KindOf(string line)
	{
		var separator = line.IndexOf('|');
		return separator >= 0 ? line[..separator] : line;
	}

	private static Dictionary<string, string> ParseEntries(string text)
	{
		var entries = new Dictionary<string, string>(StringComparer.Ordinal);

		foreach (var raw in text.Split('\n'))
		{
			var line = raw.TrimEnd('\r');
			if (line.Length == 0
				|| line.StartsWith('#')
				|| line.StartsWith("*REMOVED*", StringComparison.Ordinal))
			{
				continue;
			}

			entries[KeyOf(line)] = line;
		}

		return entries;
	}

	private static string KeyOf(string line)
	{
		var parts = line.Split('|');
		return parts[0] switch
		{
			"id" => "id|" + BeforeEquals(parts[1]),
			"enum" => "enum|" + parts[1] + "|" + BeforeEquals(parts[2]),
			"member" => "member|" + parts[1] + "|" + parts[2],
			_ => line,
		};
	}

	private static string BeforeEquals(string text)
	{
		var separator = text.IndexOf('=');
		return separator >= 0 ? text[..separator] : text;
	}

	private static SyntaxNode Parse(string source) =>
		CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
}
