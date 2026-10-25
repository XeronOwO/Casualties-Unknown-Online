using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Handlers;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The <c>/heal</c> MEMBER half: the member's verb is a request, and both of the host's cheat
/// rulings are exercised where each really decides. The front end (the member's own console)
/// refuses on an ANNOUNCED closed switch or a roster that does not name it — no round trip and no
/// body touched. The back end (the host) judges the same rulings against its own state for every
/// request that does arrive, and its answer is the sentence the member's console prints. The
/// swallowed-announcement case is what proves the second half is real.
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleMemberHealResetTests
{
	[Fact]
	public void Member_HealsTheHost_LandsAndTheHostsAnswerPrints()
	{
		var (host, guest, reset) = CreateMemberHealSession(switchOpen: true, grant: true);
		SeedBoth(host, guest);

		var answer = Run(guest, "/heal @a[name=1001]");

		Assert.Equal("Reset 1001: full heal applied.", answer);
		Assert.Equal(1, reset.Resets);
	}

	[Fact]
	public void Member_HealsItself_ReachesItsOwnClientAndTheAnswerPrints()
	{
		var (host, guest, reset) = CreateMemberHealSession(switchOpen: true, grant: true);
		SeedBoth(host, guest);

		var console = guest.Services.GetRequiredService<ICommandControl>();
		var before = console.Lines.Count;
		Assert.True(console.TryExecute("/heal @s"));

		Assert.Equal(1, reset.Resets);
		var newLines = console.Lines.Skip(before).Select(line => line.Text).ToArray();
		Assert.Contains(newLines, text => text.StartsWith("2001 fully reset your state", StringComparison.Ordinal));
		Assert.Contains(newLines, text => text.StartsWith("Reset 2001: sent", StringComparison.Ordinal));
	}

	[Fact]
	public void Member_Heals_WhenTheAnnouncedSwitchIsClosed_IsRefusedLocally()
	{
		var (host, guest, reset) = CreateMemberHealSession(switchOpen: false, grant: true);
		SeedBoth(host, guest);
		var requests = RequestsToHost(host);

		var answer = Run(guest, "/heal @a[name=1001]");

		Assert.Contains("Refused locally", answer);
		Assert.Contains("AllowMemberCheatVerbs", answer);
		Assert.Equal(0, reset.Resets);
		Assert.DoesNotContain(NetMsg.HealResetRequest, requests);
	}

	[Fact]
	public void Member_Heals_WhenTheSwitchIsOpenButTheMemberIsNotGranted_IsRefusedByTheRoster()
	{
		var (host, guest, reset) = CreateMemberHealSession(switchOpen: true, grant: false);
		SeedBoth(host, guest);
		var requests = RequestsToHost(host);

		var answer = Run(guest, "/heal @a[name=1001]");

		Assert.Contains("cheat roster", answer);
		Assert.DoesNotContain("cheat switch", answer);
		Assert.Equal(0, reset.Resets);
		Assert.DoesNotContain(NetMsg.HealResetRequest, requests);
	}

	/// <summary>The member whose roster announcement never arrived: its own check is skipped, the request reaches the host, and the host's refusal is the answer.</summary>
	[Fact]
	public void Member_Heals_WhenNoRosterWasAnnounced_IsSettledByTheHostsOwnRefusal()
	{
		var (host, guest, reset) = CreateMemberHealSession(switchOpen: true, grant: false, announceRoster: false);
		SeedBoth(host, guest);

		var answer = Run(guest, "/heal @a[name=1001]");

		Assert.Contains("the host's cheat roster does not name you", answer);
		Assert.Equal(0, reset.Resets);
	}

	/// <summary>
	/// The back end's switch ruling, where the front end cannot have seen it: the rules announcement
	/// never arrived, so the member's own switch check is skipped and the request reaches the host —
	/// which refuses it with its own switch sentence. Without this case the host's
	/// <c>CheatVerbsOpenToMembers</c> branch is never exercised and could be deleted green.
	/// </summary>
	[Fact]
	public void Member_Heals_WhenTheRulesAnnouncementNeverArrived_IsRefusedByTheHostsOwnSwitch()
	{
		var (host, guest, reset) = CreateMemberHealSession(switchOpen: false, grant: true, announceRules: false);
		SeedBoth(host, guest);

		var answer = Run(guest, "/heal @a[name=1001]");

		Assert.Contains("the host's cheat switch (AllowMemberCheatVerbs) is off", answer);
		Assert.Equal(0, reset.Resets);
	}

	/// <summary>
	/// The instruction handler's sender rule. This is the only host-addressed frame that MUTATES a
	/// body, so a forged one is the frame that matters most: a third peer sends it straight to the
	/// member, and the member must neither reset its body nor print a notice.
	/// </summary>
	[Fact]
	public void AResetInstructionFromANonHostSender_IsDropped()
	{
		const ulong ThirdId = 3001;
		var log = new RecordingLogger<HealResetInstructionHandler>();
		var (host, guest, reset) = CreateMemberHealSession(switchOpen: true, grant: true, instructionLog: log);
		SeedBoth(host, guest);
		using (host)
		using (guest)
		{
			var console = guest.Services.GetRequiredService<ICommandControl>();
			var linesBefore = console.Lines.Count;
			var third = CreateThirdPeer(host, guest, ThirdId);
			try
			{
				third.Services.GetRequiredService<PacketSender>().Send(GuestId, NetMsg.HealResetInstruction,
					new HealResetInstructionMsg { OperatorSteamId = HostId });
				guest.Update();

				Assert.Equal(0, reset.Resets);
				Assert.Equal(linesBefore, console.Lines.Count);
				Assert.Contains(log.Entries, entry => entry.Message.Contains("not this session's host", StringComparison.Ordinal));
			}
			finally
			{
				third.Dispose();
			}
		}
	}

	/// <summary>A hand-built request past the cap is refused whole, before any target is looked up or instructed.</summary>
	[Fact]
	public void ARequestPastTheTargetCap_IsRefusedWhole()
	{
		var (host, guest, reset) = CreateMemberHealSession(switchOpen: true, grant: true);
		SeedBoth(host, guest);
		var console = guest.Services.GetRequiredService<ICommandControl>();
		var ids = new List<ulong>(33);
		for (var i = 0; i < 33; i++)
		{
			ids.Add(3000UL + (ulong)i);
		}

		guest.Services.GetRequiredService<PacketSender>().Send(HostId, NetMsg.HealResetRequest,
			new HealResetRequestMsg { TargetSteamIds = ids });

		Assert.Contains("accepts at most 32", console.Lines[console.Lines.Count - 1].Text);
		Assert.Equal(0, reset.Resets);
	}

	/// <summary>The answer handler's sender rule: a frame from a peer that is not this session's host is a claim and must not print.</summary>
	[Fact]
	public void AnAnswerFromANonHostSender_IsDropped()
	{
		const ulong ThirdId = 3001;
		var log = new RecordingLogger<HealResetResultHandler>();
		var (host, guest, _) = CreateMemberHealSession(switchOpen: true, grant: true, thirdPeerLog: log);
		SeedBoth(host, guest);
		using (host)
		using (guest)
		{
			var console = guest.Services.GetRequiredService<ICommandControl>();
			var linesBefore = console.Lines.Count;
			var third = CreateThirdPeer(host, guest, ThirdId);
			try
			{
				third.Services.GetRequiredService<PacketSender>().Send(GuestId, NetMsg.HealResetResult,
					new HealResetResultMsg { Applied = true, Answer = "Gave the member a fake reset." });
				guest.Update();

				Assert.Equal(linesBefore, console.Lines.Count);
				Assert.Contains(log.Entries, entry => entry.Message.Contains("not this session's host", StringComparison.Ordinal));
			}
			finally
			{
				third.Dispose();
			}
		}
	}

	/// <summary>One third node on the session's own network, handshaken by the host so its forged frame is a real peer's.</summary>
	private static TestNode CreateThirdPeer(TestNode host, TestNode guest, ulong thirdId)
	{
		var third = TestNode.Create(thirdId, guest.Transport.Network,
			new FakeSteamService(thirdId) { LobbyOwner = HostId, LobbyMembers = [HostId, GuestId, thirdId] },
			host.Clock, pumpFirstFrame: true);
		host.Steam.LobbyMembers = [HostId, GuestId, thirdId];
		third.Steam.FireLobbyEntered(LobbyId);
		new SimulationDriver(host.Clock, guest.Transport.Network, host, third).TickUntil(
			() => host.Session.Members.Any(member => member.SteamId == thirdId && member.Handshaken),
			maxMs: 5000);
		return third;
	}

	/// <summary>
	/// A member's console case needs the switch OPEN as the host's own configuration (announced on
	/// the handshake), the roster granted only when the case is about the WHO half, and each side's
	/// view of the other seeded so the selector resolves. The body fixture is shared by both nodes
	/// exactly as the fake network is; only the node whose body is the target calls it.
	/// </summary>
	private static (TestNode Host, TestNode Guest, FakeLocalBodyReset Reset) CreateMemberHealSession(
		bool switchOpen,
		bool grant,
		bool announceRoster = true,
		bool announceRules = true,
		RecordingLogger<HealResetResultHandler>? thirdPeerLog = null,
		RecordingLogger<HealResetInstructionHandler>? instructionLog = null)
	{
		var reset = new FakeLocalBodyReset();
		var rules = new MutableOptionsMonitor<HostRulesOptions>(
			new HostRulesOptions { AllowMemberCheatVerbs = switchOpen });
		var (host, guest) = CreateSession(
			services =>
			{
				services.Replace(ServiceDescriptor.Singleton<ILocalBodyReset>(reset));
				services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(rules));
				if (thirdPeerLog is not null)
				{
					services.Replace(ServiceDescriptor.Singleton<ILogger<HealResetResultHandler>>(thirdPeerLog));
				}

				if (instructionLog is not null)
				{
					services.Replace(ServiceDescriptor.Singleton<ILogger<HealResetInstructionHandler>>(instructionLog));
				}
			},
			swallowHostToGuest: !announceRules ? NetMsg.HostRules : announceRoster ? null : NetMsg.CheatPermissions);

		if (grant)
		{
			GrantAndAnnounceCheatVerbs(host, guest);
		}

		return (host, guest, reset);
	}

	/// <summary>Both sides' views of each other: the host's snapshots and entity table, and the member's own entity table for <c>@p</c>.</summary>
	private static void SeedBoth(TestNode host, TestNode guest)
	{
		Characters(host).SaveHostCharacterData(Snapshot(HostId, conscious: true));
		Characters(host).SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, 5f);
		guest.Services.GetRequiredService<IEntitySyncControl>().ProcessPlayerJoin(new PlayerJoinMsg
		{
			HostSteamId = HostId,
			GuestSteamId = GuestId,
			HostPosition = new NetVector2Msg(0f, 0f),
			GuestPosition = new NetVector2Msg(5f, 0f),
		});
	}

	private static List<NetMsg> RequestsToHost(TestNode host)
	{
		var requests = new List<NetMsg>();
		host.Transport.MessageReceived += (_, frame) => requests.Add((NetMsg)frame[0]);
		return requests;
	}

	private static ICharacterDataControl Characters(TestNode node) =>
		node.Services.GetRequiredService<ICharacterDataControl>();

	/// <summary>Runs one console line and answers with the line it printed — the host's answer line, for a member's verb.</summary>
	private static string Run(TestNode node, string line)
	{
		var console = node.Services.GetRequiredService<ICommandControl>();
		Assert.True(console.TryExecute(line), $"'{line}' was not accepted by the console");
		return console.Lines[console.Lines.Count - 1].Text;
	}
}
