using CasualtiesUnknownOnline.Runtime.Protocol;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Behavior family: a retransmitted report relays unconditionally (the message layer is not the guard), while the host guard drops the re-execution for one-shots only.
/// Shard: crystal entities (a partition of the archive — no row is duplicated;
/// see <see cref="EntityEventBehaviorData"/>). One row per archived kind in this shard.
/// </summary>
public class EntityEventDuplicateGuardCrystalTests
{
	[Theory]
	[MemberData(nameof(EntityEventBehaviorData.CrystalKinds), MemberType = typeof(EntityEventBehaviorData))]
	public void DuplicateReport_GuardPerKind(EntityEventKind kind)
	{
		var w = EntityEventSimWorld.Create();

		w.Trigger(w.G1, kind, 10f, 20f, extra: 7);
		w.Trigger(w.G1, kind, 10f, 20f, extra: 7); // a retransmit

		// The handler relays unconditionally — the message layer is not the
		// guard (the relayed duplicate is what the guests' replay guards
		// consume). The HOST guard drops the re-execution for one-shots only.
		Assert.True(w.G2Events.Count == 2, $"{kind}: both reports relay, got {w.G2Events.Count}");
		var expectedExecutions = EntityEventArchives.IsOneShot(kind) ? 1 : 2;
		Assert.True(w.HostExecutions.Value == expectedExecutions,
			$"{kind}: one-shot must execute once, repeatable twice — got {w.HostExecutions.Value}");
	}
}
