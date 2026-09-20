using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;

namespace CasualtiesUnknownOnline.ContractTool.Snapshot;

/// <summary>
/// Reads one game-assembly build into a <see cref="SnapshotDocument"/> as
/// METADATA: Mono.Cecil parses the file, nothing is loaded, no type is
/// initialized and no dependency is resolved. That is what makes the tool usable
/// on a build you have not launched (a rollback copy, an incoming update) and
/// what keeps it out of the plugin's dependency graph.
///
/// Everything the snapshot stores is sorted here, once: types by canonical name,
/// members by name and shape, enum members by name. The writer then only emits;
/// it never decides an order, which is why the artifact is byte-reproducible.
/// </summary>
public static class GameAssemblyReader
{
	private const string CompilerGeneratedAttribute = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";
	private const string SerializeFieldAttribute = "UnityEngine.SerializeField";
	private const string NonSerializedAttribute = "System.NonSerializedAttribute";
	private const string EnumValueField = "value__";

	/// <summary>The visibility strings ordered least to most visible (a property reports its most visible accessor).</summary>
	private static readonly string[] VisibilityOrder = ["private", "private protected", "internal", "protected", "protected internal", "public"];

	/// <summary>
	/// Snapshots <paramref name="assemblyPath"/>. When <paramref name="adapterPath"/>
	/// is given, the patch-target contract rows are read from that adapter
	/// assembly's metadata and embedded in the same document, so a report's rows
	/// and the standalone snapshot cannot drift apart.
	/// </summary>
	public static SnapshotDocument Read(string assemblyPath, string? adapterPath = null)
	{
		var fullPath = Path.GetFullPath(assemblyPath);
		if (!File.Exists(fullPath))
		{
			throw new FileNotFoundException($"game assembly not found: {fullPath}", fullPath);
		}

		using var assembly = AssemblyDefinition.ReadAssembly(fullPath, new ReaderParameters { InMemory = true });
		var types = Flatten(assembly.MainModule.Types)
			.Where(type => type.Name != "<Module>")
			.Select(ToSnapshotType)
			.OrderBy(type => type.Name, StringComparer.Ordinal)
			.ToList();

		var contracts = adapterPath is null ? [] : PatchContractReader.Read(adapterPath);
		if (adapterPath is not null && contracts.Count == 0)
		{
			// A silently empty contract list would make every contract verdict in
			// the report wrong; a wrong --adapter path must fail here, not there.
			throw new InvalidOperationException($"no [HarmonyPatch] contract was read from '{Path.GetFullPath(adapterPath)}' — check the adapter path");
		}

		var identity = new AssemblyIdentity(
			assembly.Name.Name,
			assembly.Name.Version.ToString(),
			assembly.MainModule.Mvid.ToString("D"),
			Sha256(fullPath));

		return new SnapshotDocument(SnapshotSchema.Snapshot, identity, Count(types, contracts), types, contracts);
	}

	private static IEnumerable<TypeDefinition> Flatten(IEnumerable<TypeDefinition> types)
	{
		foreach (var type in types)
		{
			yield return type;
			foreach (var nested in Flatten(type.NestedTypes))
			{
				yield return nested;
			}
		}
	}

	private static SnapshotType ToSnapshotType(TypeDefinition type)
	{
		var methods = type.Methods
			.Select(ToSnapshotMethod)
			.OrderBy(MemberShape.Signature, StringComparer.Ordinal)
			.ToList();
		var fields = type.Fields
			.Where(field => !(type.IsEnum && (field.IsLiteral || field.Name == EnumValueField)))
			.Select(ToSnapshotField)
			.OrderBy(field => field.Name, StringComparer.Ordinal)
			.ToList();
		var properties = type.Properties
			.Select(ToSnapshotProperty)
			.OrderBy(property => property.Name, StringComparer.Ordinal)
			.ToList();
		var enumMembers = type.IsEnum
			? type.Fields.Where(field => field.IsLiteral).Select(ToEnumMember).OrderBy(member => member.Name, StringComparer.Ordinal).ToList()
			: [];

		return new SnapshotType(
			TypeNameFormat.Of(type),
			KindOf(type),
			VisibilityOf(type),
			type.BaseType is null ? null : TypeNameFormat.Of(type.BaseType),
			methods,
			fields,
			properties,
			enumMembers);
	}

	private static SnapshotMethod ToSnapshotMethod(MethodDefinition method) => new(
		method.Name,
		VisibilityOf(method),
		method.IsStatic,
		method.IsAbstract,
		method.IsVirtual,
		HasAttribute(method, CompilerGeneratedAttribute),
		[.. method.GenericParameters.Select(parameter => parameter.Name)],
		TypeNameFormat.Of(method.ReturnType),
		[.. method.Parameters.Select(parameter => new SnapshotParameter(parameter.Name ?? string.Empty, TypeNameFormat.Of(parameter.ParameterType)))]);

	private static SnapshotField ToSnapshotField(FieldDefinition field) => new(
		field.Name,
		VisibilityOf(field),
		field.IsStatic,
		field.IsLiteral,
		field.IsInitOnly,
		IsSerialized(field),
		TypeNameFormat.Of(field.FieldType));

	private static SnapshotProperty ToSnapshotProperty(PropertyDefinition property) => new(
		property.Name,
		MostVisible(new[] { property.GetMethod, property.SetMethod }.Where(accessor => accessor is not null).Select(accessor => VisibilityOf(accessor!))),
		(property.GetMethod ?? property.SetMethod)?.IsStatic ?? false,
		property.GetMethod is not null,
		property.SetMethod is not null,
		TypeNameFormat.Of(property.PropertyType));

	private static SnapshotEnumMember ToEnumMember(FieldDefinition field) => new(
		field.Name,
		field.Constant is { } value ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "?" : "?");

	/// <summary>
	/// The Unity serialization rule this tool applies: a field the engine would
	/// put into a scene/save — non-static, non-literal, and either public or
	/// explicitly <c>[SerializeField]</c> — minus anything marked
	/// <c>[NonSerialized]</c>. Readonly fields are kept (Unity's own behavior
	/// around them is version-dependent, so the tool records the field rather
	/// than deciding for the engine).
	/// </summary>
	private static bool IsSerialized(FieldDefinition field) =>
		!field.IsStatic
		&& !field.IsLiteral
		&& (field.IsPublic || HasAttribute(field, SerializeFieldAttribute))
		&& !HasAttribute(field, NonSerializedAttribute);

	private static bool HasAttribute(ICustomAttributeProvider provider, string attributeFullName) =>
		provider.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == attributeFullName);

	private static string KindOf(TypeDefinition type)
	{
		if (type.IsEnum)
		{
			return "enum";
		}

		if (type.IsInterface)
		{
			return "interface";
		}

		if (type.IsValueType)
		{
			return "struct";
		}

		return type.BaseType?.FullName == "System.MulticastDelegate" ? "delegate" : "class";
	}

	private static string VisibilityOf(TypeDefinition type)
	{
		if (!type.IsNested)
		{
			return type.IsPublic ? "public" : "internal";
		}

		return (type.Attributes & TypeAttributes.VisibilityMask) switch
		{
			TypeAttributes.NestedPublic => "public",
			TypeAttributes.NestedPrivate => "private",
			TypeAttributes.NestedFamily => "protected",
			TypeAttributes.NestedAssembly => "internal",
			TypeAttributes.NestedFamORAssem => "protected internal",
			TypeAttributes.NestedFamANDAssem => "private protected",
			_ => "internal",
		};
	}

	private static string VisibilityOf(MethodDefinition method)
	{
		if (method.IsPublic)
		{
			return "public";
		}

		if (method.IsFamilyOrAssembly)
		{
			return "protected internal";
		}

		if (method.IsFamilyAndAssembly)
		{
			return "private protected";
		}

		if (method.IsFamily)
		{
			return "protected";
		}

		return method.IsAssembly ? "internal" : "private";
	}

	private static string VisibilityOf(FieldDefinition field)
	{
		if (field.IsPublic)
		{
			return "public";
		}

		if (field.IsFamilyOrAssembly)
		{
			return "protected internal";
		}

		if (field.IsFamilyAndAssembly)
		{
			return "private protected";
		}

		if (field.IsFamily)
		{
			return "protected";
		}

		return field.IsAssembly ? "internal" : "private";
	}

	private static string MostVisible(IEnumerable<string> visibilities)
	{
		var ordered = visibilities.OrderBy(visibility => Array.IndexOf(VisibilityOrder, visibility)).ToList();
		return ordered.Count == 0 ? "?" : ordered[ordered.Count - 1];
	}

	private static SnapshotCounts Count(IReadOnlyList<SnapshotType> types, IReadOnlyList<SnapshotContract> contracts) => new(
		types.Count,
		types.Sum(type => type.Methods.Count),
		types.Sum(type => type.Fields.Count),
		types.Sum(type => type.Properties.Count),
		types.Sum(type => type.EnumMembers.Count),
		types.Sum(type => type.Fields.Count(field => field.IsSerialized)),
		contracts.Count);

	private static string Sha256(string path)
	{
		using var stream = File.OpenRead(path);
		using var algorithm = SHA256.Create();
		return string.Concat(algorithm.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
	}
}
