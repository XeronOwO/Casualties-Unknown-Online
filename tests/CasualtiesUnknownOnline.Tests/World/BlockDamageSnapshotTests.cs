using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using CasualtiesUnknownOnline.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The partial block-damage snapshot's SEND side. CUO keeps no table of its own,
/// so the rows a member receives are the GAME's own list read at send time: same
/// cells, same order, same bound (the game's own 128 entries). That is what makes
/// a late joiner's set fit its own identically-bounded list by construction —
/// the two bounded tables that used to disagree are gone, because there is only
/// one. The real reader needs a running game, so the Runtime seam is driven with
/// the native port faked.
/// </summary>
[Trait("Category", "Integration")]
public class BlockDamageSnapshotTests
{
	[Fact]
	public void Snapshot_CarriesTheNativeTableVerbatim()
	{
		var native = new FakeNativeWorldFacts();
		native.SeedBlockDamage(10, 20, 35f);
		native.SeedBlockDamage(0, 0, 12f);
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		var received = new List<IReadOnlyList<BlockDamageEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().BlockDamageSnapshotReceived += list => received.Add(list);

		w.Host.Services.GetRequiredService<IWorldControl>().SendBlockDamageSnapshot(w.G1.SteamId);

		Assert.Single(received);
		var rows = received[0];
		Assert.Equal(2, rows.Count);
		Assert.True(rows[0].X == 10 && rows[0].Y == 20 && rows[0].Damage == 35f,
			$"the first row must be the game's own first row, got {rows[0].X}/{rows[0].Y}/{rows[0].Damage}");
		Assert.True(rows[1].X == 0 && rows[1].Y == 0 && rows[1].Damage == 12f,
			"the origin cell must survive the wire unchanged");
		Assert.Contains("capture-block-damages", native.Calls);
	}

	[Fact]
	public void Snapshot_ReadsTheGameTableAtSendTime_NotACachedCopy()
	{
		var native = new FakeNativeWorldFacts();
		native.SeedBlockDamage(1, 1, 10f);
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		var received = new List<IReadOnlyList<BlockDamageEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().BlockDamageSnapshotReceived += list => received.Add(list);
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();

		hostWorld.SendBlockDamageSnapshot(w.G1.SteamId);
		Assert.Equal(10f, Assert.Single(Assert.Single(received)).Damage);

		// The game's own list moved on — a hit landed, or its own 128-entry eviction
		// dropped the cell. The next snapshot must carry THAT: a CUO-side copy is
		// exactly what would have gone stale here.
		native.ClearBlockDamages();
		native.SeedBlockDamage(2, 2, 7f);
		hostWorld.SendBlockDamageSnapshot(w.G1.SteamId);

		Assert.Equal(2, received.Count);
		Assert.True(Assert.Single(received[1]) is { X: 2, Y: 2, Damage: 7f },
			"the snapshot must reflect the game's list as it stands now");
	}

	[Fact]
	public void EmptyNativeTable_SendsNothing()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		var received = new List<IReadOnlyList<BlockDamageEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().BlockDamageSnapshotReceived += list => received.Add(list);

		w.Host.Services.GetRequiredService<IWorldControl>().SendBlockDamageSnapshot(w.G1.SteamId);

		Assert.Empty(received);
	}

	[Fact]
	public void GuestRole_NeverSends()
	{
		var native = new FakeNativeWorldFacts();
		native.SeedBlockDamage(10, 20, 25f);
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		var received = new List<IReadOnlyList<BlockDamageEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().BlockDamageSnapshotReceived += list => received.Add(list);

		// A guest's own game list is not the authority: it never sends it.
		w.G1.Services.GetRequiredService<IWorldControl>().SendBlockDamageSnapshot(w.Host.SteamId);

		Assert.Empty(received);
	}

	[Fact]
	public void NoNativeReader_SendsNothing()
	{
		// A Runtime-only composition (no adapter registered a reader) must stay
		// resolvable — the reader is an OPTIONAL dependency — and must not invent
		// rows. The absence is logged, never silent.
		using var w = ItemSimWorld.Create();
		var received = new List<IReadOnlyList<BlockDamageEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().BlockDamageSnapshotReceived += list => received.Add(list);

		w.Host.Services.GetRequiredService<IWorldControl>().SendBlockDamageSnapshot(w.G1.SteamId);

		Assert.Empty(received);
	}
}
