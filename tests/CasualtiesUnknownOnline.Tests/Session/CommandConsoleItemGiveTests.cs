using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The <c>/give</c> host half through the real console, the real kernel and the
/// production composition root: the item is created from the definition, the
/// properties are applied to it BEFORE delivery, the destination is chosen from the
/// recipient's own snapshot, and the answer names what landed where. The permission
/// cases and the member's request/answer path are
/// <see cref="CommandConsoleMemberItemGiveTests"/>'s, because those are a different
/// half of the same verb.
///
/// <para>
/// The creation port is the suite's own fixture (<see cref="FakeItemGrantSource"/>):
/// the game's item table, prefab capture and id allocator are exactly what a test
/// host has none of, and the real implementation's answers (a definition in the
/// table, a prefab that loads, an id from the one allocator) are the adapter's own
/// to prove at runtime.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleItemGiveTests
{
	private const ulong SecondGuest = 2002;

	[Fact]
	public void Host_GivesToAGuest_WritesTheSnapshotTheLedgerAndTheKernelRow()
	{
		var (host, guest, creation) = CreateGiveSession();
		SeedGuestSnapshot(host);

		var answer = Run(host, "/give @p[distance=1..] bandage");

		var item = Assert.Single(creation.Created);
		Assert.Equal($"Gave bandage (#{item.InstanceId}) to {GuestId} in slot 0.", answer);
		var landed = Assert.Single(Characters(host).GetSavedCharacter(GuestId)!.Items);
		Assert.Equal(item.InstanceId, landed.InstanceId);
		Assert.Equal("bandage", landed.ItemId);
		Assert.Equal(0, landed.SlotIndex);
		Assert.Equal(ItemLocationKind.Carried, Kernel(host).FindItem(item.InstanceId)!.Value.Location.Kind);
		Assert.Equal(GuestId, Kernel(host).FindItem(item.InstanceId)!.Value.Location.Owner.Value);
		Assert.Contains(Items(host).GetTransferredItems(GuestId), world => world.Item.InstanceId == item.InstanceId);
		Assert.NotNull(Kernel(guest).FindItem(item.InstanceId)); // the committed batch carried the row to the member
	}

	[Fact]
	public void Host_GivesToItself_WritesItsOwnSnapshot()
	{
		var (host, _, creation) = CreateGiveSession();
		Characters(host).SaveHostCharacterData(Snapshot(HostId, conscious: true));

		var answer = Run(host, "/give bandage");

		var item = Assert.Single(creation.Created);
		Assert.Equal($"Gave bandage (#{item.InstanceId}) to {HostId} in slot 0.", answer);
		var landed = Assert.Single(Characters(host).GetHostCharacterData()!.Items);
		Assert.Equal(item.InstanceId, landed.InstanceId);
		Assert.Equal(ItemLocationKind.Carried, Kernel(host).FindItem(item.InstanceId)!.Value.Location.Kind);
	}

	[Fact]
	public void Host_GivesInTheNamedSlot()
	{
		var (host, _, creation) = CreateGiveSession();
		SeedGuestSnapshot(host);

		var answer = Run(host, "/give @p[distance=1..] bandage slot 2");

		Assert.Equal($"Gave bandage (#{creation.Created[0].InstanceId}) to {GuestId} in slot 2.", answer);
		Assert.Equal(2, Assert.Single(Characters(host).GetSavedCharacter(GuestId)!.Items).SlotIndex);
	}

	/// <summary>The named slot is a preference, not a promise: an occupied one falls back to the first free slot, and the answer names where the item really went.</summary>
	[Fact]
	public void Host_GivesIntoAnOccupiedSlot_FallsBackAndSaysWhereItWent()
	{
		var (host, _, creation) = CreateGiveSession();
		Characters(host).SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(4100, "backpack", slot: 2)));

		var answer = Run(host, "/give @p[distance=1..] bandage slot 2");

		Assert.Equal($"Gave bandage (#{creation.Created[0].InstanceId}) to {GuestId} in slot 0.", answer);
		Assert.Contains(Characters(host).GetSavedCharacter(GuestId)!.Items, item => item.InstanceId == 4100 && item.SlotIndex == 2);
		Assert.Contains(Characters(host).GetSavedCharacter(GuestId)!.Items, item => item.InstanceId == creation.Created[0].InstanceId && item.SlotIndex == 0);
	}

	/// <summary>
	/// The ladder's last rung: a body with no free slot and no container that can take the item. The item is
	/// not carried — no snapshot row, no transfer-table entry, no kernel carried row — and the published
	/// transfer carries the DROP destination, which is what makes the receiving client drop it where it
	/// stands. The answer says so instead of claiming a slot.
	/// </summary>
	[Fact]
	public void Host_GivesToATargetWithNoRoom_DropsAtItsPositionAndSaysSo()
	{
		var (host, guest, creation) = CreateGiveSession();
		Characters(host).SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true,
			Item(4100, "backpack", slot: 0), Item(4101, "bandage", slot: 1), Item(4102, "knife", slot: 2)));
		PlayerInventoryTransferMsg? transfer = null;
		guest.Services.GetRequiredService<IPlayerInteractionControl>().TransferReceived += message => transfer = message;

		var answer = Run(host, "/give @p[distance=1..] bandage");

		var item = Assert.Single(creation.Created);
		Assert.Equal($"Gave bandage (#{item.InstanceId}) to {GuestId}, dropped at its position (the ladder's last rung).", answer);
		Assert.Equal(3, Characters(host).GetSavedCharacter(GuestId)!.Items.Count);
		Assert.Empty(Items(host).GetTransferredItems(GuestId));
		// The host writes no row here: a drop is not a carried item, and the world row is the RECEIVING client's
		// own creation report (the adapter's half — the acceptance batch reads the row on both peers).
		Assert.Null(Kernel(host).FindItem(item.InstanceId));
		Assert.NotNull(transfer);
		Assert.Equal(PlayerInventoryDestination.Drop, transfer!.Destination);
		Assert.Equal(0UL, transfer.TargetParentItemId);
		Assert.Equal(item.InstanceId, transfer.Item!.InstanceId);
	}

	/// <summary>
	/// Rung 2: a full body whose carried container can take the item. The item lands INSIDE that container
	/// (the snapshot's parent-slot convention), the transfer names the container, and the answer names it —
	/// the ladder's order is the Runtime's, the room is the fixture's.
	/// </summary>
	[Fact]
	public void Host_GivesToAFullBodyWithACarriedContainer_PlacesItInsideAndSaysSo()
	{
		var containers = new FakeItemContainerFit { Fits = 4100 };
		var (host, guest, creation) = CreateGiveSession(containers: containers);
		var backpack = Item(4100, "backpack", slot: 0);
		Characters(host).SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true,
			backpack, Item(4101, "bandage", slot: 1), Item(4102, "knife", slot: 2)));
		PlayerInventoryTransferMsg? transfer = null;
		guest.Services.GetRequiredService<IPlayerInteractionControl>().TransferReceived += message => transfer = message;

		var answer = Run(host, "/give @p[distance=1..] bandage");

		var item = Assert.Single(creation.Created);
		Assert.Equal($"Gave bandage (#{item.InstanceId}) to {GuestId} inside the carried container backpack (#4100).", answer);
		var saved = Characters(host).GetSavedCharacter(GuestId)!;
		var parent = saved.Items.Single(candidate => candidate.InstanceId == 4100);
		var child = Assert.Single(parent.Contents);
		Assert.Equal(item.InstanceId, child.InstanceId);
		Assert.Equal(0, child.SlotIndex); // a container child carries its parent's slot
		Assert.DoesNotContain(saved.Items, candidate => candidate.InstanceId == item.InstanceId);
		var ledger = Assert.Single(Items(host).GetTransferredItems(GuestId));
		Assert.Equal(-1, ledger.Item.SlotIndex); // "carried, no slot of its own" — the container owns the slot
		Assert.Equal(GuestId, Kernel(host).FindItem(item.InstanceId)!.Value.Location.Owner.Value);
		Assert.NotNull(transfer);
		Assert.Equal(PlayerInventoryDestination.Container, transfer!.Destination);
		Assert.Equal(4100UL, transfer.TargetParentItemId);
	}

	/// <summary>
	/// Rungs 2 then 3: the carried candidates are offered before the worn ones, and the worn container is
	/// what answers when no carried one can take the item — the ladder's own order, read from the fixture.
	/// </summary>
	[Fact]
	public void Host_GivesToAWornContainer_CarriedCandidatesAreOfferedFirst()
	{
		var containers = new FakeItemContainerFit { Fits = 4200 };
		var (host, _, creation) = CreateGiveSession(containers: containers);
		Characters(host).SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true,
			Item(4100, "backpack", slot: 0), Item(4101, "bandage", slot: 1), Item(4102, "knife", slot: 2),
			Item(4200, "pants", slot: -3)));

		var answer = Run(host, "/give @p[distance=1..] bandage");

		var item = Assert.Single(creation.Created);
		Assert.Contains($"inside the worn container pants (#4200)", answer);
		Assert.Equal(new ulong[] { 4100, 4101, 4102, 4200 }, containers.Offered); // the whole carried set, then the worn one
		var saved = Characters(host).GetSavedCharacter(GuestId)!;
		Assert.Equal(item.InstanceId, Assert.Single(saved.Items.Single(candidate => candidate.InstanceId == 4200).Contents).InstanceId);
	}

	[Fact]
	public void Host_GivesWithAProperty_AppliesItBeforeDelivery()
	{
		var (host, _, creation) = CreateGiveSession();
		SeedGuestSnapshot(host);

		var answer = Run(host, "/give @p[distance=1..] bandage{Condition:0.25f}");

		var item = Assert.Single(creation.Created);
		Assert.Contains("with state {Condition:0.25f,", answer);
		Assert.Equal(0.25f, Assert.Single(Characters(host).GetSavedCharacter(GuestId)!.Items).Condition);
		Assert.Equal(0.25f, Kernel(host).FindItem(item.InstanceId)!.Value.Data.Condition);
	}

	[Fact]
	public void Host_GivesWithALiquidAndAComponent_AppliesBoth()
	{
		var (host, _, creation) = CreateGiveSession();
		SeedGuestSnapshot(host);

		var liquid = Run(host, "/give @p[distance=1..] waterbottle{Liquids:[{LiquidId:'water',Amount:500f}]}");
		var component = Run(host, "/give @p[distance=1..] largebattery{Components:{BatteryItem:{charge:0.9f}}}");

		Assert.Contains("Liquids:[{LiquidId:", liquid);
		Assert.Contains("Amount:500f}]", liquid);
		Assert.Contains("Components:{BatteryItem:{charge:0.9f,", component);
		var landed = Characters(host).GetSavedCharacter(GuestId)!.Items;
		Assert.Equal(500f, landed.Single(item => item.ItemId == "waterbottle").Liquids.Single().Amount);
		var battery = landed.Single(item => item.ItemId == "largebattery");
		Assert.Equal(0.9f, battery.Components.Single().Fields.Single(field => field.Name == "charge").FloatValue);
		Assert.Equal(2, creation.Created.Count);
	}

	/// <summary>
	/// A property the definition does not carry refuses the whole give: the created
	/// fact is discarded and NOTHING is delivered — the same one-commit rule the
	/// write verbs' merge has, applied before the item ever starts existing for the
	/// recipient.
	/// </summary>
	[Fact]
	public void Host_GivesWithAPropertyTheDefinitionDoesNotCarry_IsRefusedAndDeliversNothing()
	{
		var (host, _, creation) = CreateGiveSession();
		SeedGuestSnapshot(host);

		var answer = Run(host, "/give @p[distance=1..] bandage{Liquids:[{LiquidId:'water',Amount:500f}]}");

		Assert.Contains("has no liquid container", answer);
		Assert.Empty(creation.Created); // refused by the bind, before an id exists
		Assert.Empty(Characters(host).GetSavedCharacter(GuestId)!.Items);
		Assert.Empty(Items(host).GetTransferredItems(GuestId));
	}

	[Fact]
	public void Host_GivesADefinitionTheBuildDoesNotKnow_IsRefusedByName()
	{
		var (host, _, creation) = CreateGiveSession();
		SeedGuestSnapshot(host);

		var answer = Run(host, "/give @p[distance=1..] notanitem");

		Assert.Contains("No item definition 'notanitem' is known", answer);
		Assert.Empty(creation.Created);
		Assert.Empty(Characters(host).GetSavedCharacter(GuestId)!.Items);
	}

	/// <summary>
	/// The delivery's own guard: a target the host does not know is refused by name
	/// before anything is placed on a snapshot. The console cannot address such a
	/// target — its selector expands against the entity table, so a member that left
	/// the world is no longer in it — which makes this the race the guard exists for
	/// rather than a shape an operator can type; it is driven through the surface the
	/// console calls.
	/// </summary>
	[Fact]
	public void Host_GivesToATargetOutsideTheWorld_IsRefusedByName()
	{
		var (host, _, _) = CreateGiveSession();
		SeedGuestSnapshot(host);

		var answer = host.Services.GetRequiredService<IPlayerInteractionControl>()
			.GiveItem(9999, "bandage", -1, "");

		Assert.Contains("9999 is not in-world", answer);
		Assert.Empty(Characters(host).GetSavedCharacter(GuestId)!.Items);
		Assert.Empty(Items(host).GetTransferredItems(GuestId));
	}

	/// <summary>A selector may name several bodies, but one give names ONE destination; the verdict is taken on the resolved set and answered by name.</summary>
	[Fact]
	public void Host_GivesWhenTheSelectorNamesSeveralBodies_IsRefusedByName()
	{
		var (host, _, creation) = CreateGiveSession();
		SeedHostEntities(host, GuestId, 5f);
		AddSecondRemote(host);

		// @a is EVERY player now — the executor included (ticket selectors-exclude-the-executor) — so this names
		// three bodies rather than two, which is exactly what the refusal must count.
		var answer = Run(host, "/give @a bandage");

		Assert.Contains("matched 3 bodies", answer);
		Assert.Contains("[limit=1]", answer);
		Assert.Empty(creation.Created);
	}

	[Theory]
	[InlineData("/give", "Usage:")]
	[InlineData("/give @p[distance=1..]", "Usage:")]
	[InlineData("/give @p[distance=1..] bandage slot x", "slot")]
	[InlineData("/give id 5 bandage", "names an item, not a definition")]
	public void MalformedGive_IsRefusedWithItsOwnSentence(string line, string expected)
	{
		var (host, _, creation) = CreateGiveSession();
		SeedGuestSnapshot(host);

		var answer = Run(host, line);

		Assert.Contains(expected, answer);
		Assert.Empty(creation.Created);
	}

	/// <summary>
	/// The property words are parsed on the typing client BEFORE the gate and the host:
	/// a malformed line never reaches the creation port, so nothing is created and
	/// nothing is sent — the refusal is the write parser's own sentence, quoting the
	/// give grammar.
	/// </summary>
	[Fact]
	public void Host_GivesWithAStateTheDefinitionDoesNotCarry_IsRefusedByTheCreationPort()
	{
		var (host, guest, creation) = CreateGiveSession();
		SeedGuestSnapshot(host);

		var answer = Run(host, "/give @p[distance=1..] bandage{Quality:5}");

		Assert.Contains("not part of an item's state", answer);
		Assert.Contains("bandage", creation.Attempts); // it reached the port, which refused it
		Assert.Empty(creation.Created);
		Assert.Empty(Characters(host).GetSavedCharacter(GuestId)!.Items);
		_ = guest;
	}

	/// <summary>
	/// The host's own standing is the online switch: neither the cheat switch nor a
	/// roster entry gates the host's own console, exactly as the landed write verb's
	/// tests pin. The member's half is the other class's.
	/// </summary>
	[Fact]
	public void Host_GivesWhileTheMemberSwitchIsClosed()
	{
		var (host, _, creation) = CreateGiveSession(memberCheatSwitch: false);
		SeedGuestSnapshot(host);

		var answer = Run(host, "/give @p[distance=1..] bandage");

		Assert.StartsWith("Gave bandage", answer);
		Assert.Single(creation.Created);
	}

	/// <summary>
	/// Every console case runs against a scene of its own: the fake creation port and
	/// the host rules the member cases vary. The roster is granted and announced only
	/// when the case is about a member (`grant: true`), because the host's own cases
	/// must not depend on it.
	/// </summary>
	private static (TestNode Host, TestNode Guest, FakeItemGrantSource Creation) CreateGiveSession(bool memberCheatSwitch = false, bool grant = false, bool announceRoster = true, FakeItemContainerFit? containers = null)
	{
		var creation = new FakeItemGrantSource();
		var containerFit = containers ?? new FakeItemContainerFit();
		var rules = new MutableOptionsMonitor<HostRulesOptions>(
			new HostRulesOptions { AllowMemberCheatVerbs = memberCheatSwitch });
		var (host, guest) = CreateSession(
			services =>
			{
				services.Replace(ServiceDescriptor.Singleton<IItemGrantSource>(creation));
				services.Replace(ServiceDescriptor.Singleton<IItemContainerFit>(containerFit));
				services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(rules));
			},
			swallowHostToGuest: announceRoster ? null : NetMsg.CheatPermissions);

		if (grant)
		{
			GrantAndAnnounceCheatVerbs(host, guest);
		}

		SeedHostEntities(host, GuestId, 5f);
		return (host, guest, creation);
	}

	private static void SeedGuestSnapshot(TestNode host) =>
		Characters(host).SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

	private static void AddSecondRemote(TestNode host)
	{
		var entities = host.Services.GetRequiredService<IEntitySyncControl>();
		entities.ProcessPlayerJoin(new PlayerJoinMsg
		{
			HostSteamId = HostId,
			GuestSteamId = SecondGuest,
			HostPosition = new NetVector2Msg(0f, 0f),
			GuestPosition = new NetVector2Msg(7f, 0f),
		});
	}

	private static CharacterItemMsg Item(ulong instanceId, string definition, int slot) => new()
	{
		InstanceId = instanceId,
		ItemId = definition,
		SlotIndex = slot,
		Condition = 1f,
	};

	private static ICharacterDataControl Characters(TestNode node) =>
		node.Services.GetRequiredService<ICharacterDataControl>();

	private static IItemControl Items(TestNode node) =>
		node.Services.GetRequiredService<IItemControl>();

	private static ItemKernelAuthority Kernel(TestNode node) =>
		node.Services.GetRequiredService<ItemKernelAuthority>();

	/// <summary>Runs one console line and answers with the line it printed.</summary>
	private static string Run(TestNode node, string line)
	{
		var console = node.Services.GetRequiredService<ICommandControl>();
		Assert.True(console.TryExecute(line), $"'{line}' was not accepted by the console");
		return console.Lines[console.Lines.Count - 1].Text;
	}
}
