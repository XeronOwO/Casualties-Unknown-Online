using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Handlers;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Permissions;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The cheat permission's two sides, over the real two-node stack (the production
/// composition root, the real console, the real kernel protocol):
/// <list type="bullet">
/// <item>the FRONT END — the member's own console refuses a cheat verb the announced
/// roster does not name, before anything is applied or sent;</item>
/// <item>the BACK END — the host's admission seam judges every request against its
/// own roster and ANSWERS a refusal, and the member settles the value it had already
/// applied back to the recorded one;</item>
/// <item>the edges — the roster reaches a member at admission, at that moment on a
/// grant or revoke, and is dropped with the session that owned it.</item>
/// </list>
///
/// <para>
/// The host's own standing is the online switch, so a host write needs no grant; that
/// is asserted here beside the member's, because a roster that also gated the host
/// would be a different feature.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class CheatPermissionReplicationTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong ThirdId = 3001;
	private const ulong LobbyId = 9001;
	private const ulong Backpack = 4101;

	[Fact]
	public void TheHandshake_PutsAnEmptyRosterOnTheMember()
	{
		// enterWorld: false — the world-entry group announces the same list, so a case
		// about the ADMISSION edge must not let that edge stand in for it.
		var (host, guest, _, _) = CreatePair(enterWorld: false);
		using (host)
		using (guest)
		{
			var announced = guest.Services.GetRequiredService<AnnouncedCheatPermissions>().Current;

			Assert.NotNull(announced);
			Assert.Empty(announced!);
			Assert.True(guest.Services.GetRequiredService<AnnouncedCheatPermissions>().Refuses(GuestId),
				"an announced EMPTY roster closes the cheat verbs locally — that is not the same state as no announcement");
		}
	}

	[Fact]
	public void TheHostsOp_ReachesTheMemberAtThatMoment()
	{
		var (host, guest, _, _) = CreatePair();
		using (host)
		using (guest)
		{
			var answer = Run(host, $"/op {GuestId}");

			Assert.Contains($"Granted the cheat verbs to {GuestId}", answer);
			Assert.Contains(GuestId, guest.Services.GetRequiredService<AnnouncedCheatPermissions>().Current ?? []);
			Assert.False(guest.Services.GetRequiredService<AnnouncedCheatPermissions>().Refuses(GuestId));
		}
	}

	[Fact]
	public void TheHostsDeop_ReachesTheMemberAtThatMoment()
	{
		var (host, guest, _, _) = CreatePair();
		using (host)
		using (guest)
		{
			Run(host, $"/op {GuestId}");

			var answer = Run(host, $"/deop {GuestId}");

			Assert.Contains($"Revoked the cheat verbs from {GuestId}", answer);
			Assert.Empty(guest.Services.GetRequiredService<AnnouncedCheatPermissions>().Current ?? [1]);
			Assert.True(guest.Services.GetRequiredService<AnnouncedCheatPermissions>().Refuses(GuestId));
		}
	}

	/// <summary>
	/// The two standings the roster refuses by name at the command, not at the verb:
	/// the host itself (its permission is the online switch) and a player this session
	/// has never admitted. Both answers name the reason, and neither stores anything.
	/// </summary>
	[Fact]
	public void OpOfTheHostItself_IsRefusedByName()
	{
		var (host, guest, _, _) = CreatePair();
		using (host)
		using (guest)
		{
			var answer = Run(host, $"/op {HostId}");

			Assert.Contains("online switch", answer);
			Assert.Empty(host.Services.GetRequiredService<CheatPermissions>().Granted);
		}
	}

	[Fact]
	public void OpOfSomebodyThisSessionNeverAdmitted_IsRefusedByName()
	{
		var (host, guest, _, _) = CreatePair();
		using (host)
		using (guest)
		{
			var answer = Run(host, "/op 9999");

			Assert.Contains("not a handshaken member", answer);
			Assert.Empty(host.Services.GetRequiredService<CheatPermissions>().Granted);
		}
	}

	/// <summary>
	/// The front end and the back end on one command: before the grant the member's
	/// own console refuses the write (nothing applied, nothing sent — the local object
	/// and the host's row both still hold the old value), and after <c>/op</c> the very
	/// same line lands and the host's kernel records it.
	/// </summary>
	[Fact]
	public void AMembersWrite_IsRefusedLocallyUntilTheGrantLands()
	{
		var (host, guest, _, local) = CreatePair();
		using (host)
		using (guest)
		{
			Hold(host, local, Backpack);

			var refused = Run(guest, $"/data set id {Backpack} condition 0.25");

			Assert.Contains("Refused locally", refused);
			Assert.Contains("cheat roster", refused);
			Assert.Equal(0.5f, local.Held(Backpack)!.Condition);
			Assert.Equal(0.5f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
			Assert.Empty(local.Written);

			Run(host, $"/op {GuestId}");
			var landed = Run(guest, $"/data set id {Backpack} condition 0.25");
			Pump(host, guest);

			Assert.Contains("condition 0.5 -> 0.25", landed);
			Assert.Equal(0.25f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
			Assert.Single(local.Written);
		}
	}

	/// <summary>
	/// The member that never received the roster (the announcement frame is SWALLOWED
	/// on the host-to-member link, the lazy peer session's own failure): its console has
	/// no local opinion, applies and reports as it always did, and the HOST's own
	/// roster refuses the report by name. The answer puts the recorded value back where
	/// the operator typed the command — the same way back the item-data rule's refusal
	/// takes.
	/// </summary>
	[Fact]
	public void AMemberWhoseRosterWasSwallowed_IsSettledByTheHostsOwnRefusal()
	{
		var (host, guest, _, local) = CreatePair(swallowToGuest: NetMsg.CheatPermissions);
		using (host)
		using (guest)
		{
			Assert.Null(guest.Services.GetRequiredService<AnnouncedCheatPermissions>().Current);
			Hold(host, local, Backpack);

			var answer = Run(guest, $"/data set id {Backpack} condition 0.25");
			Pump(host, guest);

			Assert.Contains("condition 0.5 -> 0.25", answer);
			Assert.Equal(0.5f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
			Assert.Equal(0.5f, local.Held(Backpack)!.Condition);
			var refusal = Assert.Single(RefusalLines(guest));
			Assert.Contains("cheat-permission roster does not name you", refusal);
			Assert.Contains($"#{Backpack}", refusal);
		}
	}

	[Fact]
	public void TheHostsOwnWrite_NeedsNoGrant()
	{
		var (host, guest, _, local) = CreatePair();
		using (host)
		using (guest)
		{
			Hold(host, local, Backpack, owner: HostId);

			var answer = Run(host, $"/data set id {Backpack} condition 0.25");

			Assert.Contains("condition 0.5 -> 0.25", answer);
			Assert.Equal(0.25f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
		}
	}

	[Fact]
	public void AMemberTypingOp_IsRefusedAsHostOnly()
	{
		var (host, guest, _, _) = CreatePair();
		using (host)
		using (guest)
		{
			var console = guest.Services.GetRequiredService<ICommandControl>();

			Assert.False(console.TryExecute($"/op {GuestId}"));
			Assert.Contains("host-only", console.Lines[console.Lines.Count - 1].Text);
			Assert.False(host.Services.GetRequiredService<CheatPermissions>().IsGranted(GuestId));
		}
	}

	[Fact]
	public void TheSessionsEnd_DropsTheRosterOnBothSides()
	{
		var (host, guest, _, _) = CreatePair();
		using (host)
		using (guest)
		{
			Run(host, $"/op {GuestId}");
			Assert.True(host.Services.GetRequiredService<CheatPermissions>().IsGranted(GuestId));

			host.Services.GetRequiredService<ISessionControl>().EndSession();
			guest.Services.GetRequiredService<ISessionControl>().EndSession();

			Assert.False(host.Services.GetRequiredService<CheatPermissions>().IsGranted(GuestId), "a grant belongs to the session that made it");
			Assert.Null(guest.Services.GetRequiredService<AnnouncedCheatPermissions>().Current);
		}
	}

	/// <summary>
	/// The ticket's identity decision on the HOST side: an IP-direct host's `/op` refuses
	/// by name and stores nothing. Only the host's transport identity is flipped here —
	/// the member's side of an IP-direct session (its announced empty list and its own
	/// refusal) is an acceptance row the ticket still owes, because it needs a real
	/// IP-direct pair; this case is named for what it proves.
	/// </summary>
	[Fact]
	public void AnIpDirectHost_RefusesTheOpAndKeepsTheRosterEmpty()
	{
		var (host, guest, _, _) = CreatePair(hostIpDirect: true);
		using (host)
		using (guest)
		{
			var answer = Run(host, $"/op {GuestId}");

			Assert.Contains("IP-direct", answer);
			Assert.False(host.Services.GetRequiredService<CheatPermissions>().IsGranted(GuestId));
			Assert.Empty(guest.Services.GetRequiredService<AnnouncedCheatPermissions>().Current ?? []);
		}
	}

	/// <summary>
	/// The local gate's own ORDER, and the only case that can tell the two rulings
	/// apart: the switch is CLOSED and nobody is granted, so the answer must be the
	/// switch's sentence — the broader ruling and the operator's first remedy — and NOT
	/// the roster's. Reversing the gate's order would print the roster sentence and
	/// fail here. `ruleOpen: false` is the same switch the config binds.
	/// </summary>
	[Fact]
	public void AMembersWrite_WhenTheSwitchIsClosedAndNobodyIsGranted_NamesTheSwitchNotTheRoster()
	{
		var (host, guest, _, local) = CreatePair(switchOpen: false);
		using (host)
		using (guest)
		{
			Hold(host, local, Backpack);

			var answer = Run(guest, $"/data set id {Backpack} condition 0.25");

			Assert.Contains("Refused locally", answer);
			Assert.Contains("cheat switch", answer);
			Assert.DoesNotContain("cheat roster", answer);
			Assert.Equal(0.5f, local.Held(Backpack)!.Condition);
			Assert.Empty(local.Written);
			Assert.Equal(0.5f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
		}
	}

	/// <summary>
	/// The sender-anchored rule both announcement handlers gained in the same change:
	/// the wire direction check only proves the LOCAL role, so a frame whose sender is
	/// not this session's host is dropped by name. A third peer sends a roster and a
	/// rules frame straight to the guest; neither store may move.
	/// </summary>
	[Fact]
	public void AnAnnouncementFromANonHostSender_IsDropped()
	{
		var cheatLog = new RecordingLogger<CheatPermissionsHandler>();
		var rulesLog = new RecordingLogger<HostRulesHandler>();
		var (host, guest, network, _) = CreatePair(enterWorld: false, guestServices: services =>
		{
			services.Replace(ServiceDescriptor.Singleton<ILogger<CheatPermissionsHandler>>(cheatLog));
			services.Replace(ServiceDescriptor.Singleton<ILogger<HostRulesHandler>>(rulesLog));
		});
		using (host)
		using (guest)
		{
			var rosterBefore = guest.Services.GetRequiredService<AnnouncedCheatPermissions>().Current;
			var rulesBefore = guest.Services.GetRequiredService<AnnouncedHostRules>().Current;
			var third = TestNode.Create(ThirdId, network,
				new FakeSteamService(ThirdId) { LobbyOwner = HostId, LobbyMembers = [HostId, GuestId, ThirdId] },
				host.Clock, pumpFirstFrame: true);
			try
			{
				host.Steam.LobbyMembers = [HostId, GuestId, ThirdId];
				third.Steam.FireLobbyEntered(LobbyId);
				new SimulationDriver(host.Clock, network, host, third).TickUntil(
					() => host.Session.Members.Any(member => member.SteamId == ThirdId && member.Handshaken),
					maxMs: 5000);

				third.Services.GetRequiredService<PacketSender>().Send(GuestId, NetMsg.CheatPermissions, CheatPermissionsMsg.From([ThirdId]));
				third.Services.GetRequiredService<PacketSender>().Send(GuestId, NetMsg.HostRules, new HostRulesMsg { AllowMemberCheatVerbs = true, NativeBindingParity = (int)NativeBindingParity.Warn });
				guest.Update();

				Assert.Equal(rosterBefore, guest.Services.GetRequiredService<AnnouncedCheatPermissions>().Current);
				Assert.Equal(rulesBefore, guest.Services.GetRequiredService<AnnouncedHostRules>().Current);
				Assert.Contains(cheatLog.Entries, entry => entry.Message.Contains("not this session's host", StringComparison.Ordinal));
				Assert.Contains(rulesLog.Entries, entry => entry.Message.Contains("not this session's host", StringComparison.Ordinal));
			}
			finally
			{
				third.Dispose();
			}
		}
	}

	private static (TestNode Host, TestNode Guest, FakeNetwork Network, FakeLocalItemAccess Local) CreatePair(
		NetMsg? swallowToGuest = null,
		bool hostIpDirect = false,
		bool switchOpen = true,
		bool enterWorld = true,
		Action<IServiceCollection>? guestServices = null)
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var local = new FakeLocalItemAccess();
		var rules = new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions { AllowMemberCheatVerbs = switchOpen });
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var guestSteam = new FakeSteamService(GuestId) { LobbyOwner = HostId, LobbyMembers = [HostId, GuestId] };
		var host = TestNode.Create(HostId, network, hostSteam, clock, pumpFirstFrame: true, extraRegistrations: services =>
		{
			services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(rules));
			services.Replace(ServiceDescriptor.Singleton<ILocalItemAccess>(local));
			if (hostIpDirect)
			{
				services.Replace(ServiceDescriptor.Singleton<ITransportIdentity>(new FakeTransportIdentity { IsIpDirect = true, LocalPeerId = HostId }));
			}
		});
		var guest = TestNode.Create(GuestId, network, guestSteam, clock, pumpFirstFrame: true, extraRegistrations: services =>
		{
			services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(rules));
			services.Replace(ServiceDescriptor.Singleton<ILocalItemAccess>(local));
			guestServices?.Invoke(services);
		});

		if (swallowToGuest is { } dropped)
		{
			// Armed BEFORE the guest joins the lobby: the admission announcement leaves on
			// the handshake edge, so a fault set afterwards would arrive after the frame it
			// is meant to swallow.
			network.SetFaults(HostId, GuestId, new LinkFaults { DropMessageId = dropped });
		}

		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);
		new SimulationDriver(clock, network, host, guest).TickUntil(
			() => host.Session.Members.Any(member => member.SteamId == GuestId && member.Handshaken),
			maxMs: 5000);

		// The world-entry edge is not what this suite judges; the scene report is stated
		// so a host-side write's route and the members' presence are ordinary. A case that
		// asserts the ADMISSION edge itself passes enterWorld: false, so the entry group's
		// own announcement cannot stand in for the handshake's (both send the same list).
		if (enterWorld)
		{
			host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
			guest.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		}

		return (host, guest, network, local);
	}

	/// <summary>The write's two halves: the host's kernel row (the record) and THIS client's local object.</summary>
	private static void Hold(TestNode host, FakeLocalItemAccess local, ulong instanceId, ulong owner = GuestId)
	{
		var item = new CharacterItemMsg
		{
			InstanceId = instanceId,
			ItemId = "backpack",
			SlotIndex = 1,
			Condition = 0.5f,
		};
		local.Hold(item);
		Assert.True(Kernel(host).TrySpawnCarried(owner, instanceId, item.ItemId, item, out _, out var rejection),
			$"seeding item #{instanceId} for {owner} on the host was refused: {rejection?.Reason} ({rejection?.Message})");
	}

	/// <summary>The production frame pump: each node drains what arrived for it, twice, because the member's report and the host's answer each cross one link.</summary>
	private static void Pump(TestNode host, TestNode guest)
	{
		for (var round = 0; round < 2; round++)
		{
			host.Update();
			guest.Update();
		}
	}

	/// <summary>The refusal lines the console is showing — the answer that arrives after the verb's own line.</summary>
	private static IReadOnlyList<string> RefusalLines(TestNode node) =>
		[.. node.Services.GetRequiredService<ICommandControl>().Lines
			.Select(line => line.Text)
			.Where(text => text.StartsWith("The host refused the write to ", StringComparison.Ordinal))];

	/// <summary>Runs one console line and answers with the line it printed.</summary>
	private static string Run(TestNode node, string line)
	{
		var console = node.Services.GetRequiredService<ICommandControl>();
		Assert.True(console.TryExecute(line), $"'{line}' was not accepted by the console");
		return console.Lines[console.Lines.Count - 1].Text;
	}

	private static ItemKernelAuthority Kernel(TestNode node) =>
		node.Services.GetRequiredService<ItemKernelAuthority>();
}
