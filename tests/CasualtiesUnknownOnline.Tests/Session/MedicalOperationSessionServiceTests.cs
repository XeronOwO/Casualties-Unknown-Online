using System;
using Microsoft.Extensions.DependencyInjection;

using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Stage 1 remote medical operation session tests: the host owns one generic
/// session per active operation, accepts incremental injection deltas, applies
/// each delta to the authoritative item/target snapshots, broadcasts progress,
/// and emits exactly one terminal EndCommitted. The guest side and the host
/// side share the same IPlayerInteractionControl surface through the real
/// packet handlers.
/// </summary>
public sealed class MedicalOperationSessionServiceTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static CharacterItemMsg MedicineBag(ulong instanceId, string itemId, string liquidId, float amount = 100f, float condition = 1f) => new()
	{
		InstanceId = instanceId,
		ItemId = itemId,
		SlotIndex = 0,
		Condition = condition,
		Liquids = [new LiquidStackMsg { LiquidId = liquidId, Amount = amount }],
	};

	private static CharacterDataMsg Snapshot(ulong owner, bool conscious, params CharacterItemMsg[] items) => new()
	{
		OwnerSteamId = owner,
		Items = [.. items],
		Health = new CharacterHealthMsg
		{
			Alive = true,
			Conscious = conscious,
			BrainHealth = conscious ? 80f : 5f,
		},
	};

	private static CharacterDataMsg SnapshotWithLimbs(ulong owner, bool conscious, params CharacterItemMsg[] items)
	{
		var data = Snapshot(owner, conscious, items);
		data.Limbs =
		[
			new CharacterLimbMsg { Index = 0, SkinHealth = 50f, MuscleHealth = 50f },
			new CharacterLimbMsg { Index = 1, SkinHealth = 20f, MuscleHealth = 30f },
			new CharacterLimbMsg { Index = 2, SkinHealth = 80f, MuscleHealth = 80f },
		];
		return data;
	}

	private static void MarkInWorld(TestNode node) =>
		node.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

	private static (TestNode Host, TestNode Guest, List<(NetMsg Msg, byte[] Frame)> Received) CreateSession()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var received = new List<(NetMsg Msg, byte[] Frame)>();
		guest.Transport.MessageReceived += (_, frame) => received.Add(((NetMsg)frame[0], frame));
		MarkInWorld(host);
		MarkInWorld(guest);
		return (host, guest, received);
	}

	[Fact]
	public void Host_InjectsGuest_AppliesProgressOnTargetAndSendsStateEnd()
	{
		var (host, guest, _) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, MedicineBag(77, "morphine", "morphine", amount: 100f)));
		characters.SaveCharacterData(GuestId, SnapshotWithLimbs(GuestId, conscious: true));

		MedicalOperationStartAckMsg? hostAck = null;
		var guestStates = new List<MedicalOperationStateMsg>();
		var guestEnds = new List<MedicalOperationEndCommittedMsg>();
		var hostOps = host.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		hostOps.StartAckReceived += m => hostAck = m;
		var guestOps = guest.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		guestOps.StateReceived += guestStates.Add;
		guestOps.EndCommittedReceived += guestEnds.Add;

		hostOps.SendStartRequest(GuestId, 77, targetLimbIndex: -1);
		Assert.NotNull(hostAck);
		Assert.True(hostAck!.Accepted);
		hostOps.SendUpdate(hostAck.OperationId, 50f);
		var progress = Assert.Single(guestStates);
		Assert.True(Math.Abs(progress.CommittedMl - 50f) < 0.001f);
		Assert.True(Math.Abs(progress.TargetHealth!.OpiateAmount - 45f) < 0.001f);

		hostOps.SendEndRequest(hostAck.OperationId, 50f);
		var end = Assert.Single(guestEnds);
		Assert.Equal(MedicalOperationTerminalReason.Completed, end.TerminalReason);
		Assert.True(Math.Abs(end.CommittedMl - 50f) < 0.001f);

		var guestSaved = characters.GetSavedCharacter(GuestId)!.Health!;
		Assert.True(Math.Abs(guestSaved.OpiateAmount - 45f) < 0.001f);
		var hostItem = characters.GetHostCharacterData()!.Items.Single(i => i.InstanceId == 77);
		Assert.True(Math.Abs(hostItem.Liquids.Single().Amount - 50f) < 0.001f);
	}

	[Fact]
	public void SameItem_SecondStart_IsRejectedWhileReserved()
	{
		var (host, guest, _) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		// The same instance id is intentionally seeded on the host too, so the
		// second operator can attempt the reservation conflict without a third
		// node. Real item ownership is host-separate; the reservation id is the
		// conflict key under test.
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, MedicineBag(42, "morphine", "morphine", amount: 100f)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, MedicineBag(42, "morphine", "morphine", amount: 100f)));

		var hostOps = host.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		var guestOps = guest.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		MedicalOperationStartAckMsg? firstAck = null;
		MedicalOperationStartAckMsg? secondAck = null;
		guestOps.StartAckReceived += m => firstAck = m;
		hostOps.StartAckReceived += m => secondAck = m;

		guestOps.SendStartRequest(HostId, 42, targetLimbIndex: -1);
		Assert.NotNull(firstAck);
		Assert.True(firstAck!.Accepted);

		// A different operator trying to reserve the same item instance must be
		// rejected until the first session terminates.
		hostOps.SendStartRequest(GuestId, 42, targetLimbIndex: -1);
		Assert.NotNull(secondAck);
		Assert.False(secondAck!.Accepted);
	}

	[Fact]
	public void IdleSession_TimesOutAndReleasesReservation()
	{
		var (host, guest, _) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var morphine = MedicineBag(42, "morphine", "morphine", amount: 100f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, morphine));
		items.AdoptTransferredItem(GuestId, 42, morphine);

		MedicalOperationStartAckMsg? ack = null;
		var ends = new List<MedicalOperationEndCommittedMsg>();
		var guestOps = guest.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		guestOps.StartAckReceived += m => ack = m;
		guestOps.EndCommittedReceived += ends.Add;
		guestOps.SendStartRequest(HostId, 42, targetLimbIndex: -1);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);

		guestOps.SendUpdate(ack.OperationId, 10f);
		host.Clock.Advance(20_000);
		host.Update();
		var end = Assert.Single(ends);
		Assert.Equal(MedicalOperationTerminalReason.TimedOut, end.TerminalReason);
		Assert.True(Math.Abs(end.CommittedMl - 10f) < 0.001f);
	}

	[Fact]
	public void OperatorDisconnect_MidInjection_KeepsCommittedAndReleases()
	{
		var (host, guest, _) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var morphine = MedicineBag(42, "morphine", "morphine", amount: 100f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, morphine));
		items.AdoptTransferredItem(GuestId, 42, morphine);

		MedicalOperationStartAckMsg? ack = null;
		var hostEnds = new List<MedicalOperationEndCommittedMsg>();
		var guestOps = guest.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		guestOps.StartAckReceived += m => ack = m;
		var hostOps = host.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		hostOps.EndCommittedReceived += hostEnds.Add;

		guestOps.SendStartRequest(HostId, 42, targetLimbIndex: -1);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);
		guestOps.SendUpdate(ack.OperationId, 20f);

		((ISessionControl)host.Session).RemoveGuestMember(GuestId);
		var end = Assert.Single(hostEnds);
		Assert.Equal(MedicalOperationTerminalReason.Disconnected, end.TerminalReason);
		Assert.True(Math.Abs(end.CommittedMl - 20f) < 0.001f);
	}

	[Fact]
	public void DirectUseRequest_WithInjectableMedicine_IsRefused()
	{
		var (host, guest, _) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var morphine = MedicineBag(42, "morphine", "morphine", amount: 100f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, morphine));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		// The one-shot path must not apply anything for injectable medicine.
		Assert.Equal(0f, characters.GetHostCharacterData()!.Health!.OpiateAmount);
		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.True(Math.Abs(saved.Liquids.Single().Amount - 100f) < 0.001f);
	}

	[Fact]
	public void Guest_InjectionStartUpdateEnd_AppliesProgressivelyAndEmitsSingleEnd()
	{
		var (host, guest, _) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var morphine = MedicineBag(42, "morphine", "morphine", amount: 100f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, morphine));
		items.AdoptTransferredItem(GuestId, 42, morphine);

		MedicalOperationStartAckMsg? ack = null;
		var states = new List<MedicalOperationStateMsg>();
		var ends = new List<MedicalOperationEndCommittedMsg>();
		var guestOps = guest.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		guestOps.StartAckReceived += m => ack = m;
		guestOps.StateReceived += states.Add;
		guestOps.EndCommittedReceived += ends.Add;

		guestOps.SendStartRequest(HostId, 42, targetLimbIndex: -1);

		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);

		guestOps.SendUpdate(ack.OperationId, 50f);
		var progress = Assert.Single(states);
		Assert.True(Math.Abs(progress.CommittedMl - 50f) < 0.001f);
		Assert.NotNull(progress.ItemAfter);
		Assert.True(Math.Abs(progress.ItemAfter!.Liquids.Single().Amount - 50f) < 0.001f);
		Assert.True(Math.Abs(progress.TargetHealth!.OpiateAmount - 45f) < 0.001f);

		guestOps.SendEndRequest(ack.OperationId, 50f);
		var end = Assert.Single(ends);
		Assert.Equal(MedicalOperationTerminalReason.Completed, end.TerminalReason);
		Assert.True(Math.Abs(end.CommittedMl - 50f) < 0.001f);
		Assert.NotNull(end.TargetHealth);
		Assert.True(Math.Abs(end.TargetHealth!.OpiateAmount - 45f) < 0.001f);

		var hostData = characters.GetHostCharacterData()!;
		Assert.True(Math.Abs(hostData.Health!.OpiateAmount - 45f) < 0.001f);
		var transferred = items.GetTransferredItems(GuestId).Single(w => w.Item.InstanceId == 42);
		Assert.True(Math.Abs(transferred.Item.Liquids.Single().Amount - 50f) < 0.001f);
		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.True(Math.Abs(saved.Liquids.Single().Amount - 50f) < 0.001f);
	}

	[Fact]
	public void Guest_InjectionCancel_KeepsCommittedAndSendsCancelledEnd()
	{
		var (host, guest, _) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var morphine = MedicineBag(42, "morphine", "morphine", amount: 100f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, morphine));
		items.AdoptTransferredItem(GuestId, 42, morphine);

		MedicalOperationStartAckMsg? ack = null;
		var states = new List<MedicalOperationStateMsg>();
		var ends = new List<MedicalOperationEndCommittedMsg>();
		var guestOps = guest.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		guestOps.StartAckReceived += m => ack = m;
		guestOps.StateReceived += states.Add;
		guestOps.EndCommittedReceived += ends.Add;

		guestOps.SendStartRequest(HostId, 42, targetLimbIndex: -1);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);
		guestOps.SendUpdate(ack.OperationId, 30f);
		Assert.Single(states);

		guestOps.SendCancelRequest(ack.OperationId);
		var end = Assert.Single(ends);
		Assert.Equal(MedicalOperationTerminalReason.Cancelled, end.TerminalReason);
		Assert.True(Math.Abs(end.CommittedMl - 30f) < 0.001f);
		Assert.True(Math.Abs(end.TargetHealth!.OpiateAmount - 27f) < 0.001f);

		// The same item can be started again after cancellation: no reservation leak.
		var firstAck = ack;
		MedicalOperationStartAckMsg? secondAck = null;
		guestOps.StartAckReceived += m => secondAck = m;
		guestOps.SendStartRequest(HostId, 42, targetLimbIndex: -1);
		Assert.NotNull(secondAck);
		Assert.True(secondAck!.Accepted);
		Assert.NotEqual(firstAck!.OperationId, secondAck.OperationId);
	}

	[Fact]
	public void Guest_InjectionCancel_FlushesBufferedDeltaBeforeCancel()
	{
		var (host, guest, _) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var morphine = MedicineBag(42, "morphine", "morphine", amount: 100f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, morphine));
		items.AdoptTransferredItem(GuestId, 42, morphine);

		MedicalOperationStartAckMsg? ack = null;
		var hostEnds = new List<MedicalOperationEndCommittedMsg>();
		var guestOps = guest.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		var hostOps = host.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		guestOps.StartAckReceived += m => ack = m;
		hostOps.EndCommittedReceived += hostEnds.Add;

		guestOps.SendStartRequest(HostId, 42, targetLimbIndex: -1);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);

		guestOps.SendUpdate(ack.OperationId, 10f);
		guestOps.SendUpdate(ack.OperationId, 5f); // second frame is coalesced/buffered
		guestOps.SendCancelRequest(ack.OperationId);

		var end = Assert.Single(hostEnds);
		Assert.Equal(MedicalOperationTerminalReason.Cancelled, end.TerminalReason);
		Assert.True(Math.Abs(end.CommittedMl - 15f) < 0.001f);
		Assert.True(Math.Abs(end.TargetHealth!.OpiateAmount - 13.5f) < 0.001f);
	}

	[Fact]
	public void Guest_InjectionBurst_CoalescesIntoOneReliableFrameAfterInterval()
	{
		var (host, guest, _) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var morphine = MedicineBag(42, "morphine", "morphine", amount: 100f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, morphine));
		items.AdoptTransferredItem(GuestId, 42, morphine);

		MedicalOperationStartAckMsg? ack = null;
		var updates = new List<MedicalOperationUpdateMsg>();
		host.Transport.MessageReceived += (_, data) =>
		{
			if (data.Length > 0 && (NetMsg)data[0] == NetMsg.MedicalOperationUpdate)
			{
				updates.Add(NetPacket.DecodePayload<MedicalOperationUpdateMsg>(data));
			}
		};

		var guestOps = guest.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		guestOps.StartAckReceived += m => ack = m;
		guestOps.SendStartRequest(HostId, 42, targetLimbIndex: -1);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);

		guestOps.SendUpdate(ack.OperationId, 10f);
		Assert.Single(updates);
		guestOps.SendUpdate(ack.OperationId, 5f);
		Assert.Single(updates);

		guest.Clock.Advance(50);
		guest.Update();

		Assert.Equal(2, updates.Count);
		Assert.True(Math.Abs(updates[1].DeltaMl - 5f) < 0.001f);
	}
}
