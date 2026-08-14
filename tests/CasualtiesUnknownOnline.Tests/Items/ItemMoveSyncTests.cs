using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The host's physics-move stream (ItemMove, unreliable): the host broadcasts
/// the world items' authoritative positions to every handshaken member — the
/// guests' kinematic copies follow. Empty list sends nothing.
/// </summary>
public class ItemMoveSyncTests
{
	[Fact]
	public void HostMove_BroadcastReachesEveryGuest()
	{
		using var w = ItemSimWorld.Create();
		var g1Moves = new List<IReadOnlyList<ItemMoveEntryMsg>>();
		var g2Moves = new List<IReadOnlyList<ItemMoveEntryMsg>>();
		w.G1.Services.GetRequiredService<IItemControl>().ItemMoveReceived += moves => g1Moves.Add(moves);
		w.G2.Services.GetRequiredService<IItemControl>().ItemMoveReceived += moves => g2Moves.Add(moves);

		var entries = new List<ItemMoveEntryMsg>
		{
			new() { ItemId = 100, X = 10f, Y = 20f, VelX = 1f, VelY = -2f, Rotation = 0.5f, AngularVelocity = 0.1f },
			new() { ItemId = 200, X = -5f, Y = 3f, VelX = 0f, VelY = 0f, Rotation = 2f, AngularVelocity = 0f },
		};
		w.Host.Services.GetRequiredService<IItemControl>().SendItemMove(entries);
		w.Driver.Tick(50);

		Assert.True(g1Moves.Count == 1, $"g1 must get the move stream, got {g1Moves.Count}");
		Assert.True(g2Moves.Count == 1, $"g2 must get the move stream, got {g2Moves.Count}");
		Assert.True(g1Moves[0].Count == 2, $"both entries ride, got {g1Moves[0].Count}");
		Assert.True(g1Moves[0][0].ItemId == 100 && g1Moves[0][0].X == 10f && g1Moves[0][0].VelY == -2f,
			"the authoritative vectors arrive intact");
		Assert.True(g2Moves[0][1].ItemId == 200 && g2Moves[0][1].Rotation == 2f,
			"the second entry's rotation arrives intact");
	}

	[Fact]
	public void HostMove_EmptySendsNothing()
	{
		using var w = ItemSimWorld.Create();
		var moves = new List<IReadOnlyList<ItemMoveEntryMsg>>();
		w.G1.Services.GetRequiredService<IItemControl>().ItemMoveReceived += m => moves.Add(m);

		w.Host.Services.GetRequiredService<IItemControl>().SendItemMove([]);
		w.Driver.Tick(50);

		Assert.True(moves.Count == 0, $"an empty move list sends nothing, got {moves.Count}");
	}
}
