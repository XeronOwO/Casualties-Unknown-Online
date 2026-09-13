using System;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using CasualtiesUnknownOnline.Runtime;
using CasualtiesUnknownOnline.Runtime.Networking;
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
				WorldTransientPolicy.PickupQueueKey,
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

	private static ServiceProvider Build(string? savesRoot) =>
		CuoBootstrap.BuildServiceProvider(
			new ManualLogSource("test"),
			Path.Combine(Path.GetTempPath(), "cuo-compose-tests", Guid.NewGuid().ToString("N"), "logs"),
			savesRoot: savesRoot,
			gameBuild: "test");
}
