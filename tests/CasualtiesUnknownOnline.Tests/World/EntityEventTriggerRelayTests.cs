using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Behavior family: one report triggers the host execution exactly once, relays
/// to the other guests and never back to the source; a one-shot consumption is
/// recorded for the late-joiner snapshot. One row per archived entity-event
/// kind (see <see cref="EntityEventArchives"/>). The host executor is the shell
/// (the real TrapConsumptionRegistry + the guard shape the production executor
/// applies); the wire path is the real stack.
/// </summary>
public class EntityEventTriggerRelayTests
{
	[Theory]
	[MemberData(nameof(EntityEventBehaviorData.AllKinds), MemberType = typeof(EntityEventBehaviorData))]
	public void Trigger_RelaysAndExecutes(EntityEventKind kind)
	{
		var w = EntityEventSimWorld.Create();
		var consumed = new List<IReadOnlyList<EntityEventMsg>>();
		w.G2.Services.GetRequiredService<WorldEntityKernelProjection>().TrapSnapshotProjected += list => consumed.Add(list);

		w.Trigger(w.G1, kind, 10f, 20f, extra: 7);

		Assert.True(w.HostExecutions.Value == 1, $"{kind}: the host must execute once, got {w.HostExecutions.Value}");
		Assert.True(w.G2Events.Count == 1, $"{kind}: the other guest must get exactly one relay, got {w.G2Events.Count}");
		Assert.True(w.G2Events[0].Kind == kind && w.G2Events[0].Position.X == 10f && w.G2Events[0].Position.Y == 20f,
			$"{kind}: the relay carries kind + position key");
		Assert.Empty(w.G1Events); // the source never sees its own report back

		if (EntityEventArchives.IsOneShot(kind))
		{
			w.SendCheckpoint(w.G2);
			Assert.True(consumed.Count == 1 && consumed[0].Count == 1 && consumed[0][0].Kind == kind,
				$"{kind}: the one-shot consumption is recorded for the late-joiner snapshot");
		}
	}
}
