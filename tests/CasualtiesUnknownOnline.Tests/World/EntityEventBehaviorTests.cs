using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The phase-5 COMBINATORIAL entity-event behavior suite: the archive
/// (EntityEventArchives — one row per kind) cross-multiplied with the
/// scenario families every kind must satisfy. One runner, 25 kinds × N
/// families — a new kind automatically runs every family (the archive's
/// coverage guard guarantees it cannot be added without a row). The
/// assertions dispatch on the archive's classification: one-shot = the
/// consumption executes exactly once (the per-entity guard drops the
/// duplicate) while the relay stays unconditional; repeatable = every
/// report executes. The host executor is the shell (the real
/// TrapConsumptionRegistry + the guard shape the production executor
/// applies); the wire path is the real stack.
/// </summary>
public class EntityEventBehaviorTests
{
	public static IEnumerable<object[]> AllKinds() =>
		EntityEventArchives.AllKinds.Select(k => new object[] { k });

	public static IEnumerable<object[]> OneShotKinds() =>
		EntityEventArchives.AllKinds.Where(EntityEventArchives.IsOneShot).Select(k => new object[] { k });

	[Theory]
	[MemberData(nameof(AllKinds))]
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

	[Theory]
	[MemberData(nameof(AllKinds))]
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

	[Theory]
	[MemberData(nameof(AllKinds))]
	public void DoubleTriggerRace_OneConsumptionPerSide(EntityEventKind kind)
	{
		var w = EntityEventSimWorld.Create();

		// Two guests trigger the SAME entity (same position key) — the classic
		// one-shot race: whichever report the host processes first consumes,
		// the other is dropped by the guard (repeatables execute both).
		w.Trigger(w.G1, kind, 10f, 20f, extra: 7);
		w.Trigger(w.G2, kind, 10f, 20f, extra: 7);

		var expectedExecutions = EntityEventArchives.IsOneShot(kind) ? 1 : 2;
		Assert.True(w.HostExecutions.Value == expectedExecutions,
			$"{kind}: one-shot executes once under the race, repeatable twice — got {w.HostExecutions.Value}");
		Assert.True(w.G1Events.Count == 1, $"{kind}: G1 receives G2's relay (the source-excluded other copy)");
		Assert.True(w.G2Events.Count == 1, $"{kind}: G2 receives G1's relay");
	}

	[Theory]
	[MemberData(nameof(OneShotKinds))]
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

	[Theory]
	[MemberData(nameof(OneShotKinds))]
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
