using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The <c>/data get</c> verb through the real console and the real kernel: what a
/// target resolves to, what the answer names, and every refusal. The wording of
/// the rows themselves is pinned by <see cref="ItemDataRendererTests"/>, and the
/// grammar by <see cref="ItemDataQueryParserTests"/>; this class is the wiring
/// those two cannot prove.
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleItemDataTests
{
	private const ulong Backpack = 4101;

	private const ulong Bandage = 4102;

	private const ulong GuestsBandage = 4201;

	private const ulong SecondGuest = 2002;

	[Fact]
	public void Get_WithoutATarget_ListsTheCallersOwnItemsInSlotOrder()
	{
		var (host, _) = CreateSession();
		SeedCarried(host, HostId, Bandage, "bandage", slot: 0);
		SeedCarried(host, HostId, Backpack, "backpack", slot: 1);

		var answer = Run(host, "/data get");

		Assert.Contains("Items recorded for 1001 (2):", answer);
		Assert.Contains("#4101 backpack [slot 1]", answer);
		Assert.True(
			answer.IndexOf("#4102", StringComparison.Ordinal) < answer.IndexOf("#4101", StringComparison.Ordinal),
			$"the listing follows slot order: {answer}");
	}

	[Fact]
	public void Get_ByInstanceId_PrintsEveryProperty()
	{
		var (host, _) = CreateSession();
		SeedCarried(host, HostId, Backpack, "backpack", slot: 1, condition: 0.8f, favourited: true, withState: true);

		var answer = Run(host, $"/data get id {Backpack}");

		Assert.Contains("#4101 backpack [carried by 1001, slot 1] revision", answer);
		Assert.Contains("Condition:0.8f", answer);
		Assert.Contains("Favourited:true", answer);
		Assert.Contains("Liquids:[", answer);
		Assert.Contains("Amount:250f", answer);
		Assert.Contains("Components:{BatteryItem:{charge:0.75f}}", answer);
	}

	[Fact]
	public void Get_BySlot_ReadsThatSlotOnly()
	{
		var (host, _) = CreateSession();
		SeedCarried(host, HostId, Bandage, "bandage", slot: 0);
		SeedCarried(host, HostId, Backpack, "backpack", slot: 1);

		var answer = Run(host, "/data get slot 1");

		Assert.Contains("#4101 backpack", answer);
		Assert.DoesNotContain("#4102", answer);
	}

	[Fact]
	public void Get_ForASelectedBody_ReadsThatBodysItems()
	{
		var (host, _) = CreateSession();
		SeedHostEntities(host, GuestId, 5f);
		SeedCarried(host, HostId, Backpack, "backpack", slot: 1);
		SeedCarried(host, GuestId, GuestsBandage, "bandage", slot: 2);

		var answer = Run(host, "/data get @a[distance=1..]");

		Assert.Contains("Items recorded for 2001 (1):", answer);
		Assert.Contains("#4201 bandage [slot 2]", answer);
		Assert.DoesNotContain("#4101", answer);
	}

	[Fact]
	public void Get_ForASelectedBodySlot_ReadsThatBodysSlot()
	{
		var (host, _) = CreateSession();
		SeedHostEntities(host, GuestId, 5f);
		SeedCarried(host, GuestId, GuestsBandage, "bandage", slot: 2);

		var answer = Run(host, $"/data get @a[distance=1..] slot 2");

		Assert.Contains("#4201 bandage [carried by 2001, slot 2]", answer);
	}

	[Fact]
	public void Get_NamesAContainerContentItRecords()
	{
		var (host, _) = CreateSession();
		SeedCarried(host, HostId, Backpack, "backpack", slot: 1);
		var parent = new CharacterItemMsg { InstanceId = Backpack, ItemId = "backpack", SlotIndex = 1 };
		parent.Contents.Add(new CharacterItemMsg { InstanceId = GuestsBandage, ItemId = "bandage", SlotIndex = 1 });
		Assert.True(Kernel(host).TrySyncContainerFacts(HostId, parent, out _, out _));

		var answer = Run(host, $"/data get id {Backpack}");

		Assert.Contains("contents (1): #4201 bandage", answer);
	}

	[Fact]
	public void Get_WithAnUnknownInstanceId_IsRefusedByName()
	{
		var (host, _) = CreateSession();

		Assert.Contains("No item #999999 is recorded in this session.", Run(host, "/data get id 999999"));
	}

	[Fact]
	public void Get_WithAnEmptySlot_IsRefusedByName()
	{
		var (host, _) = CreateSession();
		SeedCarried(host, HostId, Backpack, "backpack", slot: 1);

		Assert.Contains("No item is carried by 1001 in slot 7.", Run(host, "/data get slot 7"));
	}

	[Fact]
	public void Get_ForASelectorNoBaseMatches_IsRefusedByName()
	{
		var (host, _) = CreateSession();

		Assert.Contains("No players match selector '@x'.", Run(host, "/data get @x"));
	}

	[Fact]
	public void Get_ForABodyThatCarriesNothing_IsRefusedByName()
	{
		var (host, _) = CreateSession();
		SeedHostEntities(host, GuestId, 5f);

		Assert.Contains("No item is recorded for 2001.", Run(host, "/data get @a[distance=1..]"));
	}

	/// <summary>
	/// The caller's own body is the one empty answer that needs the reason: a
	/// carried item joins the record — and gets the id a target names — on the
	/// holding client's first report, which for the host is its first use, move or
	/// drop. Without that sentence an operator reads "you carry nothing" from an
	/// empty record.
	/// </summary>
	[Fact]
	public void Get_ForTheCallersOwnBodyWithNothingRecorded_SaysWhyItIsEmpty()
	{
		var (host, _) = CreateSession();

		var answer = Run(host, "/data get");

		Assert.Contains("No item is recorded for 1001 yet", answer);
		Assert.Contains("first report of it", answer);
	}

	[Theory]
	[InlineData("/data")]
	[InlineData("/data nonsense")]
	[InlineData("/data get 4101")]
	[InlineData("/data get slot -1")]
	public void Data_WithSomethingThatIsNotATarget_AnswersWithTheUsage(string line)
	{
		var (host, _) = CreateSession();

		var answer = Run(host, line);

		Assert.Contains("Usage:", answer);
		Assert.Contains("/data get", answer);
	}

	[Fact]
	public void Data_WithAWriteVerbThatDoesNotExist_SaysWhatTheVerbsAre()
	{
		var (host, _) = CreateSession();

		var answer = Run(host, "/data frobnicate id 4101 Condition set 0.5f");

		Assert.Contains("Unknown /data verb 'frobnicate'", answer);
		Assert.Contains("/data modify", answer);
	}

	// ===== Write =====

	/// <summary>
	/// The whole write, end to end through the real console, the real kernel and the
	/// adapter port's own seam: the value lands in the local scene AND the record
	/// follows it, because the report the command makes is the one a native use makes —
	/// and the report's ROUTE is observable, not inferred from the row changing (both
	/// routes update it): a carried item goes out as this client's carried fact.
	/// </summary>
	[Fact]
	public void Set_OnAnItemThisClientHolds_WritesTheSceneAndTheRecordFollows()
	{
		var (host, _, local) = CreateSceneSession();
		var item = Item(Backpack, "backpack", slot: 1, condition: 0.5f);
		Hold(host, local, item);
		var reports = RecordReports(host);

		var answer = Run(host, "/data modify @s slot 1 Condition set 0.25f");

		Assert.Contains("#4101 backpack: Condition 0.5f -> 0.25f", answer);
		Assert.Equal(0.25f, local.Held(Backpack)!.Condition);
		Assert.Equal(0.25f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
		Assert.Contains(reports.Carried, carried => carried.InstanceId == Backpack && carried.Condition == 0.25f);
		Assert.Empty(reports.Corrected);
	}

	/// <summary>
	/// The value is computed against the item's LOCAL capture, not against the record:
	/// the two can disagree for a frame (a native change the holder has not reported
	/// yet), and the write is the local truth the record then follows — which is why the
	/// answer's "before" is the local one.
	/// </summary>
	[Fact]
	public void Set_ComputesTheValueAgainstTheLocalCapture_NotTheRecord()
	{
		var (host, _, local) = CreateSceneSession();
		local.Hold(Item(Backpack, "backpack", slot: 1, condition: 0.4f));
		SeedCarried(host, HostId, Item(Backpack, "backpack", slot: 1, condition: 0.9f));

		var answer = Run(host, "/data modify id 4101 Condition set 0.2f");

		Assert.Contains("Condition 0.4f -> 0.2f", answer);
		Assert.Equal(0.2f, local.Held(Backpack)!.Condition);
		Assert.Equal(0.2f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
	}

	[Fact]
	public void Set_ByInstanceId_ReachesAnItemWhereverItSits()
	{
		var (host, _, local) = CreateSceneSession();
		Hold(host, local, Item(Backpack, "backpack", slot: 2));

		var answer = Run(host, $"/data modify id {Backpack} Favourited set true");

		Assert.Contains("Favourited false -> true", answer);
		Assert.True(Kernel(host).FindItem(Backpack)!.Value.Data.Favourited);
	}

	/// <summary>A component field is written as the kind the item's own capture carries, so the value is parsed against that item rather than against a global table.</summary>
	[Fact]
	public void Set_AComponentField_ParsesTheValueAgainstTheItemsOwnKind()
	{
		var (host, _, local) = CreateSceneSession();
		var item = Item(Backpack, "backpack", slot: 2, withState: true);
		Hold(host, local, item);

		var answer = Run(host, $"/data modify id {Backpack} Components.BatteryItem.charge set 0.9f");

		Assert.Contains("Components.BatteryItem.charge 0.75f -> 0.9f", answer);
		var field = Kernel(host).FindItem(Backpack)!.Value.Data.Components
			.Single(component => component.TypeName == "BatteryItem")
			.Fields.Single(candidate => candidate.Name == "charge");
		Assert.Equal(0.9f, field.FloatValue);
	}

	[Fact]
	public void Set_ALiquidStack_ReachesTheRecordInMillilitres()
	{
		var (host, _, local) = CreateSceneSession();
		Hold(host, local, Item(Backpack, "backpack", slot: 2, withState: true));

		var answer = Run(host, $"/data modify id {Backpack} Liquids[0].Amount set 500f");

		Assert.Contains("Liquids[0].Amount 250f -> 500f", answer);
		Assert.Equal(500f, Kernel(host).FindItem(Backpack)!.Value.Data.Liquids.Single(liquid => liquid.LiquidId == "water").Amount);
	}

	/// <summary>
	/// A write is the HOLDER's act: the record may name an item this client does not
	/// hold (another member's carried item, an id it never loaded), and the refusal
	/// says where the row puts it rather than silently doing nothing.
	/// </summary>
	[Fact]
	public void Set_ForAnItemThisClientDoesNotHold_IsRefusedByName()
	{
		var (host, _, _) = CreateSceneSession();
		SeedCarried(host, HostId, Item(Backpack, "backpack", slot: 1));

		var answer = Run(host, $"/data modify id {Backpack} Condition set 0.5f");

		Assert.Contains("is recorded as carried by 1001, slot 1 but this client does not hold it", answer);
	}

	[Fact]
	public void Set_ForAnUnrecordedInstanceId_IsRefusedByName()
	{
		var (host, _, _) = CreateSceneSession();

		Assert.Contains("No item #999999 is recorded in this session.", Run(host, "/data modify id 999999 Condition set 0.5f"));
	}

	/// <summary>
	/// The one-item rule the read half's ruling established, at the write: a target
	/// that names no single item is refused by name, with what to write instead.
	/// </summary>
	[Fact]
	public void Set_WithoutNamingOneItem_IsRefusedByName()
	{
		var (host, _, _) = CreateSceneSession();

		Assert.Contains("a write names ONE item", Run(host, "/data modify @s Condition set 0.5f"));
	}

	[Fact]
	public void Set_ForASlotTheRecordDoesNotNameYet_IsRefusedByName()
	{
		var (host, _, _) = CreateSceneSession();

		Assert.Contains("No item is recorded for 1001 in slot 7 yet", Run(host, "/data modify slot 7 Condition set 0.5f"));
	}

	[Fact]
	public void Set_ForAnotherMembersItem_IsRefusedByName()
	{
		var (host, _, _) = CreateSceneSession();
		SeedHostEntities(host, GuestId, 5f);
		SeedCarried(host, GuestId, Item(GuestsBandage, "bandage", slot: 2));

		var answer = Run(host, "/data modify @a[distance=1..] slot 2 Condition set 0.5f");

		Assert.Contains("is carried by 2001", answer);
		Assert.Contains("holder's own act", answer);
	}

	[Fact]
	public void Set_ForASelectorMatchingSeveralBodies_IsRefusedByName()
	{
		var (host, _, _) = CreateSceneSession();
		SeedHostEntities(host, GuestId, 5f);
		AddSecondRemote(host);
		SeedCarried(host, GuestId, Item(GuestsBandage, "bandage", slot: 0));

		var answer = Run(host, "/data modify @a[distance=1..] slot 0 Condition set 0.5f");

		Assert.Contains("matched 2 bodies", answer);
		Assert.Contains("[limit=1]", answer);
	}

	[Fact]
	public void Set_ForAnUnknownPath_NamesTheKeysTheItemCarries()
	{
		var (host, _, local) = CreateSceneSession();
		Hold(host, local, Item(Backpack, "backpack", slot: 2, withState: true));

		var answer = Run(host, $"/data modify id {Backpack} Components.Radio.gain set 1");

		Assert.Contains("has no 'Radio'", answer);
		Assert.Contains("it carries: BatteryItem", answer);
	}

	/// <summary>The scene's own refusal reaches the operator as its own sentence — the seam cannot turn "not yours to write" into "done".</summary>
	[Fact]
	public void Set_WhenTheLocalSceneRefuses_IsAnsweredWithThatReason()
	{
		var (host, _, local) = CreateSceneSession();
		Hold(host, local, Item(Backpack, "backpack", slot: 2));
		local.NextResult = LocalItemWriteResult.NotWritable;

		var answer = Run(host, $"/data modify id {Backpack} Condition set 0.5f");

		Assert.Contains("not this client's fact to write", answer);
		Assert.Equal(1f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
	}

	[Fact]
	public void Set_LiquidOnAnItemWhoseCaptureCarriesNone_IsRefusedByName()
	{
		var (host, _, local) = CreateSceneSession();
		Hold(host, local, Item(Backpack, "backpack", slot: 2));

		var answer = Run(host, $"/data modify id {Backpack} Liquids append {{LiquidId:'water',Amount:500f}}");

		Assert.Contains("has no liquid container", answer);
		Assert.Equal(1f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
	}

	/// <summary>
	/// A WORLD-rooted item's local copy is the fact and every other member's copy adopts
	/// it (the ground-canister correction), so a write there takes the correction route
	/// rather than the carried-fact broadcast — both routes update the row, so the route
	/// is asserted through the events they raise.
	/// </summary>
	[Fact]
	public void Set_OnAWorldRow_TakesTheWorldCorrectionRoute()
	{
		var (host, _, local) = CreateSceneSession();
		var item = Item(Backpack, "backpack", slot: 0, condition: 0.5f);
		local.Hold(item);
		SeedWorld(host, HostId, item);
		var reports = RecordReports(host);

		var answer = Run(host, $"/data modify id {Backpack} Condition set 0.2f");

		Assert.Contains("Condition 0.5f -> 0.2f", answer);
		Assert.Equal(0.2f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
		Assert.Contains(reports.Corrected, corrected => corrected.InstanceId == Backpack && corrected.Condition == 0.2f);
		Assert.Empty(reports.Carried);
	}

	/// <summary>
	/// A child of a WORLD container is world-rooted even though its own row says
	/// "contained": the route is the kernel's own container walk, so a dropped
	/// backpack's contents take the correction the native path gives them rather than a
	/// carried fact the receivers cannot place.
	/// </summary>
	[Fact]
	public void Set_OnAContainedChildOfAWorldContainer_TakesTheWorldCorrectionRoute()
	{
		var (host, _, local) = CreateSceneSession();
		var child = Item(GuestsBandage, "bandage", slot: 0, condition: 0.5f);
		local.Hold(child);
		SeedWorld(host, HostId, Item(Backpack, "backpack", slot: 0));
		var parent = Item(Backpack, "backpack", slot: 0);
		parent.Contents.Add(child);
		Assert.True(Kernel(host).TrySyncContainerFacts(HostId, parent, out _, out _));
		var reports = RecordReports(host);

		var answer = Run(host, $"/data modify id {GuestsBandage} Condition set 0.2f");

		Assert.Contains("Condition 0.5f -> 0.2f", answer);
		Assert.Contains(reports.Corrected, corrected => corrected.InstanceId == GuestsBandage);
		Assert.Empty(reports.Carried);
	}

	/// <summary>
	/// Without an active session the report the write ends with is dropped by its own
	/// guard, so the verb refuses instead of changing the local object and calling that
	/// "reported" — the answer names the missing session.
	/// </summary>
	[Fact]
	public void Set_OutsideAnActiveSession_IsRefusedByName()
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		using var node = TestNode.Create(HostId, network, new FakeSteamService(HostId), clock, pumpFirstFrame: true);

		var answer = Run(node, "/data modify id 4101 Condition set 0.5f");

		Assert.Contains("No active session", answer);
	}

	/// <summary>Every console test runs against a scene of its own, so nothing a test writes leaks into another.</summary>
	private static (TestNode Host, TestNode Guest, FakeLocalItemAccess Local) CreateSceneSession()
	{
		var local = new FakeLocalItemAccess();
		var (host, guest) = CreateSession(services => services.Replace(ServiceDescriptor.Singleton<ILocalItemAccess>(local)));
		return (host, guest, local);
	}

	/// <summary>Puts one item on BOTH halves of the write's world: the record (the kernel row) and the local scene (the object this client holds).</summary>
	private static void Hold(TestNode host, FakeLocalItemAccess local, CharacterItemMsg item)
	{
		local.Hold(item);
		SeedCarried(host, HostId, item);
	}

	/// <summary>Puts one item into the record as a WORLD row — the row a dropped or ground item has.</summary>
	private static void SeedWorld(TestNode node, ulong owner, CharacterItemMsg item, float x = 3f, float y = 4f) =>
		Assert.True(Kernel(node).TrySpawn(owner, new ItemIdentity(item.InstanceId, item.ItemId), ItemLocation.World(x, y), item, out _, out _));

	/// <summary>
	/// The two report routes are told apart by what they RAISE, not by the row changing:
	/// the carried-fact route publishes the owner's own fact, the world route corrects
	/// every other copy.
	/// </summary>
	private static (List<CharacterItemMsg> Carried, List<CharacterItemMsg> Corrected) RecordReports(TestNode host)
	{
		var carried = new List<CharacterItemMsg>();
		var corrected = new List<CharacterItemMsg>();
		var items = host.Services.GetRequiredService<IItemControl>();
		items.ItemCarriedSyncReceived += (_, item, _) => carried.Add(item);
		items.ItemCorrectionReceived += corrected.Add;
		return (carried, corrected);
	}

	[Fact]
	public void Help_ListsTheDataCommand()
	{
		var (host, _) = CreateSession();

		Assert.Contains("/data", Run(host, "/help"));
	}

	/// <summary>
	/// A selector may name several bodies, but a verb that names ONE item needs one
	/// body to name it on: the verdict is taken at execution, on the resolved set,
	/// and answered by name (user ruling 2026-10-14) — the grammar stays as wide as
	/// the selector vocabulary. Three members are needed to see it; the selector is narrowed with a distance filter so the
	/// executor's own body is out of the match (`@a` itself names everyone, the sender included — ticket
	/// `selectors-exclude-the-executor`).
	/// </summary>
	[Fact]
	public void Get_ForASlotWithASelectorMatchingSeveralBodies_IsRefusedByName()
	{
		var (host, _) = CreateSession();
		SeedHostEntities(host, GuestId, 5f);
		AddSecondRemote(host);
		SeedCarried(host, GuestId, GuestsBandage, "bandage", slot: 0);

		var answer = Run(host, "/data get @a[distance=1..] slot 0");

		Assert.Contains("matched 2 bodies", answer);
		Assert.Contains("[limit=1]", answer);

		// The advice in that answer is itself checkable: one member's slot reads normally.
		var narrowed = Run(host, "/data get @a[distance=1..,limit=1] slot 0");
		Assert.DoesNotContain("matched", narrowed);
		Assert.Contains("slot 0", narrowed);
	}

	/// <summary>The listing form is the other shape: several bodies are fine, because each body's block is its own answer.</summary>
	[Fact]
	public void Get_ListingSeveralBodies_IsAnsweredForEach()
	{
		var (host, _) = CreateSession();
		SeedHostEntities(host, GuestId, 5f);
		AddSecondRemote(host);
		SeedCarried(host, GuestId, GuestsBandage, "bandage", slot: 0);
		SeedCarried(host, SecondGuest, Backpack, "backpack", slot: 1);

		var answer = Run(host, "/data get @a[distance=1..]");

		Assert.Contains("Items recorded for 2001 (1):", answer);
		Assert.Contains("Items recorded for 2002 (1):", answer);
	}

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

		Assert.NotNull(entities.GetRemotePlayer(SecondGuest));
	}

	[Fact]
	public void Merge_ChangesEveryPropertyItNames()
	{
		var (host, _, local) = CreateSceneSession();
		Hold(host, local, Item(Backpack, "backpack", slot: 1, condition: 0.8f, withState: true));

		var answer = Run(host, $"/data merge id {Backpack} {{Condition:0.5f,Favourited:true}}");

		// The answer names each property in the order the operator wrote them, and both landed.
		Assert.Contains("Condition 0.8f -> 0.5f", answer);
		Assert.Contains("Favourited false -> true", answer);
		Assert.Equal(0.5f, local.Held(Backpack)!.Condition);
		Assert.Equal(0.5f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
		Assert.True(local.Held(Backpack)!.Favourited);
	}

	[Fact]
	public void Merge_WithAPropertyThatCannotBeApplied_WritesNoneOfThem()
	{
		// The atomicity the single commit buys: the condition is legal and the liquid is not on the
		// item, so the whole command refuses and the first property does not land on its own. A
		// per-property commit would pass the merge's happy path and fail here.
		var (host, _, local) = CreateSceneSession();
		Hold(host, local, Item(Backpack, "backpack", slot: 1, condition: 0.8f, withState: true));

		var answer = Run(host, $"/data merge id {Backpack} {{Condition:0.5f,Liquids:[{{LiquidId:'water',Amount:999f}}]}}");

		Assert.Contains("holds 500 ml and the state names 999 ml", answer);
		Assert.Equal(0.8f, local.Held(Backpack)!.Condition);
		Assert.Equal(0.8f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
		Assert.Equal(250f, local.Held(Backpack)!.Liquids.Single(liquid => liquid.LiquidId == "water").Amount);
	}

	[Fact]
	public void Remove_DeletesALiquidStackAndRefusesToStripARequiredKey()
	{
		var (host, _, local) = CreateSceneSession();
		Hold(host, local, Item(Backpack, "backpack", slot: 1, withState: true));

		var answer = Run(host, $"/data remove id {Backpack} Liquids[0]");

		Assert.Contains("Liquids[0] {LiquidId:", answer);
		Assert.Contains("Amount:250f} -> removed", answer);

		// The stack goes and the container stays, empty — the list element is the ordinary
		// removable node. A field the component always carries cannot be removed: its state is
		// complete, and the adapter's bind refuses the document by name rather than defaulting it.
		Assert.Empty(local.Held(Backpack)!.Liquids);
		Assert.Empty(Kernel(host).FindItem(Backpack)!.Value.Data.Liquids);

		var refused = Run(host, $"/data remove id {Backpack} Components.BatteryItem.charge");
		Assert.Contains("missing or takes the wrong kind", refused);
		Assert.Equal(0.75f, local.Held(Backpack)!.Components.Single().Fields.Single(field => field.Name == "charge").FloatValue);
	}

	private static ItemKernelAuthority Kernel(TestNode node) =>
		node.Services.GetRequiredService<ItemKernelAuthority>();

	private static void SeedCarried(
		TestNode node,
		ulong owner,
		ulong instanceId,
		string definition,
		int slot,
		float condition = 1f,
		bool favourited = false,
		bool withState = false) =>
		SeedCarried(node, owner, Item(instanceId, definition, slot, condition, favourited, withState));

	/// <summary>The same row, as the local scene's own capture — the write's two halves must agree on what the item is before it is written.</summary>
	private static void SeedCarried(TestNode node, ulong owner, CharacterItemMsg item) =>
		Assert.True(Kernel(node).TrySpawnCarried(owner, item.InstanceId, item.ItemId, item, out _, out _));

	private static CharacterItemMsg Item(
		ulong instanceId,
		string definition,
		int slot,
		float condition = 1f,
		bool favourited = false,
		bool withState = false)
	{
		var item = new CharacterItemMsg
		{
			InstanceId = instanceId,
			ItemId = definition,
			SlotIndex = slot,
			Condition = condition,
			Favourited = favourited,
		};

		if (withState)
		{
			item.Liquids = [new LiquidStackMsg { LiquidId = "water", Amount = 250f }];
			item.Components =
			[
				new ComponentStateMsg
				{
					TypeName = "BatteryItem",
					Fields = [new ComponentFieldMsg { Name = "charge", Kind = 1, FloatValue = 0.75f }],
				},
			];
		}

		return item;
	}

	/// <summary>Runs one console line and answers with the line it printed.</summary>
	private static string Run(TestNode node, string line)
	{
		var console = node.Services.GetRequiredService<ICommandControl>();
		Assert.True(console.TryExecute(line), $"'{line}' was not accepted by the console");
		return console.Lines[console.Lines.Count - 1].Text;
	}
}
