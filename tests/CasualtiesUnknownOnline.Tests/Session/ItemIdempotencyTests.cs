using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The reliable channel can retransmit — a repeated ItemSpawn frame (same
/// operation id) must register and relay exactly once. Observed on a second
/// guest: the star topology relays to every member except the source, so a
/// duplicate relay would show up there.
/// </summary>
public class ItemIdempotencyTests
{
	[Fact]
	public void DuplicateItemSpawnReport_RelayedOnce()
	{
		using var w = ItemSimWorld.Create();
		var relayed = 0;
		w.G2.Services.GetRequiredService<IItemControl>().ItemSpawned += _ => relayed++;

		// Steam reliable retransmission duplicates the same wire frame, so the
		// kernel sees the same OperationId twice and applies the command once.
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Duplicate = true });
		w.Spawn(w.G1, 42, new CharacterItemMsg { ItemId = "test_item", Condition = 1f });
		w.Driver.Tick(33);

		Assert.Equal(1, relayed);
	}
}
