using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.ContractTool.Snapshot;

namespace CasualtiesUnknownOnline.ContractTool.Diff;

/// <summary>
/// Compares two builds' snapshots and classifies every difference.
///
/// The contract lens runs FIRST and authoritatively: each patch-target contract
/// row gets exactly one verdict (broken, ambiguous, or structurally unchanged),
/// and the members that verdict already covered are then skipped by the
/// member-level pass so a reader never sees the same hook twice. Everything else
/// is still compared — a game update must not be able to hide a change by
/// sitting outside the lens — but it is reported by tier, not turned into hook
/// work it is not.
///
/// The verdict rules mirror the runtime's own guard so a report and
/// <c>PatchInventory.VerifyMissing</c> cannot disagree about what "broken" means:
/// a target that is gone, argument types that no longer match exactly (never a
/// name-only fallback), an unconstrained target that gained an overload, and a
/// patch parameter name the target no longer carries (Harmony binds by name).
/// </summary>
public static class SnapshotDiffer
{
	/// <summary>Compares <paramref name="previous"/> with <paramref name="current"/>.</summary>
	public static DiffResult Diff(SnapshotDocument previous, SnapshotDocument current)
	{
		EnsureSameContractSet(previous, current);

		var previousTypes = Index(previous.Types);
		var currentTypes = Index(current.Types);
		var lens = new HashSet<string>(previous.Contracts.Select(contract => contract.TargetType), StringComparer.Ordinal);
		var contractTargets = new HashSet<string>(StringComparer.Ordinal);
		var differences = new List<ContractDifference>();

		var (previousLens, currentLens) = AppendContractVerdicts(previous, previousTypes, currentTypes, contractTargets, differences);
		AppendTypeDifferences(previousTypes, currentTypes, lens, contractTargets, differences);

		var sorted = differences
			.OrderBy(difference => DifferenceVocabulary.Order(difference.Scope))
			.ThenBy(difference => DifferenceVocabulary.Order(difference.Kind))
			.ThenBy(difference => difference.Subject, StringComparer.Ordinal)
			.ThenBy(difference => difference.Detail, StringComparer.Ordinal)
			.ThenBy(difference => difference.Previous, StringComparer.Ordinal)
			.ThenBy(difference => difference.Current, StringComparer.Ordinal)
			.ToList();

		return new DiffResult(previous, current, sorted, previousLens, currentLens);
	}

	/// <summary>
	/// Both builds must be snapshotted with the same contract set, or the verdicts
	/// would compare one adapter's hooks against another adapter's targets.
	/// </summary>
	private static void EnsureSameContractSet(SnapshotDocument previous, SnapshotDocument current)
	{
		var same = previous.Contracts.Count == current.Contracts.Count
			&& previous.Contracts.Zip(current.Contracts, (before, after) => before.HasSameFactsAs(after)).All(equal => equal);
		if (!same)
		{
			throw new InvalidOperationException("the two snapshots carry different contract sets — snapshot both builds with the same adapter build");
		}
	}

	private static Dictionary<string, SnapshotType> Index(IReadOnlyList<SnapshotType> types)
	{
		var index = new Dictionary<string, SnapshotType>(StringComparer.Ordinal);
		foreach (var type in types)
		{
			index[type.Name] = type;
		}

		return index;
	}

	private static (ContractLens Previous, ContractLens Current) AppendContractVerdicts(
		SnapshotDocument previous,
		IReadOnlyDictionary<string, SnapshotType> previousTypes,
		IReadOnlyDictionary<string, SnapshotType> currentTypes,
		ISet<string> contractTargets,
		List<ContractDifference> differences)
	{
		int previousResolved = 0, previousAmbiguous = 0, previousUnresolved = 0;
		int currentResolved = 0, currentAmbiguous = 0, currentUnresolved = 0;

		foreach (var contract in previous.Contracts)
		{
			var before = Resolve(previousTypes, contract);
			var after = Resolve(currentTypes, contract);
			Count(before, ref previousResolved, ref previousAmbiguous, ref previousUnresolved);
			Count(after, ref currentResolved, ref currentAmbiguous, ref currentUnresolved);

			if (before.Method is null)
			{
				// Already unresolvable before this update: not a difference between the
				// two builds, and NOT a reason to suppress the member-level comparison —
				// a target that only becomes resolvable now is exactly the case where the
				// change underneath has to stay visible. The lens census keeps the
				// unresolvable row visible and the contract tests own its verdict.
				continue;
			}

			// A verdict IS emitted for this contract below, so its target rows belong to
			// the contract pass; the member-level pass skips them rather than doubling the
			// same hook.
			contractTargets.Add(contract.TargetType + "|" + MemberDiffer.Key(before.Method));
			if (after.Method is not null)
			{
				contractTargets.Add(contract.TargetType + "|" + MemberDiffer.Key(after.Method));
			}

			// The namespaced patch-class name identifies the row: 205 contracts share 201
			// simple names, so the simple name alone prints an ambiguous report line.
			var patchClass = string.IsNullOrEmpty(contract.PatchClassType) ? contract.PatchClass : contract.PatchClassType;
			var subject = $"{patchClass} → {contract.TargetType}.{contract.Method}";
			var beforeShape = MemberShape.Signature(before.Method);

			if (!after.TypeExists)
			{
				differences.Add(new ContractDifference(
					DifferenceKind.RemovedOrRenamed,
					DifferenceScope.Contract,
					subject,
					beforeShape,
					"target type missing",
					$"the target type {contract.TargetType} is gone — this hook would not install"));
				continue;
			}

			if (after.Method is null)
			{
				if (contract.ArgumentTypes.Count > 0)
				{
					differences.Add(new ContractDifference(
						DifferenceKind.SignatureChanged,
						DifferenceScope.Contract,
						subject,
						beforeShape,
						Shapes(after.SameName),
						$"no overload matches the declared argument types [{string.Join(", ", contract.ArgumentTypes)}]; the build has: {Shapes(after.SameName)}"));
				}
				else if (after.SameNameCount > 1)
				{
					differences.Add(new ContractDifference(
						DifferenceKind.HarmonyTargetAmbiguous,
						DifferenceScope.Contract,
						subject,
						beforeShape,
						$"{after.SameNameCount} overloads",
						"unconstrained [HarmonyPatch] target gained an overload (was 1); Harmony would bind to an arbitrary one — declare argumentTypes"));
				}
				else
				{
					differences.Add(new ContractDifference(
						DifferenceKind.RemovedOrRenamed,
						DifferenceScope.Contract,
						subject,
						beforeShape,
						"method missing",
						$"the target method {contract.TargetType}.{contract.Method} is gone — this hook would not install"));
				}

				continue;
			}

			var method = after.Method;
			var moved = CompareTarget(before.Method, method, contract);
			if (moved.Count > 0)
			{
				differences.Add(new ContractDifference(
					DifferenceKind.SignatureChanged,
					DifferenceScope.Contract,
					subject,
					beforeShape,
					MemberShape.Signature(method),
					string.Join("; ", moved)));
				continue;
			}

			differences.Add(new ContractDifference(
				DifferenceKind.UnchangedNeedsReview,
				DifferenceScope.Contract,
				subject,
				beforeShape,
				MemberShape.Signature(method),
				"structurally identical in both builds; whether it still MEANS the same thing needs the live game"));
		}

		return (new ContractLens(previousResolved, previousAmbiguous, previousUnresolved), new ContractLens(currentResolved, currentAmbiguous, currentUnresolved));
	}

	private static List<string> CompareTarget(SnapshotMethod previous, SnapshotMethod current, SnapshotContract contract)
	{
		// The member comparison is the ONE shape comparison in the tool: it covers the
		// parameter TYPES a name-resolved target can move without the runtime guard
		// noticing, plus returns/visibility/flags/parameter names.
		var details = MemberDiffer.Compare(previous, current);
		var targetNames = new HashSet<string>(current.Parameters.Select(parameter => parameter.Name), StringComparer.Ordinal);
		details.AddRange(contract.PatchParameters
			.Where(name => !targetNames.Contains(name))
			.Select(name => $"patch parameter '{name}' is missing from the target (renamed?) — Harmony binds patch arguments by name"));
		return details;
	}

	private static void AppendTypeDifferences(
		IReadOnlyDictionary<string, SnapshotType> previousTypes,
		IReadOnlyDictionary<string, SnapshotType> currentTypes,
		ISet<string> lens,
		ISet<string> contractTargets,
		List<ContractDifference> differences)
	{
		foreach (var entry in previousTypes)
		{
			var name = entry.Key;
			var previousType = entry.Value;
			var scope = lens.Contains(name) ? DifferenceScope.ContractAdjacent : DifferenceScope.OutsideContract;
			if (!currentTypes.TryGetValue(name, out var currentType))
			{
				differences.Add(new ContractDifference(
					DifferenceKind.RemovedOrRenamed,
					scope,
					name,
					Describe(previousType),
					"missing",
					RenameCandidates(previousType, previousTypes, currentTypes)));
				continue;
			}

			if (previousType.Kind != currentType.Kind || previousType.BaseType != currentType.BaseType)
			{
				differences.Add(new ContractDifference(
					DifferenceKind.SignatureChanged,
					scope,
					name,
					Describe(previousType),
					Describe(currentType),
					$"type shape: kind {previousType.Kind} → {currentType.Kind}, base {previousType.BaseType ?? "(none)"} → {currentType.BaseType ?? "(none)"}"));
			}

			MemberDiffer.Append(previousType, currentType, scope, contractTargets, differences);
		}

		foreach (var entry in currentTypes)
		{
			var name = entry.Key;
			var currentType = entry.Value;
			if (!previousTypes.ContainsKey(name))
			{
				differences.Add(new ContractDifference(
					DifferenceKind.Added,
					lens.Contains(name) ? DifferenceScope.ContractAdjacent : DifferenceScope.OutsideContract,
					name,
					"missing",
					Describe(currentType),
					"type added"));
			}
		}
	}

	private static Resolution Resolve(IReadOnlyDictionary<string, SnapshotType> types, SnapshotContract contract)
	{
		if (!types.TryGetValue(contract.TargetType, out var type))
		{
			return new Resolution(null, [], false);
		}

		var sameName = type.Methods.Where(method => method.Name == contract.Method).ToList();
		if (contract.ArgumentTypes.Count == 0)
		{
			return new Resolution(sameName.Count == 1 ? sameName[0] : null, sameName, true);
		}

		var exact = sameName.FirstOrDefault(method =>
			method.Parameters.Count == contract.ArgumentTypes.Count
			&& method.Parameters.Select(parameter => parameter.Type).SequenceEqual(contract.ArgumentTypes, StringComparer.Ordinal));
		return new Resolution(exact, sameName, true);
	}

	private static void Count(Resolution resolution, ref int resolved, ref int ambiguous, ref int unresolved)
	{
		if (resolution.Method is not null)
		{
			resolved++;
		}
		else if (resolution.TypeExists && resolution.SameName.Count > 1)
		{
			ambiguous++;
		}
		else
		{
			unresolved++;
		}
	}

	private static string Shapes(IReadOnlyList<SnapshotMethod> sameName) => sameName.Count == 0
		? "no method of that name"
		: string.Join(", ", sameName.Select(MemberShape.Signature));

	private static string RenameCandidates(
		SnapshotType removed,
		IReadOnlyDictionary<string, SnapshotType> previousTypes,
		IReadOnlyDictionary<string, SnapshotType> currentTypes)
	{
		var fingerprint = Fingerprint(removed);
		var simpleName = SimpleName(removed.Name);
		var candidates = currentTypes
			.Where(pair => !previousTypes.ContainsKey(pair.Key))
			.Where(pair => Fingerprint(pair.Value) == fingerprint || SimpleName(pair.Key) == simpleName)
			.Select(pair => pair.Key)
			.OrderBy(name => name, StringComparer.Ordinal)
			.ToList();
		return candidates.Count == 0
			? "no same-shape type appeared — treat as a removal"
			: "rename candidate: " + string.Join(", ", candidates);
	}

	private static string Fingerprint(SnapshotType type)
	{
		var parts = new List<string>();
		parts.AddRange(type.Methods.Select(method => "m:" + MemberShape.Signature(method) + "`" + method.GenericParameters.Count));
		parts.AddRange(type.Fields.Select(field => "f:" + field.Name + ":" + field.Type));
		parts.AddRange(type.Properties.Select(property => "p:" + property.Name + ":" + property.Type));
		parts.AddRange(type.EnumMembers.Select(member => "e:" + member.Name + "=" + member.Value));
		parts.Sort(StringComparer.Ordinal);
		return string.Join("|", parts);
	}

	private static string SimpleName(string canonicalName)
	{
		var nested = canonicalName.LastIndexOf('+');
		var dot = canonicalName.LastIndexOf('.');
		var start = Math.Max(nested, dot);
		return start < 0 ? canonicalName : canonicalName.Substring(start + 1);
	}

	private static string Describe(SnapshotType type) => $"{type.Kind} {type.Name}";

	/// <summary>How one contract row resolved against one build: the matched method (when there is exactly one), every same-name method, and whether the target type exists at all.</summary>
	private sealed record Resolution(SnapshotMethod? Method, IReadOnlyList<SnapshotMethod> SameName, bool TypeExists)
	{
		internal int SameNameCount => SameName.Count;
	}
}
