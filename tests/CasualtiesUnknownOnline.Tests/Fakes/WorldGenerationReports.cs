using System;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// Shared test data for the world/layer generation stamps the direct world
/// reports carry (review/world-layer-generation-identity): the identity is the
/// kernel run baseline, so a test that needs a real stamp commits a real run
/// baseline on the node whose send path it is exercising. Stateless on purpose —
/// the suites that use it own their own scenarios.
/// </summary>
internal static class WorldGenerationReports
{
	/// <summary>Commit a kernel run baseline on a node — the source every world-report stamp is read from.</summary>
	internal static void CommitRun(TestNode node, int layerIndex) =>
		Assert.True(
			Kernel(node).TryStartRun(node.SteamId, new RunState(1UL, [1, 2, 3, 4, 5, 6, 7, 8], 0, 0, 0, false, null, layerIndex), out _, out _),
			"the run baseline must commit for a generation stamp to exist");

	/// <summary>The generation stamp a node's own send path would attach right now (its kernel run baseline), with an optional other layer for the stale cases.</summary>
	internal static WorldGenerationMsg StampOf(TestNode node, int? layerOverride = null)
	{
		var authority = Kernel(node);
		var run = authority.QueryRun() ?? throw new InvalidOperationException("the node has no run baseline, so it cannot stamp a report");
		return new WorldGenerationMsg
		{
			RunEpoch = authority.CreateCheckpoint().RunEpoch.Value,
			LayerIndex = layerOverride ?? run.LayerIndex,
		};
	}

	private static ItemKernelAuthority Kernel(TestNode node) =>
		node.Services.GetRequiredService<ItemKernelAuthority>();
}
