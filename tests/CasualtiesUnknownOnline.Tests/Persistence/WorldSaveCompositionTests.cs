using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using CasualtiesUnknownOnline.Runtime;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The production composition root's save wiring, which the unit suites cannot
/// see: the service is registered through a FACTORY, so `ValidateOnBuild` does
/// not walk its dependencies — a missing registration only shows up when the
/// plugin resolves it, where the awake-time catch turns it into a silent dead
/// mod. This pins the two paths that matter: a root with a save repository
/// resolves and is enabled, and a root without one resolves and stays inert.
/// </summary>
[Trait("Category", "Integration")]
public class WorldSaveCompositionTests
{
	[Fact]
	public void ProductionRoot_WithASavesRoot_ResolvesAnEnabledSaveControl()
	{
		var savesRoot = Path.Combine(Path.GetTempPath(), "cuo-compose-tests", Guid.NewGuid().ToString("N"), "cuo", "saves");
		using var provider = Build(savesRoot);

		var control = provider.GetRequiredService<IWorldSaveControl>();
		Assert.True(control.IsEnabled);
		Assert.False(control.HasRestorableWorld);
		Assert.Null(control.ContinueWorldId);

		// The world-fact port the save system reads and rewrites IS the world
		// control's own lifecycle: a cut has to see exactly the tables the live
		// world does, so a second instance would silently snapshot nothing.
		Assert.Same(provider.GetRequiredService<IWorldControl>(), provider.GetRequiredService<IWorldFactSource>());

		// The transport identity the save layer asks for is the router's: it is the
		// only object that knows which transport is live (§2).
		Assert.Same(provider.GetRequiredService<CuoNetworkRouter>(), provider.GetRequiredService<ITransportIdentity>());
	}

	[Fact]
	public void ProductionRoot_WithASavesRoot_ResolvesTheCutProbeAndTheRestoreAudit()
	{
		var savesRoot = Path.Combine(Path.GetTempPath(), "cuo-compose-tests", Guid.NewGuid().ToString("N"), "cuo", "saves");
		using var provider = Build(savesRoot);

		// The transient policy's Runtime half is a QUERY over the services that own
		// the state: nothing pending in a fresh root, and the keys are the policy's
		// own vocabulary (a rename would break the cut's deferral silently).
		var probe = provider.GetRequiredService<WorldCutTransientProbe>();
		var rows = probe.Capture();
		Assert.Equal(
			[
				WorldTransientPolicy.MedicalSessionKey,
				WorldTransientPolicy.ShrapnelSessionKey,
				WorldTransientPolicy.OtherMedicalSessionKey,
				WorldTransientPolicy.DeferredEntityReportKey,
			],
			rows.Select(row => row.Key));
		Assert.All(rows, row => Assert.Equal(0, row.Pending));
		Assert.All(rows, row => Assert.True(WorldTransientPolicy.IsKnown(row.Key), row.Key));

		// The restore audit starts inert and reports nothing until a restore runs.
		var audit = provider.GetRequiredService<WorldRestoreAudit>();
		Assert.False(audit.AwaitingLiveWrite);
		Assert.Null(audit.Last);

		// ...and it reports through the composition's logger. The parameter is
		// OPTIONAL (a test host may omit it), so a root that registered no
		// ILogger<WorldRestoreAudit> would silently drop the warning that names a
		// straggler from another restore attempt.
		Assert.NotNull(provider.GetService<ILogger<WorldRestoreAudit>>());
	}

	[Fact]
	public void ProductionRoot_WithoutASavesRoot_ResolvesAnInertSaveControl()
	{
		using var provider = Build(savesRoot: null);

		var control = provider.GetRequiredService<IWorldSaveControl>();
		Assert.False(control.IsEnabled);
		Assert.False(control.HasRestorableWorld);
		Assert.False(control.TryContinue(out var outcome));
		Assert.Contains("no world repository", outcome.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void ProductionRoot_WiresTheContinueAccountToTheConsole()
	{
		// The report is only a surface if the PRODUCTION root actually connects the two:
		// the service raises it and the console renders it, and a subscription that never
		// happened would leave the click account exactly as invisible as before this stage
		// (the suites that assert the rendering swap in a fake save control, so they cannot
		// see the wiring). A refusal is enough to prove the chain end to end: the fresh root
		// has no world to continue.
		var savesRoot = Path.Combine(Path.GetTempPath(), "cuo-compose-tests", Guid.NewGuid().ToString("N"), "cuo", "saves");
		using var provider = Build(savesRoot);

		var console = provider.GetRequiredService<ICommandControl>();
		var control = provider.GetRequiredService<IWorldSaveControl>();
		Assert.False(control.TryContinue(out var outcome));

		Assert.Contains(console.Lines, line => line.Text.Contains($"CUO continue refused: {outcome.Summary}", StringComparison.Ordinal));
	}

	[Fact]
	public void ProductionRoot_WiresTheStartingSuppliesAccountToTheConsole()
	{
		// S4.3's report is only a surface if the PRODUCTION root connects the two: the
		// Game Adapter publishes through IStartingSupplyPublisher and the console renders
		// what arrives on IStartingSupplyControl. A subscription that never happened would
		// leave the grant exactly as invisible as no grant at all, and the suites that assert
		// the rendering publish to a hand-built audit, so they cannot see this wiring.
		var savesRoot = Path.Combine(Path.GetTempPath(), "cuo-compose-tests", Guid.NewGuid().ToString("N"), "cuo", "saves");
		using var provider = Build(savesRoot);

		var console = provider.GetRequiredService<ICommandControl>();
		provider.GetRequiredService<IStartingSupplyPublisher>().Publish(new StartingSupplyGrantReport(
			StartingSupplyGrantReport.Disposition.Granted, "light", ["emergencylight"], []));

		Assert.Contains(
			console.Lines,
			line => line.Text.Contains("starting supplies (light) given", StringComparison.Ordinal));

		// The publisher and the subscriber are the SAME object in production — a second
		// audit instance would leave the adapter's reports reaching nobody.
		Assert.Same(provider.GetRequiredService<StartingSupplyAudit>(), provider.GetRequiredService<IStartingSupplyControl>());
	}

	[Fact]
	public void ProductionRoot_ResolvesTheStartingSupplyAuditForTheAdapter()
	{
		// The Game Adapter's constructor takes IStartingSupplyPublisher, so a root that
		// registered only the subscriber side would fail the plugin's Awake — the awake-time
		// catch turns that into a silently dead mod, which is the failure this suite exists
		// for. The audit is one instance for both directions.
		var savesRoot = Path.Combine(Path.GetTempPath(), "cuo-compose-tests", Guid.NewGuid().ToString("N"), "cuo", "saves");
		using var provider = Build(savesRoot);

		var publisher = provider.GetRequiredService<IStartingSupplyPublisher>();
		var audit = provider.GetRequiredService<StartingSupplyAudit>();
		Assert.Same(audit, publisher);

		var reports = new List<StartingSupplyGrantReport>();
		provider.GetRequiredService<IStartingSupplyControl>().Reported += reports.Add;
		audit.Publish(new StartingSupplyGrantReport(StartingSupplyGrantReport.Disposition.Disabled, "none", [], []));

		Assert.Single(reports);
	}

	private static ServiceProvider Build(string? savesRoot) =>
		CuoBootstrap.BuildServiceProvider(
			new ManualLogSource("test"),
			Path.Combine(Path.GetTempPath(), "cuo-compose-tests", Guid.NewGuid().ToString("N"), "logs"),
			savesRoot: savesRoot,
			gameBuild: "test");
}
