using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.ContractTool.Snapshot;

namespace CasualtiesUnknownOnline.Tests.ContractTool;

/// <summary>
/// Builds snapshot documents in memory for the classification tests. The
/// classification is the deliverable, so the tests that pin it drive the differ
/// with explicit before/after snapshots rather than with compiled assemblies —
/// the compiled pair lives in <see cref="ContractFixtureAssemblies"/> and covers
/// the extraction half end to end.
/// </summary>
internal static class ContractSnapshotBuilder
{
	internal static SnapshotDocument Document(IReadOnlyList<SnapshotType> types, IReadOnlyList<SnapshotContract>? contracts = null)
	{
		var rows = contracts ?? [];
		return new SnapshotDocument(SnapshotSchema.Snapshot, Identity(), Counts(types, rows), types, rows);
	}

	internal static SnapshotType Type(
		string name,
		string kind = "class",
		IReadOnlyList<SnapshotMethod>? methods = null,
		IReadOnlyList<SnapshotField>? fields = null,
		IReadOnlyList<SnapshotProperty>? properties = null,
		IReadOnlyList<SnapshotEnumMember>? enumMembers = null) =>
		new(name, kind, "public", null, methods ?? [], fields ?? [], properties ?? [], enumMembers ?? []);

	internal static SnapshotMethod Method(string name, string returns = "System.Void", params string[] parameterTypes) =>
		new(
			name,
			"public",
			true,
			false,
			false,
			false,
			[],
			returns,
			parameterTypes.Select((type, index) => new SnapshotParameter($"p{index}", type)).ToList());

	internal static SnapshotMethod MethodWithParameters(string name, string returns, params SnapshotParameter[] parameters) =>
		new(name, "public", true, false, false, false, [], returns, parameters);

	internal static SnapshotParameter Parameter(string name, string type) => new(name, type);

	internal static SnapshotField Field(string name, string type, string visibility = "public", bool isSerialized = true) =>
		new(name, visibility, false, false, false, isSerialized, type);

	internal static SnapshotEnumMember EnumMember(string name, string value) => new(name, value);

	internal static SnapshotContract Contract(
		string patchClass,
		string targetType,
		string method,
		IReadOnlyList<string>? argumentTypes = null,
		IReadOnlyList<string>? patchParameters = null,
		string? patchClassType = null) =>
		new(patchClass, patchClassType ?? patchClass, targetType, method, argumentTypes ?? [], patchParameters ?? []);

	private static AssemblyIdentity Identity() =>
		new("ContractFixture.Game", "1.0.0.0", "00000000-0000-0000-0000-000000000000", new string('0', 64));

	private static SnapshotCounts Counts(IReadOnlyList<SnapshotType> types, IReadOnlyList<SnapshotContract> contracts) => new(
		types.Count,
		types.Sum(type => type.Methods.Count),
		types.Sum(type => type.Fields.Count),
		types.Sum(type => type.Properties.Count),
		types.Sum(type => type.EnumMembers.Count),
		types.Sum(type => type.Fields.Count(field => field.IsSerialized)),
		contracts.Count);
}
