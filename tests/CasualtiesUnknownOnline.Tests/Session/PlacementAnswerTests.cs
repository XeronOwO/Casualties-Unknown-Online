using System;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
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
			.SendRemoteInventoryIntent(Give(HostId, 42, GuestId));

		Assert.Equal(1, resolver.Calls);
		Assert.Contains(received, r => r.Msg == NetMsg.PlayerPlacementQuery);
		Assert.DoesNotContain(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42 && i.SlotIndex == 2);
	}

	/// <summary>
	/// The give's transfer must TELL the receiving client that its destination is the ANSWERED one: that flag is
	/// what makes the receiver report the place its placement really used instead of substituting silently. It
	/// has to survive the kernel wire — the form the adapter actually reads — which is the defect the machine
	/// run of acceptance row 1 found (the flag was dropped on the way to the guest).
	/// </summary>
	[Fact]
	public void TheGiveTransfer_CarriesTheAnsweredDestinationFlagToTheReceiver()
	{
		var resolver = new FixedPlacementResolver { Resolution = RemotePlacementResolution.ToSlot(2) };
		var (host, guest, received) = CreateSession(s => s.AddSingleton<IRemotePlacementResolver>(resolver));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(Give(HostId, 42, GuestId));

		var transfer = TransferResult(received);
		Assert.True(transfer.ExactDestination);
		Assert.False(transfer.Returned);
		Assert.Equal(2, transfer.Item!.SlotIndex);
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
			.SendRemoteInventoryIntent(Give(GuestId, 42, HostId));

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
			.SendRemoteInventoryIntent(Give(HostId, 42, GuestId, slot: 2));

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
			.SendRemoteInventoryIntent(Give(GuestId, 42, HostId));

		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.Empty(characters.GetHostCharacterData()!.Items);
		var answer = received.Single(r => r.Msg == NetMsg.InventoryIntentRefused);
		var refused = NetPacket.DecodePayload<PlayerInventoryIntentRefusedMsg>(answer.Frame);
		Assert.Contains("no game adapter", refused.Reason);
	}

	/// <summary>
	/// Acceptance row 1: the receiving client applied the item at the answered destination and reports that same
	/// place. The host compares it with the row it wrote, sees a match, and writes nothing — no return, no ledger
	/// change. A repeated outcome for the same give settles nothing a second time: the committed record is taken by
	/// the first report, so one give can never be returned twice.
	/// </summary>
	[Fact]
	public void AMatchingPlacementOutcome_SettlesTheGiveAndMovesNothing()
	{
		var resolver = new FixedPlacementResolver { Resolution = RemotePlacementResolution.ToSlot(2) };
		var (host, guest, received) = CreateSession(s => s.AddSingleton<IRemotePlacementResolver>(resolver));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(Give(HostId, 42, GuestId));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPlacementOutcome(42, applied: true, slot: 2, containerInstanceId: 0, reason: "");

		// The row the host wrote stands: the giver gave the item up and the receiver's snapshot states the answered slot.
		Assert.DoesNotContain(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42 && i.SlotIndex == 2);
		Assert.Single(KernelEvents(received).Where(e => e.Kind == WireEventKind.PlayerInventoryTransfer));

		// A second outcome for the same item finds no committed give and moves nothing.
		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPlacementOutcome(42, applied: false, slot: -1, containerInstanceId: 0, reason: "a repeat");
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.DoesNotContain(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
		Assert.Single(KernelEvents(received).Where(e => e.Kind == WireEventKind.PlayerInventoryTransfer));
	}

	/// <summary>
	/// A container give the receiving client really placed into the answered container: the reported place is the
	/// container id the host wrote, so the comparison matches and the give settles — the container half of
	/// acceptance row 1, where the written place is a container rather than a slot.
	/// </summary>
	[Fact]
	public void AContainerGiveThatStayedInTheContainer_SettlesTheGive()
	{
		const ulong containerId = 77;
		var resolver = new FixedPlacementResolver { Resolution = RemotePlacementResolution.IntoContainer(containerId) };
		var (host, guest, received) = CreateSession(s => s.AddSingleton<IRemotePlacementResolver>(resolver));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(containerId, "trashbag", slot: 2)));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(Give(HostId, 42, GuestId, slot: -1, container: containerId));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPlacementOutcome(42, applied: true, slot: -1, containerInstanceId: containerId, reason: "");

		Assert.DoesNotContain(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
		Assert.Contains(
			characters.GetSavedCharacter(GuestId)!.Items,
			i => i.InstanceId == containerId && i.Contents.Any(c => c.InstanceId == 42));
		Assert.Single(KernelEvents(received).Where(e => e.Kind == WireEventKind.PlayerInventoryTransfer));
	}

	/// <summary>
	/// Acceptance row 2: the receiving client's own container refused the item and its placement fell back to a
	/// body slot — a place the host never wrote. The outcome names it, the host refuses the mismatch by name and
	/// returns the row, so the item ends with the giver instead of living in a place the ledger does not state.
	/// </summary>
	[Fact]
	public void APlacementThatLandedSomewhereElse_IsRefusedAndReturned()
	{
		const ulong containerId = 77;
		var resolver = new FixedPlacementResolver { Resolution = RemotePlacementResolution.IntoContainer(containerId) };
		var (host, guest, received) = CreateSession(s => s.AddSingleton<IRemotePlacementResolver>(resolver));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(containerId, "trashbag", slot: 2)));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(Give(HostId, 42, GuestId, slot: -1, container: containerId));

		// The container refused the item on the receiving client, so its placement used a body slot instead.
		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPlacementOutcome(42, applied: true, slot: 3, containerInstanceId: 0, reason: "the container refused it");

		// The host refused the outcome and returned the row: the giver has the item again and the receiver's copy is gone.
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
		Assert.DoesNotContain(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.Equal(2, KernelEvents(received).Count(e => e.Kind == WireEventKind.PlayerInventoryTransfer));
	}

	/// <summary>
	/// The residual (acceptance row 2's refusal arm): the receiver answered a destination, the host committed it, and
	/// the receiver's LIVE body then cannot take it. Its outcome says nothing was placed; the host refuses it by
	/// name, moves the row back to the giver and publishes the return, so the item ends with the giver — the user's
	/// ruling, even across the answer-to-transfer window.
	/// </summary>
	[Fact]
	public void APlacementThatAppliedNothing_ReturnsTheItemToTheGiver()
	{
		var resolver = new FixedPlacementResolver { Resolution = RemotePlacementResolution.ToSlot(2) };
		var (host, guest, received) = CreateSession(s => s.AddSingleton<IRemotePlacementResolver>(resolver));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(Give(HostId, 42, GuestId));

		// The give committed at the answered slot; the receiver's live body then changed and it reports that.
		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPlacementOutcome(42, applied: false, slot: -1, containerInstanceId: 0, reason: "the answered slot is no longer free.");

		// The host moved the row back: the giver has it again, the receiver's copy lost it, and the kernel saw the
		// give and the return (two transfer events, the second the return). Both flags are read off the SERIALIZED
		// frames the peer received, not off an in-memory event: the give names the answered destination, the return
		// names itself as one.
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
		Assert.DoesNotContain(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.Equal(2, KernelEvents(received).Count(e => e.Kind == WireEventKind.PlayerInventoryTransfer));
		var transfers = TransferResults(received);
		Assert.True(transfers[0].ExactDestination);
		Assert.False(transfers[0].Returned);
		Assert.False(transfers[1].ExactDestination);
		Assert.True(transfers[1].Returned);
	}

	/// <summary>
	/// Acceptance row 3: a report the host cannot match to a placement it committed — an item it never wrote, or a
	/// sender it never wrote that row to — is refused by name and moves nothing. It can neither move an item nor
	/// invent a return, and the committed give it did not come from stands.
	/// </summary>
	[Fact]
	public void AnOutcomeFromAnotherPeer_OrForNoCommittedGive_MovesNothing()
	{
		var resolver = new FixedPlacementResolver { Resolution = RemotePlacementResolution.ToSlot(2) };
		var (host, guest, _) = CreateSession(s => s.AddSingleton<IRemotePlacementResolver>(resolver));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(Give(HostId, 42, GuestId));

		var control = host.Services.GetRequiredService<IPlayerInteractionControl>();
		control.HandlePlacementOutcome(HostId, new PlayerPlacementOutcomeMsg { ItemInstanceId = 42, Applied = false, Slot = -1, Reason = "not the receiver" });
		control.HandlePlacementOutcome(GuestId, new PlayerPlacementOutcomeMsg { ItemInstanceId = 999, Applied = true, Slot = 2, Reason = "no such give" });

		// Neither report matched, so the committed give stands: the giver does not have the item back.
		Assert.DoesNotContain(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	/// <summary>
	/// An "applied" outcome that names neither a slot nor a container is malformed: there is no place to compare, so
	/// the host refuses it like a mismatch and the row goes home. The comparison must never read a missing place as
	/// the place it wrote.
	/// </summary>
	[Fact]
	public void AnAppliedOutcomeThatNamesNoPlace_IsRefusedAndReturned()
	{
		var resolver = new FixedPlacementResolver { Resolution = RemotePlacementResolution.ToSlot(2) };
		var (host, guest, received) = CreateSession(s => s.AddSingleton<IRemotePlacementResolver>(resolver));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(Give(HostId, 42, GuestId));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.HandlePlacementOutcome(GuestId, new PlayerPlacementOutcomeMsg { ItemInstanceId = 42, Applied = true, Slot = -1, ContainerInstanceId = 0 });

		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
		Assert.DoesNotContain(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.Equal(2, KernelEvents(received).Count(e => e.Kind == WireEventKind.PlayerInventoryTransfer));
	}

	/// <summary>
	/// The refusal reason is MEMBER-authored, so the host caps and strips it on receipt: a peer that sends control
	/// characters or an unbounded string must not be able to write them into the host's log. The sender's own clamp
	/// is not enough — the host cannot rely on a peer having run it — which is why the report above bypasses the
	/// sending side's clamp and lands the raw text on the host.
	/// </summary>
	[Fact]
	public void AMemberAuthoredReason_IsClampedOnTheHostSide()
	{
		const string raw = "\u0007bad\nline " + "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
		var resolver = new FixedPlacementResolver { Resolution = RemotePlacementResolution.ToSlot(2) };
		var recorder = new RecordingLoggerFactory();
		var (host, guest, _) = CreateSession(s => s.AddSingleton<ILoggerFactory>(recorder));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(Give(HostId, 42, GuestId));
		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.HandlePlacementOutcome(GuestId, new PlayerPlacementOutcomeMsg { ItemInstanceId = 42, Applied = false, Slot = -1, Reason = raw });

		var line = recorder.Entries.Single(entry => entry.Message.Contains("refused the placement outcome", StringComparison.Ordinal)).Message;
		Assert.DoesNotContain('\u0007', line);
		Assert.DoesNotContain('\n', line);
		Assert.True(line.Length < 400, $"the host's line kept {line.Length} characters of a member-authored reason");
	}

	/// <summary>
	/// The two transfer flags must SURVIVE the kernel boundary: the host builds the message, but the receiving
	/// client is fed the reconstructed one from the committed event. A flag dropped here is a branch that never
	/// runs in production (the review's blocker), so the projection is pinned directly.
	/// </summary>
	[Fact]
	public void TheTransferFlags_SurviveTheKernelProjection()
	{
		var item = PlayerInteractionKernelCodec.FromCharacterItem(new CharacterItemMsg { InstanceId = 42, ItemId = "bandage" });
		var message = PlayerInteractionKernelCodec.ToTransferMessage(new PlayerInventoryTransferEvent(
			FromSteamId: HostId,
			ToSteamId: GuestId,
			Item: item,
			TargetParentItemId: 0,
			Destination: PlayerInventoryDestination.BodySlot,
			ExactDestination: true,
			Returned: false));

		Assert.True(message.ExactDestination);
		Assert.False(message.Returned);

		var returned = PlayerInteractionKernelCodec.ToTransferMessage(new PlayerInventoryTransferEvent(
			FromSteamId: GuestId,
			ToSteamId: HostId,
			Item: item,
			Destination: PlayerInventoryDestination.BodySlot,
			Returned: true));
		Assert.True(returned.Returned);
		Assert.False(returned.ExactDestination);
	}

	/// <summary>One give intent as the giver's own release produces it: an owner, the item, the destination body and one operand.</summary>
	private static RemoteInventoryIntentMsg Give(ulong owner, ulong itemId, ulong receiver, int slot = 0, ulong container = 0) => new()
	{
		Kind = RemoteInventoryIntentKind.GiveToBody,
		OwnerSteamId = owner,
		ItemInstanceId = itemId,
		TargetBodySteamId = receiver,
		TargetSlotIndex = slot,
		TargetContainerInstanceId = container,
	};
}
