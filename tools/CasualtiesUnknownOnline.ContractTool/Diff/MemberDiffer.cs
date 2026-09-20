using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CasualtiesUnknownOnline.ContractTool.Snapshot;

namespace CasualtiesUnknownOnline.ContractTool.Diff;

/// <summary>
/// The member-level half of the comparison: two types that exist in both builds,
/// compared method by method, field by field, property by property and enum
/// member by enum member.
///
/// Matching rules that matter:
///   - methods match on NAME plus parameter types, never on the return type (a
///     return type cannot overload, so a return-type move must surface as a
///     signature change, not as a removal plus an addition);
///   - a removed member is offered the new members with the same shape as RENAME
///     CANDIDATES — metadata cannot see a rename, and naming the pair is the most
///     a structural tool may honestly say;
///   - a member that is a patch-target contract target is SKIPPED here: the
///     contract verdict pass already reported it, and reporting it twice would
///     double the hook work a reader sees.
/// </summary>
public static class MemberDiffer
{
	/// <summary>Appends every member difference between two builds of one type.</summary>
	public static void Append(
		SnapshotType previous,
		SnapshotType current,
		DifferenceScope scope,
		ISet<string> contractTargets,
		List<ContractDifference> differences)
	{
		AppendMethods(previous, current, scope, contractTargets, differences);
		AppendFields(previous, current, scope, differences);
		AppendProperties(previous, current, scope, differences);
		AppendEnumMembers(previous, current, scope, differences);
	}

	/// <summary>The key a method is matched on (name, parameter types, generic arity).</summary>
	public static string Key(SnapshotMethod method) => MemberShape.Identity(method) + "`" + method.GenericParameters.Count.ToString(CultureInfo.InvariantCulture);

	private static void AppendMethods(
		SnapshotType previous,
		SnapshotType current,
		DifferenceScope scope,
		ISet<string> contractTargets,
		List<ContractDifference> differences)
	{
		var currentByKey = current.Methods.ToDictionary(Key, StringComparer.Ordinal);
		var matched = new HashSet<string>(StringComparer.Ordinal);
		var removed = new List<SnapshotMethod>();

		// Pass 1 — the same member: name, parameter types and generic arity all unchanged.
		foreach (var oldOne in previous.Methods)
		{
			var key = Key(oldOne);
			if (!currentByKey.TryGetValue(key, out var newOne))
			{
				continue;
			}

			matched.Add(key);
			if (contractTargets.Contains(previous.Name + "|" + key))
			{
				continue;
			}

			var details = Compare(oldOne, newOne);
			if (details.Count > 0)
			{
				differences.Add(new ContractDifference(
					DifferenceKind.SignatureChanged,
					scope,
					Subject(previous, oldOne),
					MemberShape.Signature(oldOne),
					MemberShape.Signature(newOne),
					string.Join("; ", details)));
			}
		}

		// Pass 2 — the same NAME with a moved signature. A parameter list that grew,
		// shrank or re-typed is the member still being there, so it must read as a
		// signature change, not as a removal plus an unrelated addition.
		foreach (var oldOne in previous.Methods.Where(method => !matched.Contains(Key(method))))
		{
			var candidate = current.Methods.FirstOrDefault(method => method.Name == oldOne.Name && !matched.Contains(Key(method)));
			if (candidate is null)
			{
				removed.Add(oldOne);
				continue;
			}

			matched.Add(Key(candidate));
			if (contractTargets.Contains(previous.Name + "|" + Key(oldOne)) || contractTargets.Contains(previous.Name + "|" + Key(candidate)))
			{
				continue;
			}

			differences.Add(new ContractDifference(
				DifferenceKind.SignatureChanged,
				scope,
				Subject(previous, oldOne),
				MemberShape.Signature(oldOne),
				MemberShape.Signature(candidate),
				string.Join("; ", Compare(oldOne, candidate))));
		}

		// Pass 3 — what is really gone, and what is really new. A rename candidate must
		// be a member that was NOT there before: a same-shape sibling that exists in both
		// builds says nothing about a rename.
		var added = current.Methods.Where(method => !matched.Contains(Key(method))).ToList();
		foreach (var oldOne in removed)
		{
			if (contractTargets.Contains(previous.Name + "|" + Key(oldOne)))
			{
				// The contract verdict already reported this target as gone; a second row
				// under the same subject would double the hook work a reader sees.
				continue;
			}

			var shape = MemberShape.Shape(oldOne);
			var candidates = added
				.Where(method => method.Name != oldOne.Name && MemberShape.Shape(method) == shape)
				.Select(method => method.Name)
				.Distinct(StringComparer.Ordinal)
				.OrderBy(name => name, StringComparer.Ordinal)
				.ToList();
			differences.Add(new ContractDifference(
				DifferenceKind.RemovedOrRenamed,
				scope,
				Subject(previous, oldOne),
				MemberShape.Signature(oldOne),
				"missing",
				candidates.Count == 0
					? "no same-shape method appeared — treat as a removal"
					: "rename candidate: " + string.Join(", ", candidates)));
		}

		foreach (var newOne in added)
		{
			if (contractTargets.Contains(previous.Name + "|" + Key(newOne)))
			{
				continue;
			}

			differences.Add(new ContractDifference(
				DifferenceKind.Added,
				scope,
				Subject(previous, newOne),
				"missing",
				MemberShape.Signature(newOne),
				"method added"));
		}
	}

	/// <summary>The row subject: the type plus the member's identity (name and parameter types).</summary>
	private static string Subject(SnapshotType type, SnapshotMethod method) => type.Name + "." + MemberShape.Identity(method);

	/// <summary>
	/// Every way a resolved method can move. The parameter TYPES come first because a
	/// re-typed parameter is invisible to every name-based rule: an unconstrained
	/// [HarmonyPatch] target still resolves by NAME after the type moved, which is the
	/// one shape change the runtime guard accepts silently and a report must not.
	/// Parameter names are compared only when the arity is unchanged — otherwise the
	/// parameter-list detail already carries the whole story.
	/// </summary>
	public static List<string> Compare(SnapshotMethod previous, SnapshotMethod current)
	{
		var details = new List<string>();
		var previousParameters = MemberShape.Parameters(previous.Parameters);
		var currentParameters = MemberShape.Parameters(current.Parameters);
		if (previousParameters != currentParameters)
		{
			details.Add($"parameters: ({previousParameters}) → ({currentParameters})");
		}

		if (previous.GenericParameters.Count != current.GenericParameters.Count)
		{
			details.Add($"generic parameters: {previous.GenericParameters.Count} → {current.GenericParameters.Count}");
		}

		if (previous.Returns != current.Returns)
		{
			details.Add($"returns: {previous.Returns} → {current.Returns}");
		}

		if (previous.Visibility != current.Visibility)
		{
			details.Add($"visibility: {previous.Visibility} → {current.Visibility}");
		}

		if (previous.IsStatic != current.IsStatic)
		{
			details.Add($"static: {DifferenceVocabulary.Flag(previous.IsStatic)} → {DifferenceVocabulary.Flag(current.IsStatic)}");
		}

		if (previous.IsVirtual != current.IsVirtual)
		{
			details.Add($"virtual: {DifferenceVocabulary.Flag(previous.IsVirtual)} → {DifferenceVocabulary.Flag(current.IsVirtual)}");
		}

		if (previous.IsAbstract != current.IsAbstract)
		{
			details.Add($"abstract: {DifferenceVocabulary.Flag(previous.IsAbstract)} → {DifferenceVocabulary.Flag(current.IsAbstract)}");
		}

		if (previous.Parameters.Count == current.Parameters.Count)
		{
			details.AddRange(previous.Parameters
				.Zip(current.Parameters, (before, after) => (Before: before.Name, After: after.Name))
				.Where(pair => pair.Before != pair.After)
				.Select(pair => $"parameter name: {pair.Before} → {pair.After}"));
		}

		return details;
	}

	private static void AppendFields(SnapshotType previous, SnapshotType current, DifferenceScope scope, List<ContractDifference> differences)
	{
		var currentFields = current.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
		var previousFields = previous.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
		foreach (var entry in previousFields)
		{
			var name = entry.Key;
			var oldField = entry.Value;
			var subject = previous.Name + "." + name;
			if (!currentFields.TryGetValue(name, out var newField))
			{
				var candidates = current.Fields
					.Where(field => !previousFields.ContainsKey(field.Name) && field.Type == oldField.Type && field.IsStatic == oldField.IsStatic)
					.Select(field => field.Name)
					.OrderBy(candidate => candidate, StringComparer.Ordinal)
					.ToList();
				differences.Add(new ContractDifference(
					DifferenceKind.RemovedOrRenamed,
					scope,
					subject,
					Describe(oldField),
					"missing",
					candidates.Count > 0 ? "rename candidate: " + string.Join(", ", candidates) : "no same-type field appeared — treat as a removal"));
				continue;
			}

			var details = new List<string>();
			if (oldField.Type != newField.Type)
			{
				details.Add($"type: {oldField.Type} → {newField.Type}");
			}

			if (oldField.Visibility != newField.Visibility)
			{
				details.Add($"visibility: {oldField.Visibility} → {newField.Visibility}");
			}

			if (oldField.IsStatic != newField.IsStatic)
			{
				details.Add($"static: {DifferenceVocabulary.Flag(oldField.IsStatic)} → {DifferenceVocabulary.Flag(newField.IsStatic)}");
			}

			if (oldField.IsReadOnly != newField.IsReadOnly)
			{
				details.Add($"readonly: {DifferenceVocabulary.Flag(oldField.IsReadOnly)} → {DifferenceVocabulary.Flag(newField.IsReadOnly)}");
			}

			if (oldField.IsSerialized != newField.IsSerialized)
			{
				details.Add($"serialized: {DifferenceVocabulary.Flag(oldField.IsSerialized)} → {DifferenceVocabulary.Flag(newField.IsSerialized)}");
			}

			if (details.Count > 0)
			{
				differences.Add(new ContractDifference(DifferenceKind.FieldShapeChanged, scope, subject, Describe(oldField), Describe(newField), string.Join("; ", details)));
			}
		}

		foreach (var entry in currentFields)
		{
			if (!previousFields.ContainsKey(entry.Key))
			{
				differences.Add(new ContractDifference(DifferenceKind.Added, scope, previous.Name + "." + entry.Key, "missing", Describe(entry.Value), "field added"));
			}
		}
	}

	private static void AppendProperties(SnapshotType previous, SnapshotType current, DifferenceScope scope, List<ContractDifference> differences)
	{
		var currentProperties = current.Properties.ToDictionary(property => property.Name, StringComparer.Ordinal);
		var previousProperties = previous.Properties.ToDictionary(property => property.Name, StringComparer.Ordinal);
		foreach (var entry in previousProperties)
		{
			var name = entry.Key;
			var oldProperty = entry.Value;
			var subject = previous.Name + "." + name;
			if (!currentProperties.TryGetValue(name, out var newProperty))
			{
				differences.Add(new ContractDifference(DifferenceKind.RemovedOrRenamed, scope, subject, Describe(oldProperty), "missing", "property removed"));
				continue;
			}

			var details = new List<string>();
			if (oldProperty.Type != newProperty.Type)
			{
				details.Add($"type: {oldProperty.Type} → {newProperty.Type}");
			}

			if (oldProperty.Visibility != newProperty.Visibility)
			{
				details.Add($"visibility: {oldProperty.Visibility} → {newProperty.Visibility}");
			}

			if (oldProperty.IsStatic != newProperty.IsStatic)
			{
				details.Add($"static: {DifferenceVocabulary.Flag(oldProperty.IsStatic)} → {DifferenceVocabulary.Flag(newProperty.IsStatic)}");
			}

			if (oldProperty.HasGetter != newProperty.HasGetter)
			{
				details.Add($"getter: {DifferenceVocabulary.Flag(oldProperty.HasGetter)} → {DifferenceVocabulary.Flag(newProperty.HasGetter)}");
			}

			if (oldProperty.HasSetter != newProperty.HasSetter)
			{
				details.Add($"setter: {DifferenceVocabulary.Flag(oldProperty.HasSetter)} → {DifferenceVocabulary.Flag(newProperty.HasSetter)}");
			}

			if (details.Count > 0)
			{
				differences.Add(new ContractDifference(DifferenceKind.SignatureChanged, scope, subject, Describe(oldProperty), Describe(newProperty), string.Join("; ", details)));
			}
		}

		foreach (var entry in currentProperties)
		{
			if (!previousProperties.ContainsKey(entry.Key))
			{
				differences.Add(new ContractDifference(DifferenceKind.Added, scope, previous.Name + "." + entry.Key, "missing", Describe(entry.Value), "property added"));
			}
		}
	}

	private static void AppendEnumMembers(SnapshotType previous, SnapshotType current, DifferenceScope scope, List<ContractDifference> differences)
	{
		var currentMembers = current.EnumMembers.ToDictionary(member => member.Name, StringComparer.Ordinal);
		var previousMembers = previous.EnumMembers.ToDictionary(member => member.Name, StringComparer.Ordinal);
		foreach (var entry in previousMembers)
		{
			var name = entry.Key;
			var oldMember = entry.Value;
			var subject = previous.Name + "." + name;
			if (!currentMembers.TryGetValue(name, out var newMember))
			{
				differences.Add(new ContractDifference(DifferenceKind.RemovedOrRenamed, scope, subject, oldMember.Value, "missing", "enum member removed"));
				continue;
			}

			if (oldMember.Value != newMember.Value)
			{
				differences.Add(new ContractDifference(DifferenceKind.EnumValueChanged, scope, subject, oldMember.Value, newMember.Value, "enum member kept its name and changed its value"));
			}
		}

		foreach (var entry in currentMembers)
		{
			if (!previousMembers.ContainsKey(entry.Key))
			{
				differences.Add(new ContractDifference(DifferenceKind.Added, scope, previous.Name + "." + entry.Key, "missing", entry.Value.Value, "enum member added"));
			}
		}
	}

	private static string Describe(SnapshotField field) =>
		$"{field.Visibility}{(field.IsStatic ? " static" : string.Empty)}{(field.IsSerialized ? " serialized" : string.Empty)} {field.Type}";

	private static string Describe(SnapshotProperty property) =>
		$"{property.Visibility}{(property.IsStatic ? " static" : string.Empty)} {property.Type} {{ {(property.HasGetter ? "get; " : string.Empty)}{(property.HasSetter ? "set; " : string.Empty)}}}";
}
