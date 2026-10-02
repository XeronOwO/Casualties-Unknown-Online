using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The re-entry baseline contract: the enter-the-world instruction must bring
/// the host's run baseline to a member that stayed in the session while the
/// host moved to a state that member never had. The member's generation
/// boundary only waits for params it does NOT hold, so a member that still
/// holds its previous run's params regenerates its own world unless the
/// baseline arrives WITH the instruction — the world-entry snapshot group is
/// sent at the member's own InWorld edge, which is after that generation.
/// </summary>
[Trait("Category", "Integration")]
public class ReenterBaselineAdoptionTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void WorldJoinAtTheInviteEdge_DeliversTheRestoredRunBaselineToAStayedMember()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var world = host.Services.GetRequiredService<IWorldControl>();
		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();

		// The member already served run 1 through the invite path, so its
		// checkpoint identity expectation is run 1 — not "nothing announced yet".
		world.PublishWorldParams(new WorldStartParams { RandomState = [1, 2, 3] });
		world.SendWorldJoin(isTutorial: false);
		Assert.Equal(1UL, guestAuthority.CurrentRunEpoch.Value);

		// The host Continues: a LOCAL restore re-identifies the run (epoch 2) with a
		// state this member never had, and nothing is broadcast on this path. The
		// member refuses the epoch-2 broadcasts (its own kernel still serves run 1),
		// so the instruction is the only carrier that can reach it before its next
		// generation consumes randomness.
		hostAuthority.ResetSessionState();
		hostAuthority.TryStartRun(
			HostId,
			WorldRunStateMapper.ToRunState(2, new WorldStartParams { RandomState = [7, 7, 7] }),
			out _,
			out _);
		hostAuthority.ObserveSpawn(HostId, 88, "water", 5f, 6f);
		Assert.Null(guestAuthority.FindItem(88));

		// The host's world-entry edge invites the member back.
		world.SendWorldJoin(isTutorial: false);

		// The instruction must have carried the restored run: the member now holds
		// the host's run 2 and its state (and no longer run 1's item).
		Assert.Equal(2UL, guestAuthority.CurrentRunEpoch.Value);
		Assert.NotNull(guestAuthority.FindItem(88));
		Assert.Null(guestAuthority.FindItem(77));
	}

	[Fact]
	public void WorldJoinAnnouncesTheRunBeforeTheCheckpointThatCarriesIt()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var world = host.Services.GetRequiredService<IWorldControl>();
		world.PublishWorldParams(new WorldStartParams { RandomState = [1, 2, 3] });
		world.SendWorldJoin(isTutorial: false);

		// The host moves to another run (its kernel is re-identified): the member
		// refuses a checkpoint set that belongs to a run its last instruction did
		// not announce, so only an instruction BEFORE the set keeps it acceptable.
		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		hostAuthority.ResetSessionState();
		world.PublishWorldParams(new WorldStartParams { RandomState = [7, 7, 7] });
		hostAuthority.ObserveSpawn(HostId, 88, "water", 5f, 6f);
		world.SendWorldJoin(isTutorial: false);

		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.NotNull(guestAuthority.FindItem(88));
		Assert.Equal(2UL, guestAuthority.CurrentRunEpoch.Value);
	}

	[Fact]
	public void AnnouncedBaseline_DropsTheMembersStaleParamsUntilTheCheckpointRestores()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var world = host.Services.GetRequiredService<IWorldControl>();
		var guestWorld = guest.Services.GetRequiredService<IWorldControl>();
		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();

		// The member served run 1 through the invite path: it holds its params.
		world.PublishWorldParams(new WorldStartParams { RandomState = [1, 2, 3] });
		world.SendWorldJoin(isTutorial: false);
		Assert.NotNull(guestWorld.WorldParams);

		// The next instruction announces the restored run and promises its baseline
		// set. While that set is in flight the stale params must be gone, so the
		// generation boundary's existing params wait holds the world.
		hostAuthority.ResetSessionState();
		hostAuthority.TryStartRun(
			HostId,
			WorldRunStateMapper.ToRunState(2, new WorldStartParams { RandomState = [7, 7, 7] }),
			out _,
			out _);
		hostAuthority.ObserveSpawn(HostId, 88, "water", 5f, 6f);
		host.Services.GetRequiredService<PacketSender>().Send(GuestId, NetMsg.WorldJoin, new WorldJoinMsg { RunEpoch = 2, RunBaselineFollows = true });
		Assert.Null(guestWorld.WorldParams);

		// The promised set restores the host's baseline and its run state.
		host.Services.GetRequiredService<IKernelProtocolControl>().SendCheckpoint(GuestId);
		Assert.NotNull(guestWorld.WorldParams);
		Assert.NotNull(guest.Services.GetRequiredService<ItemKernelAuthority>().FindItem(88));
	}

	[Fact]
	public void WorldJoinWithoutTheBaselinePromise_KeepsTheMembersParams()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var guestWorld = guest.Services.GetRequiredService<IWorldControl>();

		// The reconnect shape: the entry group preceded the instruction, so the
		// baseline is already in hand when the instruction arrives with no promise.
		var parameters = new WorldStartParams { RandomState = [1, 2, 3] };
		guestWorld.WorldParams = parameters;
		host.Services.GetRequiredService<PacketSender>().Send(GuestId, NetMsg.WorldJoin, new WorldJoinMsg { RunEpoch = 1, RunBaselineFollows = false });

		Assert.Same(parameters, guestWorld.WorldParams);
	}

	[Fact]
	public void WorldJoinFromAHostWithNoRun_CarriesNoBaselinePromise()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var guestWorld = guest.Services.GetRequiredService<IWorldControl>();
		var parameters = new WorldStartParams { RandomState = [1, 2, 3] };
		guestWorld.WorldParams = parameters;

		// No run on this host: there is no baseline to deliver, so the invite
		// promises none and the params the member holds are left alone.
		host.Services.GetRequiredService<IWorldControl>().SendWorldJoin(isTutorial: false);

		Assert.Same(parameters, guestWorld.WorldParams);
	}

	[Fact]
	public void DuplicateInviteWhileLoading_RepeatsThePromiseUntilTheFollowUpSetRestores()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var world = host.Services.GetRequiredService<IWorldControl>();
		var guestWorld = guest.Services.GetRequiredService<IWorldControl>();
		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();

		// The member served run 1 through the invite path: it holds its params.
		world.PublishWorldParams(new WorldStartParams { RandomState = [1, 2, 3] });
		world.SendWorldJoin(isTutorial: false);
		Assert.NotNull(guestWorld.WorldParams);

		// The host's world-entry edge invites on InWorld alone, so it can fire again
		// while the member is still loading: the promise repeats and the stale params
		// stay dropped until the set behind it lands.
		hostAuthority.ResetSessionState();
		hostAuthority.TryStartRun(
			HostId,
			WorldRunStateMapper.ToRunState(2, new WorldStartParams { RandomState = [7, 7, 7] }),
			out _,
			out _);
		hostAuthority.ObserveSpawn(HostId, 88, "water", 5f, 6f);
		var sender = host.Services.GetRequiredService<PacketSender>();
		var instruction = new WorldJoinMsg { RunEpoch = 2, RunBaselineFollows = true };
		sender.Send(GuestId, NetMsg.WorldJoin, instruction);
		Assert.Null(guestWorld.WorldParams);
		sender.Send(GuestId, NetMsg.WorldJoin, instruction);
		Assert.Null(guestWorld.WorldParams);

		host.Services.GetRequiredService<IKernelProtocolControl>().SendCheckpoint(GuestId);
		Assert.NotNull(guestWorld.WorldParams);
		Assert.NotNull(guest.Services.GetRequiredService<ItemKernelAuthority>().FindItem(88));
	}

	[Fact]
	public void TargetedInvite_DeliversTheHostsRunBaselineToThatMember()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var world = host.Services.GetRequiredService<IWorldControl>();
		world.PublishWorldParams(new WorldStartParams { RandomState = [1, 2, 3] });
		host.Services.GetRequiredService<ItemKernelAuthority>().ObserveSpawn(HostId, 77, "water", 3f, 4f);

		// The respawn path's targeted instruction carries the baseline too.
		world.SendWorldJoinTo(GuestId);

		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.NotNull(guestAuthority.FindItem(77));
		Assert.NotNull(guestAuthority.QueryRun());
	}

	[Fact]
	public void Invite_SkipsAMemberTheHostAlreadyCountsAsInWorld()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		// The member reports InWorld: the invite edge targets members NOT in world.
		guest.Services.GetRequiredService<ISessionControl>().ReportSceneState(SceneStateType.InWorld, "level1", new NetVector2(1f, 2f));
		var world = host.Services.GetRequiredService<IWorldControl>();
		world.PublishWorldParams(new WorldStartParams { RandomState = [1, 2, 3] });
		var guestWorld = guest.Services.GetRequiredService<IWorldControl>();
		var parameters = new WorldStartParams { RandomState = [4, 5, 6] };
		guestWorld.WorldParams = parameters;

		world.SendWorldJoin(isTutorial: false);

		// Nothing reached it, so no promise dropped the params it holds.
		Assert.Same(parameters, guestWorld.WorldParams);
	}

	[Fact]
	public void HostSideJoinEvent_DoesNotDropTheHostsOwnParams()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		// The promise and its drop are guest-side: a host-side raise must leave the
		// host's own published params alone.
		var world = host.Services.GetRequiredService<IWorldControl>();
		var parameters = new WorldStartParams { RandomState = [1, 2, 3] };
		world.WorldParams = parameters;

		world.FireWorldJoinReceived(isTutorial: false, baselineFollows: true);

		Assert.Same(parameters, world.WorldParams);
	}
}
