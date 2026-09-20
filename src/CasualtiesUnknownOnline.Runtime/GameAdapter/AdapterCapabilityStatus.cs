using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Patching;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// One capability's probe row: what it is, how many patch contracts it owns and
/// why it failed. The reasons are the failures themselves (not a summary), so a
/// reader of the report sees the same violation the install log prints.
/// </summary>
internal sealed class AdapterCapabilityStatus
{
	internal AdapterCapabilityStatus(
		string id,
		string title,
		AdapterCapabilityKind kind,
		int contractCount,
		IReadOnlyList<PatchVerificationFailure> failures,
		IReadOnlyList<string> probeFailures)
	{
		Id = id;
		Title = title;
		Kind = kind;
		ContractCount = contractCount;
		Failures = failures;
		ProbeFailures = probeFailures;
	}

	/// <summary>The stable catalog id (kebab-case, never renumbered).</summary>
	internal string Id { get; }

	/// <summary>What the capability covers, for a reader who does not know the id.</summary>
	internal string Title { get; }

	internal AdapterCapabilityKind Kind { get; }

	/// <summary>Patch contract rows this capability owns (attributed + dynamic).</summary>
	internal int ContractCount { get; }

	/// <summary>Hook failures — attributed contract failures, and the probe-only dynamic-target rows.</summary>
	internal IReadOnlyList<PatchVerificationFailure> Failures { get; }

	/// <summary>Declared game members this capability needs that no longer resolve (probe-only, never blocking).</summary>
	internal IReadOnlyList<string> ProbeFailures { get; }

	internal bool Failed => Failures.Count > 0 || ProbeFailures.Count > 0;

	/// <summary>Whether this capability's failure is on stage 1's install gate; a probe-only failure is reported without refusing the session.</summary>
	internal bool BlocksInstall => Failures.Any(failure => failure.BlocksInstall);

	/// <summary>
	/// The report line: the class tag, the id, the contract count and the
	/// verdict, then one indented line per reason — enough for "which gameplay
	/// system broke, and why" to be answered from the log alone.
	/// </summary>
	internal string Describe()
	{
		var tag = Kind == AdapterCapabilityKind.Required ? "Required" : "Optional";
		var line = $"[{tag}] {Id} — {ContractCount} contract(s) — {(Failed ? "FAILED" : "OK")}";
		if (!Failed)
		{
			return line;
		}

		var lines = new List<string> { line };
		lines.AddRange(Failures.Select(failure =>
			$"    - {failure.PatchClass}: {failure.Detail}{Suffix(failure.BlocksInstall)}"));
		lines.AddRange(ProbeFailures.Select(probe => $"    - {probe}{Suffix(false)}"));
		return string.Join("\n", lines);
	}

	private static string Suffix(bool blocksInstall) =>
		blocksInstall ? string.Empty : " (reported only — stage 1 does not degrade)";
}
