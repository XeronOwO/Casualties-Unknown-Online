using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Behavior family: a one-shot entity that progresses keeps only its LATEST consumption (the registry is the fact source, later writes overwrite), and the late-joiner snapshot replays exactly that.
/// Shard: machine/station entities (a partition of the archive — no row is duplicated;
/// see <see cref="EntityEventBehaviorData"/>). One row per archived kind in this shard.
/// </summary>
[Trait("Category", "Integration")]
public class EntityEventSnapshotExtraMachineTests
{
	[Theory]
	[MemberData(nameof(EntityEventBehaviorData.MachineOneShotKinds), MemberType = typeof(EntityEventBehaviorData))]
	public void OneShot_SnapshotCarriesLatestExtra(EntityEventKind kind)
	{
		var w = EntityEventSimWorld.Create();
		var consumed = new List<IReadOnlyList<EntityEventMsg>>();
		w.G1.Services.GetRequiredService<WorldEntityKernelProjection>().TrapSnapshotProjected += list => consumed.Add(list);

		// The same one-shot entity progresses (ScrapEaterProgress's %-carrying
		// reports — the registry is the fact source, later writes overwrite).
		w.HostChannel.ReportTrapConsumed(kind, 30f, 40f, extra: 25);
		w.HostChannel.ReportTrapConsumed(kind, 30f, 40f, extra: 50);
		w.SendCheckpoint(w.G1);

		Assert.True(consumed.Count == 1 && consumed[0].Count == 1,
			$"{kind}: the snapshot must carry the one consumed entity");
		Assert.True(consumed[0][0].Extra == 50,
			$"{kind}: the LATEST consumption (50) is what the late joiner replays, got {consumed[0][0].Extra}");
	}
}
