using System;
using System.IO;
using BepInEx.Logging;
using CasualtiesUnknownOnline.Runtime;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Microsoft.Extensions.DependencyInjection;
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

		// The transport identity the save layer asks for is the router's: it is the
		// only object that knows which transport is live (§2).
		Assert.Same(provider.GetRequiredService<CuoNetworkRouter>(), provider.GetRequiredService<ITransportIdentity>());
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
