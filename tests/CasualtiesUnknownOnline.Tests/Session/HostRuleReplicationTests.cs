using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The host rules reaching a member's client, over the real two-node stack: the host's
/// own configuration is what a member reads, a change while the session runs is an
/// event rather than a wait for the next world entry, a swallowed announcement converges
/// on the entry and repair groups, and a member that received no announcement keeps the
/// behaviour it had before the rules were replicated.
///
/// <para>
/// Every case gives the two nodes DIFFERENT local config, because that is the defect
/// this ticket closes: the member's process held its own values under the same rule
/// names and nothing could tell.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class HostRuleReplicationTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong JoinerId = 3001;
	private const ulong LobbyId = 9001;
	private const ulong SecondLobbyId = 9002;

	[Fact]
	public void AMemberWhoseOwnConfigDiffers_ReadsTheHostsRules()
	{
		var hostRules = Monitor(new HostRulesOptions { AllowRemoteInventoryTake = false, AllowMemberItemDataWrite = true, PiggybackWeightMultiplier = 1.5f });
		var guestRules = Monitor(new HostRulesOptions { AllowRemoteInventoryTake = true, AllowMemberItemDataWrite = false, PiggybackWeightMultiplier = 0.1f });
		var (host, guest, network) = CreatePair(hostRules, guestRules);
		Handshake(host, guest, network);

		var member = guest.Services.GetRequiredService<IHostRules>();
		Assert.False(member.AllowRemoteInventoryTake);
		Assert.True(member.AllowMemberItemDataWrite);
		Assert.Equal(1.5f, member.PiggybackWeightMultiplier);

		// The announcement travels one way: the host keeps enforcing its own config.
		var onTheHost = host.Services.GetRequiredService<IHostRules>();
		Assert.False(onTheHost.AllowRemoteInventoryTake);
		Assert.True(onTheHost.AllowMemberItemDataWrite);
		Assert.Equal(1.5f, onTheHost.PiggybackWeightMultiplier);
	}

	[Fact]
	public void AHostFlippingARule_ReachesTheMemberAtThatMoment()
	{
		var hostRules = Monitor(new HostRulesOptions { AllowRemoteInventoryTake = true });
		var guestRules = Monitor(new HostRulesOptions { AllowRemoteInventoryTake = true });
		var (host, guest, network) = CreatePair(hostRules, guestRules);
		Handshake(host, guest, network);
		Assert.True(guest.Services.GetRequiredService<IHostRules>().AllowRemoteInventoryTake);

		hostRules.Set(new HostRulesOptions { AllowRemoteInventoryTake = false });

		// The write happens wherever the config entry is written; the announcement leaves
		// on the host's next frame, which is ONE frame — not a world entry, not a repair
		// cycle. One tick is the whole interval.
		host.Update();

		Assert.False(guest.Services.GetRequiredService<IHostRules>().AllowRemoteInventoryTake);
	}

	/// <summary>
	/// The session BOUNDARY, which is the one state a member-side set must not survive:
	/// the same process was a member (so it holds that host's rules), leaves, and hosts a
	/// lobby of its own. It must judge — and announce — its OWN values, never the ones it
	/// was told, or a host would enforce a stranger's policy on its own members.
	/// </summary>
	[Fact]
	public void AMemberThatThenHosts_JudgesAndAnnouncesItsOwnRules()
	{
		var hostRules = Monitor(new HostRulesOptions { AllowMemberItemDataWrite = true });
		var guestRules = Monitor(new HostRulesOptions { AllowMemberItemDataWrite = false });
		var (host, guest, network) = CreatePair(hostRules, guestRules);
		Handshake(host, guest, network);
		Assert.True(guest.Services.GetRequiredService<IHostRules>().AllowMemberItemDataWrite, "the member reads the host's value");

		// The member leaves that lobby (the lobby-switch path) and opens its own.
		guest.Steam.LobbyOwner = GuestId;
		guest.Steam.LobbyMembers = [GuestId];
		guest.Steam.FireLobbyLeft(LobbyId);
		Assert.Null(guest.Services.GetRequiredService<AnnouncedHostRules>().Current);
		Assert.False(guest.Services.GetRequiredService<IHostRules>().AllowMemberItemDataWrite, "the left session's rules must not survive it");

		guest.Steam.FireLobbyCreated(SecondLobbyId);
		Assert.Equal(SessionRole.Host, guest.Session.Role);

		// A new member joins the new host and is told the NEW host's values.
		var joiner = TestNode.Create(JoinerId, network, new FakeSteamService(JoinerId) { LobbyOwner = GuestId, LobbyMembers = [GuestId, JoinerId] }, guest.Clock, pumpFirstFrame: true);
		try
		{
			guest.Steam.LobbyMembers = [GuestId, JoinerId];
			joiner.Steam.FireLobbyEntered(SecondLobbyId);
			new SimulationDriver(guest.Clock, network, guest, joiner).TickUntil(
				() => guest.Session.Members.Any(m => m.SteamId == JoinerId && m.Handshaken), maxMs: 5000);

			Assert.False(joiner.Services.GetRequiredService<IHostRules>().AllowMemberItemDataWrite,
				"the new host announces its own rule, not the one it was told when it was a member");
		}
		finally
		{
			joiner.Dispose();
		}
	}

	/// <summary>
	/// The receiver's own guard: the one wire field carrying a closed set is checked, and
	/// a level this build does not know is refused WHOLE — this client keeps the set it
	/// holds rather than reading a neighbouring level's meaning (the member's Admin
	/// summary indexes the level straight into its label list).
	/// </summary>
	[Fact]
	public void AnAnnouncementWithAnUnknownParityLevel_IsRefusedWhole()
	{
		var hostRules = Monitor(new HostRulesOptions { AllowMemberItemDataWrite = true });
		var guestRules = Monitor(new HostRulesOptions());
		var (host, guest, network) = CreatePair(hostRules, guestRules, swallowToGuest: NetMsg.HostRules);
		Handshake(host, guest, network);
		Assert.Null(guest.Services.GetRequiredService<AnnouncedHostRules>().Current);

		host.Services.GetRequiredService<PacketSender>().Send(GuestId, NetMsg.HostRules, new HostRulesMsg
		{
			AllowMemberItemDataWrite = true,
			NativeBindingParity = 7,
		});

		Assert.Null(guest.Services.GetRequiredService<AnnouncedHostRules>().Current);
		Assert.False(guest.Services.GetRequiredService<IHostRules>().AllowMemberItemDataWrite);
	}

	[Fact]
	public void TheRespawnHalfOfTheSurface_ChangesLiveToo()
	{
		var hostRules = Monitor(new HostRulesOptions());
		var guestRules = Monitor(new HostRulesOptions());
		var respawn = new MutableOptionsMonitor<RespawnOptions>(new RespawnOptions());
		var (host, guest, network) = CreatePair(hostRules, guestRules, respawn);
		Handshake(host, guest, network);
		Assert.False(guest.Services.GetRequiredService<IHostRules>().Permadeath);

		respawn.Set(new RespawnOptions { Permadeath = true });

		// One frame: the change edge hands the pump a flag rather than sending from the
		// callback (the thread that wrote the entry is not this service's to choose).
		host.Update();

		Assert.True(guest.Services.GetRequiredService<IHostRules>().Permadeath);
	}

	[Fact]
	public void AMemberChangingItsOwnConfig_AnnouncesNothing()
	{
		var hostRules = Monitor(new HostRulesOptions { AllowRemoteInventoryTake = false, AllowMemberItemDataWrite = false });
		var guestRules = Monitor(new HostRulesOptions { AllowRemoteInventoryTake = true, AllowMemberItemDataWrite = false });
		var (host, guest, network) = CreatePair(hostRules, guestRules);
		Handshake(host, guest, network);

		guestRules.Set(new HostRulesOptions { AllowRemoteInventoryTake = true, AllowMemberItemDataWrite = true });

		// The member's own entries are its own: they neither travel to the host nor
		// overrule the set the host announced.
		Assert.False(host.Services.GetRequiredService<IHostRules>().AllowMemberItemDataWrite);
		var member = guest.Services.GetRequiredService<IHostRules>();
		Assert.False(member.AllowMemberItemDataWrite);
		Assert.False(member.AllowRemoteInventoryTake);
	}

	[Fact]
	public void AMemberWhoseAnnouncementWasSwallowed_IsPutRightOnWorldEntry()
	{
		var hostRules = Monitor(new HostRulesOptions { AllowMemberItemDataWrite = true });
		var guestRules = Monitor(new HostRulesOptions { AllowMemberItemDataWrite = false });
		var (host, guest, network) = CreatePair(hostRules, guestRules, swallowToGuest: NetMsg.HostRules);

		Handshake(host, guest, network);
		Assert.Null(guest.Services.GetRequiredService<AnnouncedHostRules>().Current);

		// The entry edge: the member reports its world entry and the entry group follows.
		network.ClearFaults(HostId, GuestId);
		guest.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		guest.Update();

		Assert.True(guest.Services.GetRequiredService<IHostRules>().AllowMemberItemDataWrite);
	}

	[Fact]
	public void AMemberWhoseAnnouncementWasSwallowed_IsPutRightByTheRepairGroup()
	{
		var hostRules = Monitor(new HostRulesOptions { AllowMemberItemDataWrite = true });
		var guestRules = Monitor(new HostRulesOptions { AllowMemberItemDataWrite = false });
		var (host, guest, network) = CreatePair(hostRules, guestRules, swallowToGuest: NetMsg.HostRules);

		Handshake(host, guest, network);
		Assert.Null(guest.Services.GetRequiredService<AnnouncedHostRules>().Current);

		network.ClearFaults(HostId, GuestId);
		host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(GuestId);

		Assert.True(guest.Services.GetRequiredService<IHostRules>().AllowMemberItemDataWrite);
	}

	[Fact]
	public void AMemberThatNeverReceivedTheSet_KeepsItsOwnRulesAndSkipsTheLocalCheck()
	{
		var hostRules = Monitor(new HostRulesOptions { AllowMemberItemDataWrite = false });
		var guestRules = Monitor(new HostRulesOptions { AllowMemberItemDataWrite = true });
		var (host, guest, network) = CreatePair(hostRules, guestRules, swallowToGuest: NetMsg.HostRules);

		Handshake(host, guest, network);

		var member = guest.Services.GetRequiredService<IHostRules>();
		Assert.True(member.AllowMemberItemDataWrite, "with nothing announced the member reads its own config — the pre-replication behaviour");
		Assert.False(guest.Services.GetRequiredService<AnnouncedHostRules>().Forbids(HostRuleId.AllowMemberItemDataWrite),
			"no announcement means the local check is skipped, never a guess at what the host meant");
	}

	private static MutableOptionsMonitor<HostRulesOptions> Monitor(HostRulesOptions options) => new(options);

	/// <summary>
	/// Two nodes with DIFFERENT rule config, over one fake network — the standard pair
	/// builder takes one registration set for both, and the whole point here is that the
	/// two processes disagree locally.
	/// </summary>
	private static (TestNode Host, TestNode Guest, FakeNetwork Network) CreatePair(
		MutableOptionsMonitor<HostRulesOptions> hostRules,
		MutableOptionsMonitor<HostRulesOptions> guestRules,
		MutableOptionsMonitor<RespawnOptions>? hostRespawn = null,
		NetMsg? swallowToGuest = null)
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var guestSteam = new FakeSteamService(GuestId) { LobbyOwner = HostId, LobbyMembers = [HostId, GuestId] };
		var host = TestNode.Create(HostId, network, hostSteam, clock, pumpFirstFrame: true, extraRegistrations: services =>
		{
			services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(hostRules));
			if (hostRespawn is not null)
			{
				services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<RespawnOptions>>(hostRespawn));
			}
		});
		var guest = TestNode.Create(GuestId, network, guestSteam, clock, pumpFirstFrame: true, extraRegistrations: Rules(guestRules));
		if (swallowToGuest is { } dropped)
		{
			// Armed BEFORE the guest joins the lobby: the handshake is driven by that
			// edge, so a fault set afterwards would arrive after the announcement it is
			// meant to swallow.
			network.SetFaults(HostId, GuestId, new LinkFaults { DropMessageId = dropped });
		}

		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);
		return (host, guest, network);
	}

	private static Action<IServiceCollection> Rules(MutableOptionsMonitor<HostRulesOptions> monitor) =>
		services => services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(monitor));

	/// <summary>The three-leg handshake, pumped until both sides count the other as handshaken — which is also when the host announces.</summary>
	private static void Handshake(TestNode host, TestNode guest, FakeNetwork network) =>
		new SimulationDriver(host.Clock, network, host, guest).TickUntil(
			() => host.Session.Members.Any(m => m.SteamId == GuestId && m.Handshaken)
				&& guest.Session.Members.Any(m => m.SteamId == HostId && m.Handshaken),
			maxMs: 5000);
}
