using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Handlers;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Permissions;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The <c>/gamerule</c> MEMBER half: the verb joins the cheat family, so a member's write is a request
/// whose answer arrives on the wire and whose write happens on the HOST's config entry.
///
/// <para>
/// The front end (the member's own console) refuses on an announced closed switch or an announced roster
/// that does not name it: no round trip, and the member's own config is never touched. The back end (the
/// host) judges the same two rulings against its own state, re-parses the value with the same grammar, and
/// answers with the value the entry REALLY holds after the write. The swallowed-announcement case proves
/// the second half is real: the member's check is skipped, the request arrives, and the host's verdict is
/// what the operator reads.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleMemberGameRuleTests
{
	private const string InForceAnswer = "Game rule remote_backpack_access is now set to: unconscious_or_dead.";

	[Fact]
	public void Member_SetsARule_TheHostWritesItAndTheAnswerPrints()
	{
		var (host, guest, hostEditor, _, _) = CreateSession(switchOpen: true, grant: true);

		var answer = Run(guest, "/gamerule remote_backpack_access unconscious_or_dead");

		Assert.Equal(InForceAnswer, answer);
		Assert.Contains((HostRuleId.RemoteBackpackAccess, "unconscious_or_dead"), hostEditor.Applied);
		Assert.Equal(RemoteBackpackAccess.UnconsciousOrDeadOnly, host.Services.GetRequiredService<IHostRules>().RemoteBackpackAccess);
	}

	[Fact]
	public void Member_NeverWritesItsOwnConfig()
	{
		var (_, guest, hostEditor, guestEditor, _) = CreateSession(switchOpen: true, grant: true);

		Run(guest, "/gamerule remote_backpack_access unconscious_or_dead");

		Assert.Single(hostEditor.Applied);
		Assert.Empty(guestEditor.Applied);
	}

	[Fact]
	public void Member_TheHostsChangeEdgeReachesTheMemberOnTheNextPump()
	{
		var (host, guest, _, _, _) = CreateSession(switchOpen: true, grant: true);
		var announcedBefore = guest.Services.GetRequiredService<AnnouncedHostRules>().Current;
		Assert.NotNull(announcedBefore);

		Run(guest, "/gamerule remote_backpack_access unconscious_or_dead");
		host.Update();

		var announced = guest.Services.GetRequiredService<AnnouncedHostRules>().Current;
		Assert.NotNull(announced);
		Assert.Equal(RemoteBackpackAccess.UnconsciousOrDeadOnly, announced!.Value.Access(HostRuleId.RemoteBackpackAccess));
	}

	[Fact]
	public void Member_WhenTheAnnouncedSwitchIsClosed_IsRefusedLocallyWithoutARequest()
	{
		var (host, guest, hostEditor, _, _) = CreateSession(switchOpen: false, grant: true);
		var requests = RecordRequests(host);

		var answer = Run(guest, "/gamerule remote_backpack_access unconscious_or_dead");

		Assert.Contains("Refused locally", answer);
		Assert.Contains("AllowMemberCheatVerbs", answer);
		Assert.DoesNotContain(NetMsg.HostRuleChangeRequest, requests);
		Assert.Empty(hostEditor.Applied);
	}

	[Fact]
	public void Member_NotOnTheAnnouncedRoster_IsRefusedLocallyWithoutARequest()
	{
		var (host, guest, hostEditor, _, _) = CreateSession(switchOpen: true, grant: false);
		var requests = RecordRequests(host);

		var answer = Run(guest, "/gamerule remote_backpack_access unconscious_or_dead");

		Assert.Contains("Refused locally", answer);
		Assert.Contains("cheat roster", answer);
		Assert.DoesNotContain("cheat switch", answer);
		Assert.DoesNotContain(NetMsg.HostRuleChangeRequest, requests);
		Assert.Empty(hostEditor.Applied);
	}

	[Fact]
	public void Member_RevokedAfterTheGrant_IsRefusedLocallyWithoutARequest()
	{
		var (host, guest, hostEditor, _, _) = CreateSession(switchOpen: true, grant: true);
		var permissions = host.Services.GetRequiredService<CheatPermissions>();
		Assert.True(permissions.Revoke(guest.SteamId, out var refusal), refusal);
		host.Services.GetRequiredService<ICheatPermissionAnnouncement>().AnnounceAll();
		var requests = RecordRequests(host);

		var answer = Run(guest, "/gamerule remote_backpack_access unconscious_or_dead");

		Assert.Contains("Refused locally", answer);
		Assert.Contains("cheat roster", answer);
		Assert.DoesNotContain(NetMsg.HostRuleChangeRequest, requests);
		Assert.Empty(hostEditor.Applied);
	}

	[Fact]
	public void Member_InvalidValue_IsRefusedLocallyWithoutARequest()
	{
		var (host, guest, hostEditor, _, _) = CreateSession(switchOpen: true, grant: true);
		var requests = RecordRequests(host);

		var answer = Run(guest, "/gamerule remote_backpack_access maybe");

		Assert.Contains("'maybe' is not one of allow, unconscious_or_dead, deny.", answer);
		Assert.DoesNotContain(NetMsg.HostRuleChangeRequest, requests);
		Assert.Empty(hostEditor.Applied);
	}

	[Fact]
	public void Member_UnknownRule_IsRefusedLocallyWithoutARequest()
	{
		var (host, guest, hostEditor, _, _) = CreateSession(switchOpen: true, grant: true);
		var requests = RecordRequests(host);

		var answer = Run(guest, "/gamerule nope");

		Assert.Contains("Unknown game rule 'nope'.", answer);
		Assert.DoesNotContain(NetMsg.HostRuleChangeRequest, requests);
		Assert.Empty(hostEditor.Applied);
	}

	[Fact]
	public void Member_WithNoAnnouncedRoster_IsSettledByTheHostsOwnRefusal()
	{
		var (_, guest, hostEditor, _, _) = CreateSession(switchOpen: true, grant: false, announceRoster: false);
		Assert.Null(guest.Services.GetRequiredService<AnnouncedCheatPermissions>().Current);

		var answer = Run(guest, "/gamerule remote_backpack_access unconscious_or_dead");

		Assert.Contains("the host's cheat roster does not name you", answer);
		Assert.Empty(hostEditor.Applied);
	}

	[Fact]
	public void Member_GrantedButAnnouncementSwallowed_IsServedByTheHost()
	{
		var (_, guest, hostEditor, _, _) = CreateSession(switchOpen: true, grant: true, announceRoster: false);

		var answer = Run(guest, "/gamerule remote_backpack_access unconscious_or_dead");

		Assert.Equal(InForceAnswer, answer);
		Assert.Contains((HostRuleId.RemoteBackpackAccess, "unconscious_or_dead"), hostEditor.Applied);
	}

	[Fact]
	public void Member_SetsARule_WhileTheHostIsOutOfWorld_IsStillServed()
	{
		// A rule write needs no world, and the host's own verb applies in exactly this state: a world
		// guard on the request would swallow a granted member's line with no answer and no write.
		var (host, guest, hostEditor, _, _) = CreateSession(switchOpen: true, grant: true, hostInWorld: false);
		Assert.False(host.Session.LocalInWorld);

		var answer = Run(guest, "/gamerule remote_backpack_access unconscious_or_dead");

		Assert.Equal(InForceAnswer, answer);
		Assert.Contains((HostRuleId.RemoteBackpackAccess, "unconscious_or_dead"), hostEditor.Applied);
	}

	[Fact]
	public void Member_Query_AnswersTheAnnouncedValue()
	{
		var (host, guest, _, _, _) = CreateSession(switchOpen: true, grant: true);
		Assert.Equal(
			"Game rule remote_backpack_access is now set to: unconscious_or_dead.",
			Run(host, "/gamerule remote_backpack_access unconscious_or_dead"));
		host.Update();

		var answer = Run(guest, "/gamerule remote_backpack_access");

		Assert.Equal("Game rule remote_backpack_access is currently set to: unconscious_or_dead.", answer);
	}

	[Fact]
	public void Member_WithNoAnnouncedSwitch_IsSettledByTheHostsOwnSwitch()
	{
		var (_, guest, hostEditor, _, _) = CreateSession(switchOpen: false, grant: true, announceSwitch: false);
		Assert.Null(guest.Services.GetRequiredService<AnnouncedHostRules>().Current);

		var answer = Run(guest, "/gamerule remote_backpack_access unconscious_or_dead");

		Assert.Contains("the host's cheat switch", answer);
		Assert.Empty(hostEditor.Applied);
	}

	[Fact]
	public void Host_RefusesAValueThatIsNotTheCanonicalForm()
	{
		var (host, guest, hostEditor, _, _) = CreateSession(switchOpen: true, grant: true);
		EnsureConsole(guest);
		guest.Services.GetRequiredService<PacketSender>().Send(
			host.SteamId,
			NetMsg.HostRuleChangeRequest,
			new HostRuleChangeRequestMsg
			{
				Rule = (int)HostRuleId.PiggybackWeightMultiplier,
				Value = "0.50000",
			});

		Assert.Contains("'0.50000' is not the canonical form of piggyback_weight_multiplier.", LastLine(guest));
		Assert.Empty(hostEditor.Applied);
	}

	/// <summary>
	/// The answer handler's sender-anchored rule: the wire direction check proves only the LOCAL role, so a
	/// frame whose sender is not this session's host is a CLAIM and is dropped by name. A third peer sends a
	/// forged answer straight to the member; the member's console must not print it.
	/// </summary>
	[Fact]
	public void AnAnswerFromANonHostSender_IsDropped()
	{
		const ulong ThirdId = 3001;
		var log = new RecordingLogger<HostRuleChangeResultHandler>();
		var (host, guest, network) = TestNode.CreatePairWithNetwork(
			HostId,
			GuestId,
			LobbyId,
			guestRegistrations: services => services.Replace(
				ServiceDescriptor.Singleton<ILogger<HostRuleChangeResultHandler>>(log)));
		MarkInWorld(host);
		MarkInWorld(guest);
		var console = guest.Services.GetRequiredService<ICommandControl>();
		var linesBefore = console.Lines.Count;
		var third = TestNode.Create(
			ThirdId,
			network,
			new FakeSteamService(ThirdId) { LobbyOwner = HostId, LobbyMembers = [HostId, GuestId, ThirdId] },
			host.Clock,
			pumpFirstFrame: true);
		try
		{
			third.Services.GetRequiredService<PacketSender>().Send(
				GuestId,
				NetMsg.HostRuleChangeResult,
				new HostRuleChangeResultMsg { Applied = true, Answer = "Game rule pvp_enabled is now set to: true." });

			Assert.Equal(linesBefore, console.Lines.Count);
			Assert.Contains(log.Entries, entry => entry.Message.Contains("not this session's host", StringComparison.Ordinal));
		}
		finally
		{
			third.Dispose();
		}
	}

	[Fact]
	public void Member_WhenTheHostsWriteIsRefused_ReadsTheHostsReason()
	{
		var (_, guest, hostEditor, _, _) = CreateSession(
			switchOpen: true,
			grant: true,
			editorRefusal: "the configuration could not be saved");

		var answer = Run(guest, "/gamerule remote_backpack_access unconscious_or_dead");

		Assert.Contains("Game rule remote_backpack_access was not changed: the configuration could not be saved", answer);
		Assert.Empty(hostEditor.Applied);
	}

	[Fact]
	public void Host_Set_AnswersTheValueTheEntryHolds_NotTheValueItAskedFor()
	{
		var (host, _, _, _, _) = CreateSession(
			switchOpen: false,
			grant: false,
			inForceAfterWrite: RemoteBackpackAccessText.Format(RemoteBackpackAccess.Deny));

		var answer = Run(host, "/gamerule remote_backpack_access allow");

		Assert.Equal("Game rule remote_backpack_access is now set to: deny.", answer);
		Assert.Equal(RemoteBackpackAccess.Deny, host.Services.GetRequiredService<IHostRules>().RemoteBackpackAccess);
	}

	[Fact]
	public void Host_RefusesARequestWithAValueItsOwnGrammarDoesNotAccept()
	{
		var (host, guest, hostEditor, _, _) = CreateSession(switchOpen: true, grant: true);
		var requests = RecordRequests(host);
		EnsureConsole(guest);
		guest.Services.GetRequiredService<PacketSender>().Send(
			host.SteamId,
			NetMsg.HostRuleChangeRequest,
			new HostRuleChangeRequestMsg
			{
				Rule = (int)HostRuleId.RemoteBackpackAccess,
				Value = "ALLOW",
			});

		Assert.Contains(NetMsg.HostRuleChangeRequest, requests);
		Assert.Contains("'ALLOW' is not one of allow, unconscious_or_dead, deny", LastLine(guest));
		Assert.Empty(hostEditor.Applied);
	}

	[Fact]
	public void Host_RefusesARequestNamingARuleThisBuildDoesNotKnow()
	{
		var (host, guest, hostEditor, _, _) = CreateSession(switchOpen: true, grant: true);
		var requests = RecordRequests(host);
		EnsureConsole(guest);
		guest.Services.GetRequiredService<PacketSender>().Send(
			host.SteamId,
			NetMsg.HostRuleChangeRequest,
			new HostRuleChangeRequestMsg { Rule = 999, Value = "true" });

		Assert.Contains(NetMsg.HostRuleChangeRequest, requests);
		Assert.Contains("Unknown game rule id 999", LastLine(guest));
		Assert.Empty(hostEditor.Applied);
	}

	/// <summary>
	/// The session these cases read: the host's cheat switch as the host's own configuration (announced
	/// to the member on the handshake), the roster granted only when the case is about WHO, and the roster
	/// announcement swallowed when the case is about the host's own back end.
	/// </summary>
	private static (TestNode Host, TestNode Guest, StubHostRulesEditor HostEditor, StubHostRulesEditor GuestEditor, MutableOptionsMonitor<HostRulesOptions> Rules) CreateSession(
		bool switchOpen,
		bool grant,
		bool announceRoster = true,
		bool announceSwitch = true,
		bool hostInWorld = true,
		string? inForceAfterWrite = null,
		string? editorRefusal = null)
	{
		var rules = new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions { AllowMemberCheatVerbs = switchOpen });
		var respawn = new MutableOptionsMonitor<RespawnOptions>(new RespawnOptions());
		var hostEditor = new StubHostRulesEditor(rules, respawn)
		{
			InForceAfterWrite = inForceAfterWrite,
			Refusal = editorRefusal,
		};
		// The member's own editor writes the member's own monitors if it is ever called: the point of the
		// case is that it never is (the request travels to the host instead).
		var guestEditor = new StubHostRulesEditor(
			new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions()),
			new MutableOptionsMonitor<RespawnOptions>(new RespawnOptions()));

		// One frame can be swallowed per pair: the roster when the case is about the host's WHO ruling,
		// the rules set when it is about the switch's.
		var swallowed = !announceRoster ? NetMsg.CheatPermissions : !announceSwitch ? NetMsg.HostRules : (NetMsg?)null;
		var (host, guest, _) = TestNode.CreatePairWithNetwork(
			HostId,
			GuestId,
			LobbyId,
			extraRegistrations: services =>
			{
				services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(rules));
				services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<RespawnOptions>>(respawn));
				services.Replace(ServiceDescriptor.Singleton<IHostRulesEditor>(hostEditor));
			},
			swallowHostToGuest: swallowed,
			guestRegistrations: services => services.Replace(ServiceDescriptor.Singleton<IHostRulesEditor>(guestEditor)));
		if (hostInWorld)
		{
			MarkInWorld(host);
		}

		MarkInWorld(guest);

		if (grant)
		{
			var permissions = host.Services.GetRequiredService<CheatPermissions>();
			Assert.True(permissions.Grant(guest.SteamId, out var refusal), $"the host grants the member the cheat verbs: {refusal}");
			host.Services.GetRequiredService<ICheatPermissionAnnouncement>().AnnounceAll();
		}

		return (host, guest, hostEditor, guestEditor, rules);
	}

	/// <summary>Every message id the host receives, so a case can prove a refusal never became a request.</summary>
	private static List<NetMsg> RecordRequests(TestNode host)
	{
		var requests = new List<NetMsg>();
		host.Transport.MessageReceived += (_, frame) => requests.Add((NetMsg)frame[0]);
		return requests;
	}

	/// <summary>
	/// Resolve the console before driving a frame by hand: the service subscribes to the answer event in its
	/// constructor, so a case that injects a request directly must build it first or the answer has no reader.
	/// </summary>
	private static void EnsureConsole(TestNode node) => node.Services.GetRequiredService<ICommandControl>();

	private static string Run(TestNode node, string line)
	{
		var console = node.Services.GetRequiredService<ICommandControl>();
		Assert.True(console.TryExecute(line), $"'{line}' was not accepted by the console");
		return LastLine(node);
	}

	private static string LastLine(TestNode node)
	{
		var console = node.Services.GetRequiredService<ICommandControl>();
		return console.Lines[console.Lines.Count - 1].Text;
	}
}
