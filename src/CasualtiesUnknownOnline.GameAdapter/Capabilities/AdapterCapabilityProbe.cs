using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Patching;

namespace CasualtiesUnknownOnline.GameAdapter.Capabilities;

/// <summary>
/// Builds the aggregated capability report (ticket stage 1) out of facts that
/// already existed separately: <c>PatchInventory</c>'s contract rows, the hook
/// failures the install verification found, what the dynamic installer reported,
/// the declared game members, and <c>ProbeGame</c>'s type probe. Nothing here
/// decides whether to install — the report is a projection, and the one decision
/// it does expose (<see cref="AdapterCapabilityReport.RefusesInstall"/>) is the
/// all-or-nothing rule the adapter always applied.
/// </summary>
internal static class AdapterCapabilityProbe
{
	/// <summary>
	/// One probe run over the whole catalog. <paramref name="failures"/> are the
	/// hook failures the install attempt produced — the attributed contract
	/// failures <c>PatchInventory.VerifyMissing</c> returns and what the dynamic
	/// installer could not bind, each row carrying whether the gate counts it —
	/// and <paramref name="gameProbe"/> is the game-assembly probe's own line.
	/// </summary>
	internal static AdapterCapabilityReport Build(
		IReadOnlyList<PatchVerificationFailure> failures,
		string gameProbe)
	{
		var unmapped = new List<PatchVerificationFailure>();
		var rowsPerCapability = new Dictionary<string, int>(StringComparer.Ordinal);
		var patchRows = 0;
		var dynamicRows = 0;

		foreach (var contract in PatchInventory.BuildContracts())
		{
			var dynamic = PatchInventory.IsDynamic(contract);
			var owner = dynamic
				? AdapterCapabilityCatalog.OwnerOfDynamicPatchClass(contract.PatchClassType)
				: AdapterCapabilityCatalog.OwnerOfPatchClass(contract.PatchClassType);
			if (owner is null)
			{
				unmapped.Add(new PatchVerificationFailure(
					contract.PatchClass,
					contract.PatchClassType,
					$"contract row {contract.PatchClass} → {contract.TargetType}.{contract.MethodName} belongs to no capability",
					blocksInstall: false));
				continue;
			}

			rowsPerCapability[owner] = (rowsPerCapability.TryGetValue(owner, out var current) ? current : 0) + 1;
			if (dynamic)
			{
				dynamicRows++;
			}
			else
			{
				patchRows++;
			}
		}

		var failuresPerCapability = new Dictionary<string, List<PatchVerificationFailure>>(StringComparer.Ordinal);
		foreach (var failure in failures)
		{
			var owner = AdapterCapabilityCatalog.OwnerOfPatchClass(failure.PatchClassType)
				?? AdapterCapabilityCatalog.OwnerOfDynamicPatchClass(failure.PatchClassType);
			if (owner is null)
			{
				unmapped.Add(failure);
				continue;
			}

			if (!failuresPerCapability.TryGetValue(owner, out var capabilityFailures))
			{
				capabilityFailures = [];
				failuresPerCapability[owner] = capabilityFailures;
			}

			capabilityFailures.Add(failure);
		}

		var statuses = AdapterCapabilityCatalog.All
			.Select(definition => new AdapterCapabilityStatus(
				definition.Id,
				definition.Title,
				definition.Kind,
				rowsPerCapability.TryGetValue(definition.Id, out var rows) ? rows : 0,
				failuresPerCapability.TryGetValue(definition.Id, out var capabilityFailures) ? capabilityFailures : [],
				[.. definition.GameMembers.Where(member => !member.Resolves()).Select(member => $"member {member.Describe()} not found")]))
			.ToList();

		return new AdapterCapabilityReport(gameProbe, patchRows, dynamicRows, statuses, unmapped);
	}
}
