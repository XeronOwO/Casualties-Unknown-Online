using System.Collections.Generic;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Time;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.PlayerInteractionTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The gate-authority contract, cross-family: the client that acts judges its own
/// reach from its own scene, and the client that owns a body judges what that body
/// allows — the host stops re-judging either from state that lags the two clients.
/// Both halves and both directions are pinned, because a half-migrated family would
/// be MORE permissive than it was: a host that reports "blocked" must not refuse an
/// actor whose own client sees a clear line, an actor whose own client sees a wall
/// must refuse before anything leaves that client, and a target whose own body
/// contradicts the host's stale report must be believed — while a target that cannot
/// be judged at all is refused rather than judged by the host on its behalf.
/// </summary>
[Trait("Category", "Integration")]
public sealed class InteractionGateAuthorityTests
{
	/// <summary>A SteamId no node in these sessions owns: a target that can never answer a body check.</summary>
	private const ulong UnreachableId = 7001;

	/// <summary>A second SteamId no node owns — the "operator leaves" case needs its own parked request.</summary>
	private const ulong UnreachableId2 = 7002;

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
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.DropItem,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
			});

		Assert.DoesNotContain(NetMsg.RemoteInventoryIntentRequest, hostFrames);
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

	// ---- The target's own body decides what its body allows ----

	[Fact]
	public void MedicalStart_TheTargetsOwnBodyOverridesTheHostsStaleReport()
	{
		var guestBody = Snapshot(GuestId, conscious: false);
		var (host, _, _) = CreateSession(extra => extra.Replace(
			ServiceDescriptor.Singleton<ILocalCharacterCapture>(new FixedLocalBodyCapture(guestBody))));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, MedicineBottle(42, "morphine", "morphine")));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true)); // the host's picture disagrees — and is not the judge
		var operations = host.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		MedicalOperationStartAckMsg? ack = null;
		operations.StartAckReceived += m => ack = m;

		operations.SendStartRequest(GuestId, 42, targetLimbIndex: -1);

		Assert.NotNull(ack);
		Assert.False(ack!.Accepted);
		Assert.Equal("Target is not conscious/alive.", ack.RejectReason);
	}

	[Fact]
	public void MedicalStart_TheTargetsOwnBodyOverridesAStaleHostVerdictOfDeath()
	{
		var guestBody = Snapshot(GuestId, conscious: true);
		var (host, _, _) = CreateSession(extra => extra.Replace(
			ServiceDescriptor.Singleton<ILocalCharacterCapture>(new FixedLocalBodyCapture(guestBody))));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, MedicineBottle(42, "morphine", "morphine")));
		characters.SaveCharacterData(GuestId, SnapshotWithLimbs(GuestId, conscious: true, alive: false)); // the host still holds the target for dead
		var operations = host.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		MedicalOperationStartAckMsg? ack = null;
		operations.StartAckReceived += m => ack = m;

		operations.SendStartRequest(GuestId, 42, targetLimbIndex: -1);

		Assert.NotNull(ack);
		Assert.True(ack!.Accepted, ack.RejectReason);
	}

	[Fact]
	public void MedicalStart_ATargetThatCannotBeJudged_IsRefusedRatherThanJudgedByTheHost()
	{
		var (host, _, _) = CreateSession(); // no live body in this composition, so the target has nothing to answer with
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, MedicineBottle(42, "morphine", "morphine")));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true)); // the host's own picture is fine, and no longer the judge
		var operations = host.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		MedicalOperationStartAckMsg? ack = null;
		operations.StartAckReceived += m => ack = m;

		operations.SendStartRequest(GuestId, 42, targetLimbIndex: -1);

		Assert.NotNull(ack);
		Assert.False(ack!.Accepted);
		Assert.Equal("Target body is unavailable.", ack.RejectReason);
	}

	[Fact]
	public void ShrapnelStart_TakesThePieceLayoutFromTheTargetsOwnLimb()
	{
		var guestBody = LimbSnapshot(GuestId, limbIndex: 1, shrapnel: 4);
		var (host, _, _) = CreateSession(extra => extra.Replace(
			ServiceDescriptor.Singleton<ILocalCharacterCapture>(new FixedLocalBodyCapture(guestBody))));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, LimbSnapshot(GuestId, limbIndex: 1, shrapnel: 1)); // the host's stale count says one fragment
		var operations = host.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		MedicalOperationStartAckMsg? ack = null;
		MedicalOperationStateMsg? state = null;
		operations.StartAckReceived += m => ack = m;
		operations.StateReceived += m => state = m;

		operations.SendShrapnelStartRequest(GuestId, 0, 1);

		Assert.NotNull(ack);
		Assert.True(ack!.Accepted, ack.RejectReason);
		Assert.NotNull(state);
		Assert.Equal(4, state!.ShrapnelPieces.FindAll(p => !p.Removed).Count); // the session's live pieces, seeded by the target's own count
	}

	[Fact]
	public void TargetBodyCheck_UnansweredWithinTheLivenessBound_IsAbandonedWithARefusal()
	{
		var (host, _, _) = CreateSession();
		var gate = CreateGate(host);
		MedicalTargetBodyGate.TargetBodyVerdict? verdict = null;

		gate.Begin(HostId, UnreachableId, 1, MedicalOperationKind.Shrapnel, v => verdict = v);

		Assert.Null(verdict); // parked, not decided — the bound abandons a request, it never judges one
		Assert.Equal(1, gate.PendingCount);

		var clock = (FakeClock)host.Services.GetRequiredService<ITimeSource>();
		clock.Advance(4000);
		gate.Tick();

		Assert.NotNull(verdict);
		Assert.False(verdict!.Value.Accepted);
		Assert.Equal("Target did not answer the body check.", verdict.Value.Reason);
		Assert.Equal(0, gate.PendingCount);
	}

	[Fact]
	public void MedicalStart_ALiveBodyThatCannotBeRead_IsRefusedInsteadOfUsingAStoredSnapshot()
	{
		// A live-capture composition whose body cannot be read HAS an answer — "not now" — and
		// must not silently fall back to the stored snapshot, which on a guest is the character
		// it entered the world with.
		var (host, guest, _) = CreateSession(extra => extra.Replace(
			ServiceDescriptor.Singleton<ILocalCharacterCapture>(new FixedLocalBodyCapture(null))));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, MedicineBottle(42, "morphine", "morphine")));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedOwnBody(guest, Snapshot(GuestId, conscious: true)); // a stored snapshot that must NOT be used
		var operations = host.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		MedicalOperationStartAckMsg? ack = null;
		operations.StartAckReceived += m => ack = m;

		operations.SendStartRequest(GuestId, 42, targetLimbIndex: -1);

		Assert.NotNull(ack);
		Assert.False(ack!.Accepted);
		Assert.Equal("Target body is unavailable.", ack.RejectReason);
	}

	[Fact]
	public void TargetBodyCheck_AnAnswerFromSomebodyElse_IsDropped()
	{
		var (host, _, _) = CreateSession();
		var gate = CreateGate(host);
		MedicalTargetBodyGate.TargetBodyVerdict? verdict = null;

		gate.Begin(HostId, UnreachableId, 1, MedicalOperationKind.Shrapnel, v => verdict = v);
		gate.HandleAnswer(GuestId, new MedicalOperationTargetCheckAnswerMsg { RequestId = 1, Accepted = true });

		Assert.Null(verdict); // only the asked target's answer is a verdict
		Assert.Equal(1, gate.PendingCount);
	}

	[Fact]
	public void TargetBodyCheck_ASecondAnswerForOneTicket_IsIgnored()
	{
		var (host, _, _) = CreateSession();
		var gate = CreateGate(host);
		var verdicts = new List<MedicalTargetBodyGate.TargetBodyVerdict>();

		gate.Begin(HostId, UnreachableId, 1, MedicalOperationKind.Shrapnel, verdicts.Add);
		gate.HandleAnswer(UnreachableId, new MedicalOperationTargetCheckAnswerMsg { RequestId = 1, Accepted = true });
		gate.HandleAnswer(UnreachableId, new MedicalOperationTargetCheckAnswerMsg { RequestId = 1, Accepted = false, RejectReason = "late" });

		Assert.Single(verdicts); // one ticket resolves exactly once, however many answers arrive
		Assert.True(verdicts[0].Accepted);
		Assert.Equal(0, gate.PendingCount);
	}

	[Fact]
	public void TargetBodyCheck_TheTargetLeaving_RefusesWhileTheOperatorLeaving_IsSilent()
	{
		var (host, _, _) = CreateSession();
		var gate = CreateGate(host);
		var verdicts = new List<MedicalTargetBodyGate.TargetBodyVerdict>();

		gate.Begin(HostId, UnreachableId, 1, MedicalOperationKind.Shrapnel, verdicts.Add);
		gate.Begin(HostId, UnreachableId2, 1, MedicalOperationKind.Shrapnel, verdicts.Add);
		gate.OnMemberRemoved(UnreachableId); // the target is gone: the operator still deserves an answer
		gate.OnMemberRemoved(HostId);        // the operator is gone: nobody is waiting

		Assert.Single(verdicts);
		Assert.False(verdicts[0].Accepted);
		Assert.Equal("Target left before answering the body check.", verdicts[0].Reason);
		Assert.Equal(0, gate.PendingCount);
	}

	/// <summary>A snapshot whose chosen limb carries a given fragment count — the number only that body's own client can report.</summary>
	private static CharacterDataMsg LimbSnapshot(ulong owner, int limbIndex, int shrapnel)
	{
		var data = SnapshotWithLimbs(owner, conscious: true);
		data.Limbs[limbIndex].Shrapnel = shrapnel;
		return data;
	}

	/// <summary>Stands in for the Game Adapter's live capture: the local body is whatever the test says it is, and a null body means "this client cannot read its body right now".</summary>
	private sealed class FixedLocalBodyCapture(CharacterDataMsg? body) : ILocalCharacterCapture
	{
		public bool HasLiveCapture => true;

		public CharacterDataMsg? CaptureLocal() => body;
	}

	/// <summary>The target-body gate of a live host node, wired to that node's real session, transport and clock.</summary>
	private static MedicalTargetBodyGate CreateGate(TestNode host)
	{
		var session = host.Services.GetRequiredService<ISessionControl>();
		return new MedicalTargetBodyGate(
			session,
			host.Services.GetRequiredService<PacketSender>(),
			new PlayerCharacterAccess(session, host.Services.GetRequiredService<ICharacterDataControl>()),
			host.Services.GetRequiredService<ILocalCharacterCapture>(),
			host.Services.GetRequiredService<ITimeSource>(),
			host.Services.GetRequiredService<ILogger<MedicalTargetBodyGate>>());
	}
}
