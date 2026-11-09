using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Patching;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The aggregated adapter capability report: every capability with its class,
/// its contract count and its failure reason, plus the game-assembly probe
/// result. It is a projection of facts that already existed separately
/// (<c>PatchInventory</c>'s contracts, the dynamic patch targets, the declared
/// game members, <c>ProbeGame</c>'s type reads) — no installation decision is
/// taken here beyond <see cref="RefusesInstall"/>, which is the same
/// all-or-nothing rule the adapter applied before.
/// </summary>
internal sealed class AdapterCapabilityReport
{
	internal AdapterCapabilityReport(
		string gameProbe,
		int patchContracts,
		int dynamicContracts,
		IReadOnlyList<AdapterCapabilityStatus> statuses,
		IReadOnlyList<PatchVerificationFailure> unmappedFailures)
	{
		GameProbe = gameProbe;
		PatchContracts = patchContracts;
		DynamicContracts = dynamicContracts;
		Statuses = statuses;
		UnmappedFailures = unmappedFailures;
	}

	/// <summary>The game-assembly probe's own line (the four types <c>ProbeGame</c> reads).</summary>
	internal string GameProbe { get; }

	/// <summary>Attributed patch contract rows the report accounted for.</summary>
	internal int PatchContracts { get; }

	/// <summary>Hand-declared dynamic contract rows the report accounted for.</summary>
	internal int DynamicContracts { get; }

	internal IReadOnlyList<AdapterCapabilityStatus> Statuses { get; }

	/// <summary>
	/// Hook failures no catalog entry claims. A catalog gap must never make a
	/// broken hook disappear from the report, so these are printed as their own
	/// section instead of being dropped (the totality gate keeps the section
	/// empty in a healthy tree).
	/// </summary>
	internal IReadOnlyList<PatchVerificationFailure> UnmappedFailures { get; }

	internal int ContractCount => PatchContracts + DynamicContracts;

	/// <summary>
	/// Stage 2's verdict: a REQUIRED capability's blocking hook failure refuses
	/// the session; an OPTIONAL one degrades alone (the report names it and the
	/// session runs without it). A failure no capability claims keeps the
	/// as-a-whole rule — a catalog gap is a bug, not a degradation. Computed by
	/// the same rule the install gate applies through the catalog's owner lookup,
	/// so the printed verdict and the gate's decision cannot diverge.
	/// </summary>
	internal bool RefusesSession =>
		RefusesInstall([.. RequiredFailures]) || UnmappedFailures.Any(failure => failure.BlocksInstall);

	/// <summary>The capabilities that installed as off: OPTIONAL rows with a blocking failure.</summary>
	internal IReadOnlyList<AdapterCapabilityStatus> Degraded =>
		[.. Statuses.Where(status => status.Kind == AdapterCapabilityKind.Optional
			&& status.Failures.Any(failure => failure.BlocksInstall))];

	/// <summary>
	/// The install gate's decision in one place: called with the contract
	/// failures the resolver found, it answers exactly what
	/// <c>missing.Count &gt; 0</c> answered before, and stage 2 changes this
	/// rule (not its callers) when the Optional class starts degrading alone.
	/// </summary>
	internal static bool RefusesInstall(IReadOnlyList<PatchVerificationFailure> failures) =>
		failures.Any(failure => failure.BlocksInstall);

	/// <summary>The whole report as one text block — what the startup log prints.</summary>
	internal string Render()
	{
		var lines = new List<string>
		{
			$"{Statuses.Count} capability(ies), {PatchContracts} patch contract(s) + {DynamicContracts} dynamic row(s); game probe: {GameProbe}",
		};
		lines.AddRange(Statuses.Select(status => status.Describe()));
		lines.Add(RefusesSession
			? "verdict: session REFUSED — a REQUIRED capability's hook failed (stage 2: an OPTIONAL failure degrades alone)"
			: Degraded.Count > 0
				? $"verdict: session available — {Degraded.Count} OPTIONAL capability(ies) installed off: {string.Join(", ", Degraded.Select(status => status.Id))}"
				: "verdict: session available — no blocking hook failure");
		if (UnmappedFailures.Count > 0)
		{
			lines.Add($"failures no capability claims ({UnmappedFailures.Count}) — a catalog gap, or a failure that belongs to no single capability:");
			lines.AddRange(UnmappedFailures.Select(failure => $"    - {failure.PatchClass}: {failure.Detail}"));
		}

		return string.Join("\n", lines);
	}

	private IEnumerable<PatchVerificationFailure> RequiredFailures =>
		Statuses.Where(status => status.Kind != AdapterCapabilityKind.Optional)
			.SelectMany(status => status.Failures);
}
