using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Session-end resets for the domain state that a lobby switch could
/// otherwise leak into the next session: world params/start gate/damage
/// table, item tables/modifier projection, and saved characters.
/// </summary>
public class LobbyDomainResetTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void WorldSessionState_ClearsOnSessionEnd()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var world = host.Services.GetRequiredService<WorldService>();
		var guestWorld = guest.Services.GetRequiredService<WorldService>();

		world.WorldParams = new WorldStartParams { RandomState = [1, 2, 3] };
		world.SetHostRunPending(true);
		world.ReportBlockState(3, 4, 7);
		Assert.True(world.StartStartGate(), "the handshaken, not-in-world guest must arm the start gate");

		var blockStateArrived = false;
		guestWorld.BlockStateReceived += _ => blockStateArrived = true;
		world.SendBlockStateSnapshot(GuestId);
		Assert.True(blockStateArrived, "the seeded damage table is sendable before the session ends");

		blockStateArrived = false;
		host.Session.EndSession();

		Assert.Null(world.WorldParams);
		Assert.False(world.HostRunPending);
		Assert.False(world.StartGateActive);
		world.SendBlockStateSnapshot(GuestId);
		Assert.False(blockStateArrived, "the damage table must not survive the session");
	}

	[Fact]
	public void ItemSessionState_ClearsOnSessionEnd()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var items = host.Services.GetRequiredService<ItemService>();
		var arbitration = host.Services.GetRequiredService<ItemArbitration>();

		const ulong itemId = 555;
		items.SendItemSpawned(itemId, new CharacterItemMsg { ItemId = "rock" }, default, default, 0f, freshItemDrop: false, angularVelocity: 0f);
		arbitration.RegisterCarried(GuestId, [new CharacterItemMsg { ItemId = "bandage", InstanceId = 777 }]);
		items.LayerModifierIndex = 3;
		items.LayerModifierRandomState = [4, 5, 6];

		Assert.True(items.IsWorldItemRegistered(itemId));
		Assert.True(arbitration.GetTransferredItems(GuestId).Count == 1);

		host.Session.EndSession();

		Assert.False(items.IsWorldItemRegistered(itemId), "the world table is session-scoped");
		Assert.True(arbitration.GetTransferredItems(GuestId).Count == 0, "the transfer table is session-scoped");
		Assert.True(items.LayerModifierIndex == -1, "the modifier projection must reset");
		Assert.Null(items.LayerModifierRandomState);
	}

	[Fact]
	public void SavedCharacters_ClearOnSessionEnd()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var store = host.Services.GetRequiredService<CharacterDataStore>();
		store.SaveCharacterData(GuestId, new CharacterDataMsg
		{
			Items = [new CharacterItemMsg { ItemId = "flashlight" }],
		});
		Assert.NotNull(store.GetSavedCharacter(GuestId));

		host.Session.EndSession();

		Assert.Null(store.GetSavedCharacter(GuestId));
	}
}
