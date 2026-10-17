using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Handlers;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The <c>/summon</c> MEMBER half: the member's verb is a request, and both of the host's cheat rulings are
/// exercised where each really decides.
///
/// <para>
/// The front end (the member's own console) refuses on an ANNOUNCED closed switch or an announced roster that does
/// not name it — no round trip, nothing created, no position even resolved. The back end (the host) judges the same
/// two rulings against its own state for every request that does arrive, decides the layer stamp, enforces the
/// type word, creates, and answers: that answer is the one sentence the member's console prints. The member
/// resolves the POSITION on its own scene (its pointer, its body), which is why the member's fixture is the one
/// that answers the anchor.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleMemberWorldSummonTests
{
	[Fact]
	public void Member_Summons_RequestsTheHostAndPrintsItsAnswer()
	{
		var (host, guest, summons) = CreateMemberSummonSession(switchOpen: true, grant: true);

		var answer = Run(guest, "/summon beartrap player");

		Assert.Equal("Summoned beartrap at (1.0, 2.0).", answer);
		// The member's own scene answered the position word; the HOST's seam created the object.
		Assert.Equal(WorldSummonAnchor.Player, summons.Member.LastAnchor);
		Assert.Empty(summons.Member.Attempts);
		Assert.Equal(("beartrap", WorldSummonKind.Entity, ""), Assert.Single(summons.Host.Attempts));
		Assert.Equal("beartrap", Assert.Single(Registry(host).Entries).Id);
		Assert.Equal(1f, Registry(host).Entries[0].Position.X);
	}

	[Fact]
	public void Member_SummonsAnItemEntity_IsCreatedByTheHost()
	{
		var (host, guest, summons) = CreateMemberSummonSession(switchOpen: true, grant: true);

		var answer = Run(guest, "/summon item bandage");

		var item = Assert.Single(summons.Host.CreatedItems);
		Assert.Equal($"Summoned bandage (#{item.InstanceId}) at (10.0, 20.0).", answer);
		Assert.Equal(ItemLocationKind.World, Kernel(host).FindItem(item.InstanceId)!.Value.Location.Kind);
	}

	[Fact]
	public void Member_SummonsWithState_IsBoundByTheHost()
	{
		var (host, guest, summons) = CreateMemberSummonSession(switchOpen: true, grant: true);

		var answer = Run(guest, "/summon item bandage player {Condition:0.5f}");

		var item = Assert.Single(summons.Host.CreatedItems);
		Assert.Contains("with state {Condition:0.5f,", answer);
		Assert.Equal(0.5f, Kernel(host).FindItem(item.InstanceId)!.Value.Data.Condition);
	}

	[Fact]
	public void Member_Summons_WhenTheAnnouncedSwitchIsClosed_IsRefusedLocally()
	{
		var (host, guest, summons) = CreateMemberSummonSession(switchOpen: false, grant: true);
		var requests = new List<NetMsg>();
		host.Transport.MessageReceived += (_, frame) => requests.Add((NetMsg)frame[0]);

		var answer = Run(guest, "/summon beartrap player");

		Assert.Contains("Refused locally", answer);
		Assert.Contains("AllowMemberCheatVerbs", answer);
		Assert.DoesNotContain(NetMsg.WorldSummonRequest, requests);
		Assert.Null(summons.Member.LastAnchor); // the gate ran before any scene work
		Assert.Equal(0, Registry(host).Count);
	}

	[Fact]
	public void Member_Summons_WhenTheSwitchIsOpenButTheMemberIsNotGranted_IsRefusedByTheRoster()
	{
		var (host, guest, summons) = CreateMemberSummonSession(switchOpen: true, grant: false);
		var requests = new List<NetMsg>();
		host.Transport.MessageReceived += (_, frame) => requests.Add((NetMsg)frame[0]);

		var answer = Run(guest, "/summon beartrap player");

		Assert.Contains("Refused locally", answer);
		Assert.Contains("cheat roster", answer);
		Assert.DoesNotContain("cheat switch", answer);
		Assert.DoesNotContain(NetMsg.WorldSummonRequest, requests);
		Assert.Empty(summons.Member.Attempts);
		Assert.Equal(0, Registry(host).Count);
	}

	/// <summary>
	/// The member whose roster announcement never arrived: its own check is SKIPPED (there is nothing to intercept
	/// on), so the request reaches the host — and the host's own roster refusal is the answer, printed where the
	/// verb was typed. This is the back end the front end can only ever predict.
	/// </summary>
	[Fact]
	public void Member_Summons_WhenNoRosterWasAnnounced_IsSettledByTheHostsOwnRefusal()
	{
		var (host, guest, _) = CreateMemberSummonSession(switchOpen: true, grant: false, announceRoster: false);

		var answer = Run(guest, "/summon beartrap player");

		Assert.Contains("the host's cheat roster does not name you", answer);
		Assert.Equal(0, Registry(host).Count);
	}

	[Fact]
	public void Member_SummonsWithMalformedSnbt_IsRefusedLocallyWithoutARequest()
	{
		var (host, guest, summons) = CreateMemberSummonSession(switchOpen: true, grant: true);
		var requests = new List<NetMsg>();
		host.Transport.MessageReceived += (_, frame) => requests.Add((NetMsg)frame[0]);

		var answer = Run(guest, "/summon item bandage player {Condition:}");

		Assert.Contains("The state is not SNBT:", answer);
		Assert.DoesNotContain(NetMsg.WorldSummonRequest, requests);
		Assert.Empty(summons.Host.Attempts);
	}

	/// <summary>The member's own scene answers the position word, so a member that cannot see its body or pointer learns it there — before a round trip and before anything is created.</summary>
	[Fact]
	public void Member_SummonWhenItsOwnSceneCannotAnswerThePosition_IsRefusedLocallyWithoutARequest()
	{
		var (host, guest, summons) = CreateMemberSummonSession(switchOpen: true, grant: true);
		summons.Member.AnchorRefusal = "this client is not in a world, so there is no layer to create anything in.";
		var requests = new List<NetMsg>();
		host.Transport.MessageReceived += (_, frame) => requests.Add((NetMsg)frame[0]);

		var answer = Run(guest, "/summon item bandage player");

		Assert.Contains("Refused: this client is not in a world", answer);
		Assert.DoesNotContain(NetMsg.WorldSummonRequest, requests);
		Assert.Empty(summons.Host.Attempts);
	}

	/// <summary>
	/// The position is layer-relative, so a request that outlived a layer boundary must be refused instead of
	/// materializing at the old layer's coordinates in the new one. The stamp is the kernel run baseline both
	/// sides read, and the host answers the refusal so it lands where the verb was typed.
	/// </summary>
	[Fact]
	public void ARequestFromAnotherLayer_IsRefusedByTheHostAndCreatesNothing()
	{
		var (host, guest, summons) = CreateMemberSummonSession(switchOpen: true, grant: true);
		var hostReceived = new List<NetMsg>();
		host.Transport.MessageReceived += (_, frame) => hostReceived.Add((NetMsg)frame[0]);
		// The console is resolved BEFORE the request goes out: its subscription to the answer is what prints the
		// line, and a console built after the answer arrived would legitimately miss it (production builds it at
		// startup, so this is the test's own ordering).
		var console = guest.Services.GetRequiredService<ICommandControl>();
		WorldGenerationReports.CommitRun(host, layerIndex: 0);
		var current = WorldGenerationReports.StampOf(host);

		guest.Services.GetRequiredService<PacketSender>().Send(host.SteamId, NetMsg.WorldSummonRequest, new WorldSummonRequestMsg
		{
			PrefabId = "beartrap",
			Kind = WorldSummonKind.Entity,
			X = 4f,
			Y = 5f,
			Generation = new WorldGenerationMsg { RunEpoch = current.RunEpoch, LayerIndex = 3 },
		});
		host.Update();
		guest.Update();

		Assert.Empty(summons.Host.Attempts);
		Assert.Equal(0, Registry(host).Count);
		Assert.True(
			console.Lines.Any(line => line.Text.Contains("positions are layer-relative", StringComparison.Ordinal)),
			"guest lines: " + string.Join(" | ", console.Lines.Select(line => line.Text))
				+ " ## host received: " + string.Join(",", hostReceived));
	}

	/// <summary>
	/// The sender-anchored rule the answer handlers carry: the wire direction check proves only the LOCAL role, so
	/// a frame whose sender is not this session's host is a CLAIM and is dropped by name. A third peer sends a
	/// forged answer straight to the member; the member's console must not print it and the handler must say why.
	/// </summary>
	[Fact]
	public void AnAnswerFromANonHostSender_IsDropped()
	{
		const ulong ThirdId = 3001;
		var log = new RecordingLogger<WorldSummonResultHandler>();
		var (host, guest, network) = TestNode.CreatePairWithNetwork(
			HostId, GuestId, LobbyId,
			guestRegistrations: services => services.Replace(
				ServiceDescriptor.Singleton<ILogger<WorldSummonResultHandler>>(log)));
		using (host)
		using (guest)
		{
			var console = guest.Services.GetRequiredService<ICommandControl>();
			var linesBefore = console.Lines.Count;
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

				third.Services.GetRequiredService<PacketSender>().Send(GuestId, NetMsg.WorldSummonResult,
					new WorldSummonResultMsg
					{
						PrefabId = "beartrap",
						Applied = true,
						Answer = "Summoned beartrap at (1.0, 2.0).",
					});
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

	/// <summary>
	/// A member's console case needs the switch OPEN as the host's own configuration (announced to the member on
	/// the handshake), the roster granted only when the case is about the WHO half, and one scene seam per node:
	/// the member resolves the position, the host creates.
	/// </summary>
	private static (TestNode Host, TestNode Guest, (FakeWorldSummon Host, FakeWorldSummon Member) Summons) CreateMemberSummonSession(
		bool switchOpen, bool grant, bool announceRoster = true)
	{
		var rules = new MutableOptionsMonitor<HostRulesOptions>(
			new HostRulesOptions { AllowMemberCheatVerbs = switchOpen });
		var (host, guest) = CreateSession(
			services =>
			{
				services.Replace(ServiceDescriptor.Singleton<IWorldSummon>(p => new FakeWorldSummon(
					p.GetRequiredService<IItemControl>(),
					p.GetRequiredService<IWorldControl>(),
					p.GetRequiredService<ISessionControl>())));
				services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(rules));
			},
			swallowHostToGuest: announceRoster ? null : NetMsg.CheatPermissions);

		if (grant)
		{
			GrantAndAnnounceCheatVerbs(host, guest);
		}

		return (host, guest, (
			(FakeWorldSummon)host.Services.GetRequiredService<IWorldSummon>(),
			(FakeWorldSummon)guest.Services.GetRequiredService<IWorldSummon>()));
	}

	private static RuntimeEntityRegistry Registry(TestNode node) =>
		node.Services.GetRequiredService<RuntimeEntityRegistry>();

	private static ItemKernelAuthority Kernel(TestNode node) =>
		node.Services.GetRequiredService<ItemKernelAuthority>();

	/// <summary>Runs one console line and answers with the line it printed — the host's answer line, for a member's verb.</summary>
	private static string Run(TestNode node, string line)
	{
		var console = node.Services.GetRequiredService<ICommandControl>();
		Assert.True(console.TryExecute(line), $"'{line}' was not accepted by the console");
		return console.Lines[console.Lines.Count - 1].Text;
	}
}
