using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The item-instance-id coordination: the per-guest counter watermarks (a
/// crashed-and-rejoined guest's counter restarts from zero and would reuse ids
/// the host's tables still hold — the host records the high-water mark and
/// grants it back on rejoin) and the carried-inventory registration (the
/// guest's self-assigned starting-supply ids, registered in the transfer table
/// so its use/slot reports arbitrate normally).
/// </summary>
public class ItemIdCoordinatorTests
{
	[Fact]
	public void GuestWatermark_HighWaterSurvives_RegrantedOnRejoin()
	{
		using var w = ItemSimWorld.Create();

		// The guest allocated up to counter 42, then reported a stale 10 (an
		// out-of-order retransmit) — the high-water mark must survive.
		w.G1.Services.GetRequiredService<IItemControl>().SendItemIdWatermark(42);
		w.Driver.Tick(50);
		w.G1.Services.GetRequiredService<IItemControl>().SendItemIdWatermark(10);
		w.Driver.Tick(50);

		// The guest vanishes from the lobby — the presence check removes it
		// from the host's session after ~2 s (SessionSimulationTests precedent).
		w.Host.Steam.LobbyMembers = [w.Host.SteamId, w.G2.SteamId];
		w.G1.Steam.LobbyMembers = [w.G1.SteamId];
		w.Driver.Tick(2100);
		Assert.False(w.G1.Session.SessionActive, "the guest's session must end when the lobby loses the host");

		// The guest returns: the lobby membership restores, the rejoin flow
		// fires the lobby-entered callback again, the handshake rebuilds —
		// the host grants the RECORDED high-water mark (42), not the stale 10.
		w.Host.Steam.LobbyMembers = [w.Host.SteamId, w.G1.SteamId, w.G2.SteamId];
		w.G1.Steam.LobbyMembers = [w.Host.SteamId, w.G1.SteamId, w.G2.SteamId];
		w.G1.Steam.FireLobbyEntered(9001);
		w.Driver.TickUntil(() => w.Host.Session.Members.Count(m => m.Handshaken) == 2, maxMs: 5000);

		// The rejoin's grant rides the wire (the initial handshake's grant was
		// before the recording surface went up — the list starts clean).
		var grants = w.Watermarks(w.G1);
		Assert.True(grants.Count == 1, $"the rejoin must grant the recorded watermark once, got {grants.Count}");
		Assert.True(grants[0].Counter == 42, $"the grant must carry the high-water mark (42), got {grants[0].Counter}");
	}

	[Fact]
	public void CarriedInventory_RegistersInTheTransferTable()
	{
		using var w = ItemSimWorld.Create();

		w.G1.Services.GetRequiredService<IItemControl>().SendCarriedInventory(
		[
			new CharacterItemMsg { InstanceId = 101, ItemId = "ore", Condition = 1f },
			new CharacterItemMsg { InstanceId = 102, ItemId = "medkit", Condition = 0.5f },
		]);
		w.Driver.Tick(50);

		Assert.True(w.TransferredOf(w.G1, 101), "the first starting supply registered in the transfer table");
		Assert.True(w.TransferredOf(w.G1, 102), "the second starting supply registered in the transfer table");
	}
}
