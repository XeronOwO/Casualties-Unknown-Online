using System;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Patching;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Capabilities;

/// <summary>
/// The capability report's own contract: every capability appears with its
/// Required/Optional class and its contract count, every failure reason is
/// printed under the capability it belongs to, and stage 1's verdict stays
/// all-or-nothing — an Optional capability that fails is reported but does not
/// yet degrade alone, which is stage 2's job. The last point is the pin this
/// cycle owes the ticket: when stage 2 lands, THIS is the expectation that
/// changes, and nothing else in the report has to.
/// </summary>
public class AdapterCapabilityReportTests
{
	private const string ProbeLine = "PlayerCamera/Body/PreRunScript/WorldGeneration: OK";

	[Fact]
	public void Render_ListsEveryCapabilityWithItsClassAndContractCount()
	{
		var report = new AdapterCapabilityReport(
			ProbeLine,
			9,
			2,
			[
				Status("items", AdapterCapabilityKind.Required, 7),
				Status("diagnostics", AdapterCapabilityKind.Optional, 2),
			],
			[]);

		var text = report.Render();

		Assert.Contains("[Required] items — 7 contract(s) — OK", text, StringComparison.Ordinal);
		Assert.Contains("[Optional] diagnostics — 2 contract(s) — OK", text, StringComparison.Ordinal);
		Assert.Contains("2 capability(ies), 9 patch contract(s) + 2 dynamic row(s)", text, StringComparison.Ordinal);
		Assert.Contains(ProbeLine, text, StringComparison.Ordinal);
		Assert.Contains("verdict: session available", text, StringComparison.Ordinal);
		Assert.False(report.RefusesSession);
	}

	[Fact]
	public void Render_PrintsTheFailureReasonUnderItsCapability()
	{
		var report = new AdapterCapabilityReport(
			ProbeLine,
			3,
			0,
			[Status("traps", AdapterCapabilityKind.Required, 3, Blocking())],
			[]);

		var text = report.Render();

		Assert.Contains("[Required] traps — 3 contract(s) — FAILED", text, StringComparison.Ordinal);
		Assert.Contains("TrapTurretPatch: TurretScript.Update not applied", text, StringComparison.Ordinal);
		Assert.Contains("verdict: session REFUSED", text, StringComparison.Ordinal);
		Assert.True(report.RefusesSession);
	}

	/// <summary>
	/// The report's verdict and the install gate's decision are ONE rule — the
	/// report computes its own with the same call the gate makes, so a printed
	/// "session available" can never sit next to a refused install.
	/// </summary>
	[Fact]
	public void RefusesSession_UsesTheSameRuleAsTheInstallGate()
	{
		var blocking = new AdapterCapabilityReport(ProbeLine, 1, 0, [Status("traps", AdapterCapabilityKind.Required, 1, Blocking())], []);
		var probeOnly = new AdapterCapabilityReport(ProbeLine, 1, 1, [Status("traps", AdapterCapabilityKind.Required, 2, ProbeOnly())], []);

		Assert.Equal(AdapterCapabilityReport.RefusesInstall([Blocking()]), blocking.RefusesSession);
		Assert.Equal(AdapterCapabilityReport.RefusesInstall([ProbeOnly()]), probeOnly.RefusesSession);
		Assert.True(blocking.RefusesSession);
		Assert.False(probeOnly.RefusesSession);
	}

	[Fact]
	public void Render_PrintsAProbeOnlyFailureWithItsReason()
	{
		var status = new AdapterCapabilityStatus(
			"diagnostics",
			"CUO's diagnostic hooks",
			AdapterCapabilityKind.Optional,
			2,
			[],
			["member PlayerCamera.recipeItemFilter not found"]);

		var report = new AdapterCapabilityReport(ProbeLine, 2, 0, [status], []);
		var text = report.Render();

		Assert.True(status.Failed);
		Assert.False(status.BlocksInstall);
		Assert.Contains("member PlayerCamera.recipeItemFilter not found", text, StringComparison.Ordinal);
		Assert.Contains("reported only", text, StringComparison.Ordinal);
		Assert.False(report.RefusesSession);
	}

	[Fact]
	public void Render_NamesAnUnmappedFailureInsteadOfDroppingIt()
	{
		var unmapped = new PatchVerificationFailure("NewPatchClass", "CasualtiesUnknownOnline.GameAdapter.Patches.NewPatchClass", "Target.Method missing", blocksInstall: true);
		var report = new AdapterCapabilityReport(ProbeLine, 0, 0, [], [unmapped]);
		var text = report.Render();

		Assert.Contains("failures no capability claims (1)", text, StringComparison.Ordinal);
		Assert.Contains("NewPatchClass: Target.Method missing", text, StringComparison.Ordinal);
		Assert.True(report.RefusesSession);
	}

	[Fact]
	public void RefusesInstall_IsAllOrNothingForBlockingFailures()
	{
		Assert.True(AdapterCapabilityReport.RefusesInstall([Blocking()]));
		Assert.False(AdapterCapabilityReport.RefusesInstall([ProbeOnly()]));
		Assert.False(AdapterCapabilityReport.RefusesInstall([]));
	}

	[Fact]
	public void RefusesSession_ReportsAProbeOnlyFailureWithoutRefusing()
	{
		var report = new AdapterCapabilityReport(
			ProbeLine,
			1,
			1,
			[Status("traps", AdapterCapabilityKind.Required, 2, ProbeOnly())],
			[]);

		Assert.True(report.Statuses[0].Failed);
		Assert.False(report.Statuses[0].BlocksInstall);
		Assert.False(report.RefusesSession);
	}

	private static PatchVerificationFailure Blocking() =>
		new("TrapTurretPatch", "CasualtiesUnknownOnline.GameAdapter.Patches.TrapTurretPatch+GuestTurretUpdatePatch", "TurretScript.Update not applied", blocksInstall: true);

	private static PatchVerificationFailure ProbeOnly() =>
		new("TrapCrystalPatch (dynamic)", "TrapCrystalPatch (dynamic)", "CrystalFragile.Touched not found", blocksInstall: false);

	private static AdapterCapabilityStatus Status(string id, AdapterCapabilityKind kind, int contracts, params PatchVerificationFailure[] failures) =>
		new(id, $"{id} capability", kind, contracts, failures, []);
}
