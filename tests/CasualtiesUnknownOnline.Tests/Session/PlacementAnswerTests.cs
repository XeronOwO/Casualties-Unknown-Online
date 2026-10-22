using System.Linq;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.PlayerInteractionTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behavior family: the give's answer-first custody (the user's 2026-10-22 ruling). The host asks the RECEIVING
/// client where its own rules would put the item before anything is written, and records the destination that
/// answer names; a refusal writes nothing, so the item stays with the giver. The receiving client's rules are
/// the adapter's own, so this suite drives them through the <see cref="IRemotePlacementResolver"/> port.
/// </summary>
[Trait("Category", "Integration")]
public class PlacementAnswerTests
{
	private sealed class FixedPlacementResolver : IRemotePlacementResolver
	{
		internal RemotePlacementResolution Resolution { get; set; } = RemotePlacementResolution.Refused("not configured");

		internal int Calls { get; private set; }

		public RemotePlacementResolution Resolve(CharacterItemMsg item, int requestedSlot, ulong requestedContainerInstanceId)
		{
			Calls++;
			return Resolution;
		}
	}

	/// <summary>
	/// The host gives one of its own items to a guest. The guest's answer names a DIFFERENT slot than the release
	/// did (a substitution the receiving rules made), and the host's row must state the answered slot: the defect
	/// the ticket was filed for was the host recording its own pick instead.
	/// </summary>
	[Fact]
	public void TheAnsweredSlot_IsTheSlotTheHostRecords()
	{
		var resolver = new FixedPlacementResolver { Resolution = RemotePlacementResolution.ToSlot(2) };
		var (host, guest, received) = CreateSession(s => s.AddSingleton<IRemotePlacementResolver>(resolver));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.GiveToBody,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				TargetBodySteamId = GuestId,
				TargetSlotIndex = 0,
			});

		Assert.Equal(1, resolver.Calls);
		Assert.Contains(received, r => r.Msg == NetMsg.PlayerPlacementQuery);
		Assert.DoesNotContain(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42 && i.SlotIndex == 2);
	}

	/// <summary>
	/// The receiving client's rules refuse the item (no free slot, a container that will not take it): the host
	/// writes nothing, so the item is still the giver's, and the giver is answered with the receiving side's own
	/// sentence, which is what the user's ruling asks for.
	/// </summary>
	[Fact]
	public void ARefusalFromTheReceivingRules_LeavesTheItemWithTheGiver()
	{
		var resolver = new FixedPlacementResolver { Resolution = RemotePlacementResolution.Refused("no body slot on this client would accept it.") };
		var (host, guest, received) = CreateSession(s => s.AddSingleton<IRemotePlacementResolver>(resolver));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var water = WaterBottle(42);
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, water));
		var items = host.Services.GetRequiredService<IItemControl>();
		items.AdoptTransferredItem(GuestId, 42, water);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.GiveToBody,
				OwnerSteamId = GuestId,
				ItemInstanceId = 42,
				TargetBodySteamId = HostId,
				TargetSlotIndex = 0,
			});

		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.Empty(characters.GetHostCharacterData()!.Items);
		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerInventoryTransfer);

		var answer = received.Single(r => r.Msg == NetMsg.InventoryIntentRefused);
		var refused = NetPacket.DecodePayload<PlayerInventoryIntentRefusedMsg>(answer.Frame);
		Assert.Contains("no body slot", refused.Reason);
		Assert.Equal(42UL, refused.ItemInstanceId);
	}

	/// <summary>
	/// The answer and the commit are two moments, and the destination can change between them. The host's exact
	/// commit refuses instead of substituting (a substitution would be the very drift this path ends), nothing
	/// moves, and the item stays with the giver.
	/// </summary>
	[Fact]
	public void ADestinationThatChangedAfterTheAnswer_IsRefusedBeforeCustody()
	{
		var resolver = new FixedPlacementResolver { Resolution = RemotePlacementResolution.ToSlot(2) };
		var (host, guest, received) = CreateSession(s => s.AddSingleton<IRemotePlacementResolver>(resolver));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(77, "trashbag", slot: 2)));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.GiveToBody,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				TargetBodySteamId = GuestId,
				TargetSlotIndex = 2,
			});

		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
		Assert.DoesNotContain(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 77);
		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerInventoryTransfer);
	}

	/// <summary>
	/// A composition without the receiving rules (no game adapter) must not fall back to recording the giver's
	/// request: it refuses, says so, and writes nothing.
	/// </summary>
	[Fact]
	public void WithoutAReceivingResolver_TheGiveIsRefusedAndNothingMoves()
	{
		var (host, guest, received) = CreateSession(s => s.RemoveAll<IRemotePlacementResolver>());
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var water = WaterBottle(42);
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, water));
		var items = host.Services.GetRequiredService<IItemControl>();
		items.AdoptTransferredItem(GuestId, 42, water);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.GiveToBody,
				OwnerSteamId = GuestId,
				ItemInstanceId = 42,
				TargetBodySteamId = HostId,
				TargetSlotIndex = 0,
			});

		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.Empty(characters.GetHostCharacterData()!.Items);
		var answer = received.Single(r => r.Msg == NetMsg.InventoryIntentRefused);
		var refused = NetPacket.DecodePayload<PlayerInventoryIntentRefusedMsg>(answer.Frame);
		Assert.Contains("no game adapter", refused.Reason);
	}
}
