using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.PlayerInteractionTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The gate-authority contract, cross-family: the client that acts judges its own
/// reach from its own scene, and the host stops re-judging it from streamed
/// positions that lag the actor's screen. Both directions are pinned, because
/// deleting the host gate without the actor-side one would make this family MORE
/// permissive than it was: a host that reports "blocked" must not refuse an actor
/// whose own client sees a clear line, and an actor whose own client sees a wall
/// must refuse before anything leaves that client.
/// </summary>
[Trait("Category", "Integration")]
public sealed class InteractionGateAuthorityTests
{
	[Fact]
	public void Push_ActorSeesClearWhileTheHostDoesNot_IsNotRefused()
	{
		var (host, guest, received) = CreateSessionBlockingObserver(HostId);
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 10f);
		var hostFrames = CaptureHostMessages(host);

		guest.Services.GetRequiredService<IPlayerInteractionControl>().SendPushRequest(HostId);

		Assert.Contains(NetMsg.PlayerPushRequest, hostFrames);
		Assert.Contains(received, r => r.Msg == NetMsg.PlayerPushResult);
	}

	[Fact]
	public void Push_ActorSeesAWall_IsRefusedOnTheActorClient()
	{
		var (host, guest, received) = CreateSessionBlockingObserver(GuestId);
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 10f);
		var hostFrames = CaptureHostMessages(host);

		guest.Services.GetRequiredService<IPlayerInteractionControl>().SendPushRequest(HostId);

		Assert.DoesNotContain(NetMsg.PlayerPushRequest, hostFrames);
		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerPushResult);
	}

	[Fact]
	public void Push_HostPictureSaysOutOfReach_DoesNotRefuseTheActor()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 20f); // beyond MaxPushDistance on the HOST's picture only
		var hostFrames = CaptureHostMessages(host);

		guest.Services.GetRequiredService<IPlayerInteractionControl>().SendPushRequest(HostId);

		// Reach is the pusher's own judgment: a host whose streamed picture says
		// "out of reach" cannot take that judgment away from the pusher.
		Assert.Contains(NetMsg.PlayerPushRequest, hostFrames);
		Assert.Contains(received, r => r.Msg == NetMsg.PlayerPushResult);
	}

	[Fact]
	public void MedicalStart_ActorSeesClearWhileTheHostDoesNot_IsAccepted()
	{
		var (host, guest, _) = CreateSessionBlockingObserver(HostId);
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, MedicineBottle(42, "morphine", "morphine")));
		var hostFrames = CaptureHostMessages(host);
		var operations = guest.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		MedicalOperationStartAckMsg? ack = null;
		operations.StartAckReceived += m => ack = m;

		operations.SendStartRequest(HostId, 42, targetLimbIndex: -1);

		Assert.Contains(NetMsg.MedicalOperationStartRequest, hostFrames);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);
	}

	[Fact]
	public void MedicalStart_ActorSeesAWall_IsRejectedOnTheActorWithThePreciseReason()
	{
		var (host, guest, _) = CreateSessionBlockingObserver(GuestId);
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, MedicineBottle(42, "morphine", "morphine")));
		var hostFrames = CaptureHostMessages(host);
		var operations = guest.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		MedicalOperationStartAckMsg? ack = null;
		operations.StartAckReceived += m => ack = m;

		operations.SendStartRequest(HostId, 42, targetLimbIndex: -1);

		Assert.DoesNotContain(NetMsg.MedicalOperationStartRequest, hostFrames);
		Assert.NotNull(ack);
		Assert.False(ack!.Accepted);
		Assert.Equal("No line of sight.", ack.RejectReason);
	}

	[Fact]
	public void Heal_ActorSeesClearWhileTheHostDoesNot_IsApplied()
	{
		var (host, guest, received) = CreateSessionBlockingObserver(HostId);
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "bandage", slot: 0)));
		var hostFrames = CaptureHostMessages(host);

		guest.Services.GetRequiredService<IPlayerInteractionControl>().SendHealRequest(HostId);

		Assert.Contains(NetMsg.PlayerHealRequest, hostFrames);
		Assert.Contains(KernelEvents(received), e => e.Kind == WireEventKind.PlayerHealResult);
	}

	[Fact]
	public void Take_ActorSeesAWall_IsRefusedOnTheActorClient()
	{
		var (host, guest, _) = CreateSessionBlockingObserver(GuestId);
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		var hostFrames = CaptureHostMessages(host);

		guest.Services.GetRequiredService<IPlayerInteractionControl>().SendTakeRequest(HostId, 42);

		Assert.DoesNotContain(NetMsg.PlayerInventoryTakeRequest, hostFrames);
	}

	[Fact]
	public void Carry_ActorSeesAWall_IsRefusedOnTheActorClient()
	{
		var (host, guest, _) = CreateSessionBlockingObserver(GuestId);
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		var hostFrames = CaptureHostMessages(host);

		guest.Services.GetRequiredService<IPlayerInteractionControl>().SendCarryStartRequest(HostId);

		Assert.DoesNotContain(NetMsg.PlayerCarryStartRequest, hostFrames);
	}

	[Fact]
	public void GuestHeal_ActorSeesAWall_IsRefusedOnTheActorClient()
	{
		var (host, guest, _) = CreateSessionBlockingObserver(GuestId);
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "bandage", slot: 0)));
		var hostFrames = CaptureHostMessages(host);

		guest.Services.GetRequiredService<IPlayerInteractionControl>().SendHealRequest(HostId);

		Assert.DoesNotContain(NetMsg.PlayerHealRequest, hostFrames);
	}

	[Fact]
	public void Use_ActorSeesAWall_IsRefusedOnTheActorClient()
	{
		var (host, guest, _) = CreateSessionBlockingObserver(GuestId);
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, WaterBottle(42)));
		var hostFrames = CaptureHostMessages(host);

		guest.Services.GetRequiredService<IPlayerInteractionControl>().SendUseRequest(HostId, 42);

		Assert.DoesNotContain(NetMsg.PlayerItemUseRequest, hostFrames);
	}

	[Fact]
	public void RemoteInventory_ActorSeesAWall_IsRefusedOnTheActorClient()
	{
		var (host, guest, _) = CreateSessionBlockingObserver(GuestId);
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		var hostFrames = CaptureHostMessages(host);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.Drop,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
			});

		Assert.DoesNotContain(NetMsg.RemoteInventoryOperationRequest, hostFrames);
	}

	[Fact]
	public void Stage3Start_ActorSeesAWall_IsRejectedOnTheActorClient()
	{
		var (host, guest, _) = CreateSessionBlockingObserver(GuestId);
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(77, "bandage", slot: 0)));
		var hostFrames = CaptureHostMessages(host);
		var operations = guest.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		MedicalOperationStartAckMsg? ack = null;
		operations.StartAckReceived += m => ack = m;

		operations.SendOtherStartRequest(HostId, 77, 1, MedicalOperationKind.Bandage);

		Assert.DoesNotContain(NetMsg.MedicalOperationStartRequest, hostFrames);
		Assert.NotNull(ack);
		Assert.False(ack!.Accepted);
		Assert.Equal("No line of sight.", ack.RejectReason);
	}

	[Fact]
	public void HostLocalRequest_IsJudgedByTheHostsOwnClient()
	{
		var (host, guest, received) = CreateSessionBlockingObserver(HostId);
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(77, "bandage", slot: 0)));
		characters.SaveCharacterData(GuestId, SnapshotWithLimbs(GuestId, conscious: false));

		host.Services.GetRequiredService<IPlayerInteractionControl>().SendHealRequest(GuestId, 77);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerHealResult);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 77);
	}
}
