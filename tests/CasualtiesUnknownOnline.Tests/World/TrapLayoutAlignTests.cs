using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The trap-layout alignment matrix (extracted from the adapter's
/// application): every host entry claims the closest same-kind local entity
/// within the 3-unit match radius (the position-key replay's lookup radius —
/// an entity the replay could not reach is never "kept"); the unmatched host
/// entries materialize, the unmatched local entities destroy. The matrix that
/// burned the spike-divergence round is locked.
/// </summary>
public class TrapLayoutAlignTests
{
	private static TrapLayoutEntryMsg Entry(EntityEventKind kind, float x, float y) => new() { Kind = kind, X = x, Y = y };

	[Fact]
	public void IdenticalLayouts_KeepEverything()
	{
		var host = new[] { Entry(EntityEventKind.SpikeStabbed, 10f, 20f), Entry(EntityEventKind.GeyserActivated, -5f, 3f) };
		var local = new[] { Entry(EntityEventKind.SpikeStabbed, 10f, 20f), Entry(EntityEventKind.GeyserActivated, -5f, 3f) };

		var a = TrapLayoutAlign.Align(host, local);

		Assert.True(a.ToSpawn.Count == 0 && a.ToDestroy.Count == 0,
			$"identical layouts keep everything (spawn {a.ToSpawn.Count}, destroy {a.ToDestroy.Count})");
	}

	[Fact]
	public void SubUnitDrift_WithinTheRadius_Keeps()
	{
		// The position key is cell-quantized (the report rounds to the cell
		// centre) — a 2.9-unit drift still resolves to the same entity.
		var host = new[] { Entry(EntityEventKind.SpikeStabbed, 10f, 20f) };
		var local = new[] { Entry(EntityEventKind.SpikeStabbed, 12.5f, 21.4f) };

		var a = TrapLayoutAlign.Align(host, local);

		Assert.True(a.ToSpawn.Count == 0 && a.ToDestroy.Count == 0,
			$"a within-radius drift keeps the entity (spawn {a.ToSpawn.Count}, destroy {a.ToDestroy.Count})");
	}

	[Fact]
	public void OffPositionEntity_SpawnsTheHosts_AndDestroysTheLocals()
	{
		// The observed divergence: the host's spike at (-13,466.8), the
		// guest's nearest 42 units away — beyond the radius, so the guest's
		// copy destroys and the host's position materializes.
		var host = new[] { Entry(EntityEventKind.SpikeStabbed, -13f, 466.8f) };
		var local = new[] { Entry(EntityEventKind.SpikeStabbed, -24.6f, 426f) };

		var a = TrapLayoutAlign.Align(host, local);

		Assert.True(a.ToSpawn.Count == 1 && a.ToSpawn[0].X == -13f,
			$"the host's spike must materialize, got {a.ToSpawn.Count} spawn(s)");
		Assert.True(a.ToDestroy.Count == 1 && a.ToDestroy[0] == 0,
			$"the guest's divergent spike must destroy (local index 0), got [{string.Join(",", a.ToDestroy)}]");
	}

	[Fact]
	public void DifferentKindsAtTheSamePosition_MatchTheirOwnKind()
	{
		var host = new[] { Entry(EntityEventKind.TurretFired, 5f, 5f) };
		var local = new[] { Entry(EntityEventKind.SpikeStabbed, 5f, 5f) };

		var a = TrapLayoutAlign.Align(host, local);

		Assert.True(a.ToSpawn.Count == 1 && a.ToDestroy.Count == 1,
			$"a different kind never matches (spawn {a.ToSpawn.Count}, destroy {a.ToDestroy.Count})");
	}

	[Fact]
	public void NearestNeighbour_ClaimsTheClosestSameKind()
	{
		// Two host entries and two local entities, cross-ordered: each host
		// entry must claim its closest local, never two claims on one.
		var host = new[]
		{
			Entry(EntityEventKind.GeyserActivated, 0f, 0f),
			Entry(EntityEventKind.GeyserActivated, 50f, 0f),
		};
		var local = new[]
		{
			Entry(EntityEventKind.GeyserActivated, 48f, 0f),
			Entry(EntityEventKind.GeyserActivated, 2f, 0f),
		};

		var a = TrapLayoutAlign.Align(host, local);

		Assert.True(a.ToSpawn.Count == 0 && a.ToDestroy.Count == 0,
			$"the greedy matching pairs each host entry with its closest local (spawn {a.ToSpawn.Count}, destroy {a.ToDestroy.Count})");
	}

	[Fact]
	public void MissingHostEntity_Spawns()
	{
		var host = new[] { Entry(EntityEventKind.MineExploded, 1f, 2f) };
		var a = TrapLayoutAlign.Align(host, []);

		Assert.True(a.ToSpawn.Count == 1 && a.ToDestroy.Count == 0, "a host-only entity materializes");
	}

	[Fact]
	public void SurplusLocalEntity_Destroys()
	{
		var local = new[] { Entry(EntityEventKind.SpikeStabbed, 9f, 9f) };
		var a = TrapLayoutAlign.Align([], local);

		Assert.True(a.ToSpawn.Count == 0 && a.ToDestroy.Count == 1 && a.ToDestroy[0] == 0,
			"a local-only entity destroys (index 0)");
	}
}
