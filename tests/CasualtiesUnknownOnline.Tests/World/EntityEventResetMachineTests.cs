using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Behavior family: resetting consumptions (a new layer is generating) clears the one-shot table, so a checkpoint after the reset sends nothing.
/// Shard: machine/station entities (a partition of the archive — no row is duplicated;
/// see <see cref="EntityEventBehaviorData"/>). One row per archived kind in this shard.
/// </summary>
[Trait("Category", "Integration")]
public class EntityEventResetMachineTests
{
	[Theory]
	[MemberData(nameof(EntityEventBehaviorData.MachineOneShotKinds), MemberType = typeof(EntityEventBehaviorData))]
	public void Reset_ClearsConsumptions_NewWorldStartsEmpty(EntityEventKind kind)
	{
		var w = EntityEventSimWorld.Create();
		var consumed = new List<IReadOnlyList<EntityEventMsg>>();
		w.G2.Services.GetRequiredService<WorldEntityKernelProjection>().TrapSnapshotProjected += list => consumed.Add(list);

		w.Trigger(w.G1, kind, 10f, 20f, extra: 7);
		w.HostChannel.ResetConsumptions(); // a new layer is generating
		w.SendCheckpoint(w.G2);

		Assert.True(consumed.Count == 0, $"{kind}: an empty consumption table sends nothing");
	}
}
