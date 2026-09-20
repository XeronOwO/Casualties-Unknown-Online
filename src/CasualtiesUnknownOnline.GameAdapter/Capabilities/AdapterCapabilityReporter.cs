using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Patching;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Capabilities;

/// <summary>
/// Collects one install attempt's hook failures and publishes the aggregated
/// capability report. It exists as its own collaborator because the adapter's
/// coordinator sits on the 600-line gate: "report which gameplay system broke,
/// and why" is a responsibility with its own state (the attempt's facts), not
/// three more lines inside <c>Install</c>.
/// </summary>
internal sealed class AdapterCapabilityReporter
{
	private readonly ILogger _log;
	private readonly List<PatchVerificationFailure> _failures = [];

	internal AdapterCapabilityReporter(ILogger log)
	{
		_log = log;
	}

	/// <summary>Begin a new install attempt — the previous attempt's facts must not be reported twice (a retry re-installs).</summary>
	internal void BeginInstall() => _failures.Clear();

	/// <summary>Record the attempt's failures: the blocking contract rows and the probe-only dynamic targets alike (the row says which it is).</summary>
	internal void Record(IEnumerable<PatchVerificationFailure> failures) => _failures.AddRange(failures);

	/// <summary>
	/// Publish the one report: every capability with its Required/Optional class,
	/// its contract count and its failure reasons, plus the game-assembly probe's
	/// own line. A refusal is logged at Error level so it cannot be missed in a
	/// log-level-filtered read, and the report is a projection — publishing it
	/// never changes an install verdict.
	/// </summary>
	internal void Publish(string gameProbeLine)
	{
		var report = AdapterCapabilityProbe.Build(_failures, gameProbeLine);
		// The text is composed rather than inlined as a one-line template on purpose: a literal
		// colon-backslash-n escape inside a log template is read as a drive-letter path by
		// RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths. Composing it this way keeps the
		// rendered message byte-identical to the original (a single LF, not Environment.NewLine).
		var message = "Game Adapter capability report:" + "\n" + report.Render();
		_log.Log(
			report.RefusesSession ? LogLevel.Error : LogLevel.Information,
			"{Report}",
			message);
	}
}
