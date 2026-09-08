using CasualtiesUnknownOnline.Runtime.Protocol;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Behavior family: two guests trigger the SAME entity (same position key) —
/// the classic one-shot race; whichever report the host processes first
/// consumes, the other is dropped by the guard, while repeatables execute both.
/// One row per archived entity-event kind (see <see cref="EntityEventArchives"/>).
/// </summary>
public class EntityEventRaceTests
{
	[Theory]
	[MemberData(nameof(EntityEventBehaviorData.AllKinds), MemberType = typeof(EntityEventBehaviorData))]
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
}
