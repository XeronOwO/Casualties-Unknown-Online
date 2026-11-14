using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The host's run-end broadcast is a control message for ONE run. Measured
/// 2026-11-14: a late run-0 broadcast reached a member that had just entered
/// run 1, and the member left the new run. The host now always names its
/// current run, and a member ignores a broadcast that names another one.
/// </summary>
[Trait("Category", "Integration")]
public class RunEndedTests
{
	[Fact]
	public void HostRunEnd_WithoutANamedRun_IsScopedToTheHostsCurrentRun()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 0);
		WorldGenerationReports.CommitRun(w.G1, layerIndex: 0);
		w.SetInWorld(w.G1, true);
		var received = new List<RunEndedMsg>();
		w.G1.Services.GetRequiredService<IWorldControl>().RunEndedReceived += received.Add;

		w.Host.Services.GetRequiredService<IWorldControl>().PublishRunEnded(0);
		w.Driver.Tick(50);

		var msg = Assert.Single(received);
		Assert.Equal(1, msg.RunId);
	}

	[Fact]
	public void HostRunEnd_ForAnotherRun_IsIgnoredByTheMemberInANewerRun()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 0);
		WorldGenerationReports.CommitRun(w.G1, layerIndex: 0);
		w.G1.Services.GetRequiredService<ItemKernelAuthority>().ResetSessionState();
		WorldGenerationReports.CommitRun(w.G1, layerIndex: 0);
		w.SetInWorld(w.G1, true);
		var received = new List<RunEndedMsg>();
		w.G1.Services.GetRequiredService<IWorldControl>().RunEndedReceived += received.Add;

		w.Host.Services.GetRequiredService<IWorldControl>().PublishRunEnded(0);
		w.Driver.Tick(50);

		Assert.Empty(received);
	}

	[Fact]
	public void HostRunEnd_ForTheMembersCurrentRun_ReachesIt()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 0);
		WorldGenerationReports.CommitRun(w.G1, layerIndex: 0);
		w.SetInWorld(w.G1, true);
		var received = new List<RunEndedMsg>();
		w.G1.Services.GetRequiredService<IWorldControl>().RunEndedReceived += received.Add;

		w.Host.Services.GetRequiredService<IWorldControl>().PublishRunEnded(1);
		w.Driver.Tick(50);

		var msg = Assert.Single(received);
		Assert.Equal(1, msg.RunId);
	}
}
