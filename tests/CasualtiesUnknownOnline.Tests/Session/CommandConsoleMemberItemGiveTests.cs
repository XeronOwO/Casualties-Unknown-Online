using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Handlers;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The <c>/give</c> MEMBER half: the member's verb is a request, and both of the
/// host's cheat rulings are exercised where each really decides.
///
/// <para>
/// The front end (the member's own console) refuses on an ANNOUNCED closed switch or
/// an announced roster that does not name it — no round trip, nothing created. The
/// back end (the host) judges the same two rulings against its own state for every
/// request that does arrive, and its answer is the one sentence the member's console
/// prints. The swallowed-announcement case is the one that proves the second half is
/// real: the member's check is skipped (nothing was announced), the request arrives,
/// and the host's refusal is what the operator reads.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleMemberItemGiveTests
{
	[Fact]
	public void Member_GivesToTheHost_LandsAndTheHostsAnswerPrints()
	{
		var (host, guest, creation) = CreateMemberGiveSession(switchOpen: true, grant: true);

		var answer = Run(guest, "/give @a[name=1001] bandage");

		var item = Assert.Single(creation.Created);
		Assert.Equal($"Gave bandage (#{item.InstanceId}) to {HostId} in slot 0.", answer);
		var landed = Assert.Single(Characters(host).GetHostCharacterData()!.Items);
		Assert.Equal(item.InstanceId, landed.InstanceId);
		Assert.Equal(ItemLocationKind.Carried, Kernel(host).FindItem(item.InstanceId)!.Value.Location.Kind);
	}

	[Fact]
	public void Member_GivesToItself_LandsOnItsOwnSnapshot()
	{
		var (host, guest, creation) = CreateMemberGiveSession(switchOpen: true, grant: true);

		var answer = Run(guest, "/give @s bandage");

		var item = Assert.Single(creation.Created);
		Assert.Contains($"to {GuestId} in slot 0", answer);
		var landed = Assert.Single(Characters(host).GetSavedCharacter(GuestId)!.Items);
		Assert.Equal(item.InstanceId, landed.InstanceId);
	}

	/// <summary>The properties are applied by the HOST, to ITS capture: the values the member typed are judged where the item is created and then delivered already set.</summary>
	[Fact]
	public void Member_GivesWithState_IsAppliedByTheHost()
	{
		var (host, guest, creation) = CreateMemberGiveSession(switchOpen: true, grant: true);

		var answer = Run(guest, "/give @a[name=1001] bandage{Condition:0.5f}");

		var item = Assert.Single(creation.Created);
		Assert.Contains("with state {Condition:0.5f,", answer);
		Assert.Equal(0.5f, Assert.Single(Characters(host).GetHostCharacterData()!.Items).Condition);
		Assert.Equal(0.5f, Kernel(host).FindItem(item.InstanceId)!.Value.Data.Condition);
	}

	[Fact]
	public void Member_Gives_WhenTheAnnouncedSwitchIsClosed_IsRefusedLocally()
	{
		var (host, guest, creation) = CreateMemberGiveSession(switchOpen: false, grant: true);

		var answer = Run(guest, "/give @a[name=1001] bandage");

		Assert.Contains("Refused locally", answer);
		Assert.Contains("AllowMemberCheatVerbs", answer);
		Assert.Empty(creation.Created);
		Assert.Empty(Characters(host).GetHostCharacterData()?.Items ?? []);
	}

	[Fact]
	public void Member_Gives_WhenTheSwitchIsOpenButTheMemberIsNotGranted_IsRefusedByTheRoster()
	{
		var (host, guest, creation) = CreateMemberGiveSession(switchOpen: true, grant: false);

		var answer = Run(guest, "/give @a[name=1001] bandage");

		Assert.Contains("Refused locally", answer);
		Assert.Contains("cheat roster", answer);
		Assert.DoesNotContain("cheat switch", answer);
		Assert.Empty(creation.Created);
		Assert.Empty(Characters(host).GetHostCharacterData()?.Items ?? []);
	}

	/// <summary>
	/// The member whose roster announcement never arrived: its own check is SKIPPED
	/// (there is nothing to intercept on), so the request reaches the host — and the
	/// host's own roster refusal is the answer, printed where the verb was typed. This
	/// is the back end the front end can only ever predict.
	/// </summary>
	[Fact]
	public void Member_Gives_WhenNoRosterWasAnnounced_IsSettledByTheHostsOwnRefusal()
	{
		var (host, guest, creation) = CreateMemberGiveSession(switchOpen: true, grant: false, announceRoster: false);

		var answer = Run(guest, "/give @a[name=1001] bandage");

		Assert.Contains("the host's cheat roster does not name you", answer);
		Assert.Empty(creation.Created);
		Assert.Empty(Characters(host).GetHostCharacterData()?.Items ?? []);
	}

	/// <summary>
	/// A malformed property line is refused on the client that typed it: the console
	/// parses the words before it sends, so no request reaches the host and nothing is
	/// created — the same refusal the host would have produced, without the round trip.
	/// </summary>
	[Fact]
	public void Member_GivesWithMalformedSnbt_IsRefusedLocallyWithoutARequest()
	{
		var (host, guest, creation) = CreateMemberGiveSession(switchOpen: true, grant: true);
		var requests = new List<NetMsg>();
		host.Transport.MessageReceived += (_, frame) => requests.Add((NetMsg)frame[0]);

		var answer = Run(guest, "/give @a[name=1001] bandage{Condition:}");

		Assert.Contains("The state is not SNBT:", answer);
		Assert.DoesNotContain(NetMsg.PlayerInventoryGiveRequest, requests);
		Assert.Empty(creation.Created);
	}

	[Fact]
	public void Member_GivesAnUnknownDefinition_IsAnsweredByTheHost()
	{
		var (_, guest, creation) = CreateMemberGiveSession(switchOpen: true, grant: true);

		var answer = Run(guest, "/give @a[name=1001] notanitem");

		Assert.Contains("No item definition 'notanitem' is known", answer);
		Assert.Empty(creation.Created);
	}

	/// <summary>
	/// The sender-anchored rule the answer handler gained in the fix cycle: the wire
	/// direction check proves only the LOCAL role, so a frame whose sender is not this
	/// session's host is a CLAIM and is dropped by name. A third peer sends a forged
	/// answer straight to the member; the member's console must not print it and the
	/// handler must say why.
	/// </summary>
	[Fact]
	public void AnAnswerFromANonHostSender_IsDropped()
	{
		const ulong ThirdId = 3001;
		var log = new RecordingLogger<PlayerInventoryGiveResultHandler>();
		var (host, guest, network) = TestNode.CreatePairWithNetwork(
			HostId, GuestId, LobbyId,
			guestRegistrations: services => services.Replace(
				ServiceDescriptor.Singleton<ILogger<PlayerInventoryGiveResultHandler>>(log)));
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

				third.Services.GetRequiredService<PacketSender>().Send(GuestId, NetMsg.PlayerInventoryGiveResult,
					new PlayerInventoryGiveResultMsg
					{
						DefinitionId = "bandage",
						ItemInstanceId = 1,
						SlotIndex = 0,
						Applied = true,
						Answer = "Gave bandage (#1) to 1001 in slot 0.",
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
	/// A member's console case needs the switch OPEN as the host's own configuration
	/// (announced to the member on the handshake), the roster granted only when the
	/// case is about the WHO half, and the host's entity table seeded so <c>@p</c>
	/// names the host from the member's own view. The creation port is the same fake
	/// on both nodes; only the host ever calls it.
	/// </summary>
	private static (TestNode Host, TestNode Guest, FakeItemGrantSource Creation) CreateMemberGiveSession(
		bool switchOpen, bool grant, bool announceRoster = true)
	{
		var creation = new FakeItemGrantSource();
		var rules = new MutableOptionsMonitor<HostRulesOptions>(
			new HostRulesOptions { AllowMemberCheatVerbs = switchOpen });
		var (host, guest) = CreateSession(
			services =>
			{
				services.Replace(ServiceDescriptor.Singleton<IItemGrantSource>(creation));
				services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(rules));
			},
			swallowHostToGuest: announceRoster ? null : NetMsg.CheatPermissions);

		if (grant)
		{
			GrantAndAnnounceCheatVerbs(host, guest);
		}

		Characters(host).SaveHostCharacterData(Snapshot(HostId, conscious: true));
		Characters(host).SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, 5f);
		// The member's own view of the host: its console resolves @p against its own
		// entity table, which learns the host from the join announcement.
		guest.Services.GetRequiredService<IEntitySyncControl>().ProcessPlayerJoin(new PlayerJoinMsg
		{
			HostSteamId = HostId,
			GuestSteamId = GuestId,
			HostPosition = new NetVector2Msg(0f, 0f),
			GuestPosition = new NetVector2Msg(5f, 0f),
		});
		return (host, guest, creation);
	}

	private static ICharacterDataControl Characters(TestNode node) =>
		node.Services.GetRequiredService<ICharacterDataControl>();

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
