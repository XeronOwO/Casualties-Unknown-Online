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
	/// Stage 1's verdict: ANY blocking hook failure refuses the session, which
	/// is the rule the adapter already applied — a capability's Required/Optional
	/// class is recorded and printed but does not yet change the decision. It is
	/// computed by the SAME rule the install gate calls
	/// (<see cref="RefusesInstall"/>), so the printed verdict and the gate's
	/// decision cannot diverge.
	/// </summary>
	internal bool RefusesSession => RefusesInstall([.. AllFailures]);

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
			? "verdict: session REFUSED — stage 1 counts every blocking hook failure, whatever a capability's Required/Optional class says"
			: "verdict: session available — no blocking hook failure");
		if (UnmappedFailures.Count > 0)
		{
			lines.Add($"failures no capability claims ({UnmappedFailures.Count}) — a catalog gap, or a failure that belongs to no single capability:");
			lines.AddRange(UnmappedFailures.Select(failure => $"    - {failure.PatchClass}: {failure.Detail}"));
		}

		return string.Join("\n", lines);
	}

	private IEnumerable<PatchVerificationFailure> AllFailures =>
		Statuses.SelectMany(status => status.Failures).Concat(UnmappedFailures);
}
