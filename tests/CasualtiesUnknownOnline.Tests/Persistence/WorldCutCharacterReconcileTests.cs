using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// A cut writes ONE placement per item id. The two carriers state different halves of the same
/// fact — the kernel checkpoint owns WHO holds an item (and what contains it), the member's
/// character file owns the slot it occupies — and they are written at different moments: the
/// host's own character is a live capture, while a member's file comes from its last 1 Hz report,
/// and a native pickup/drop reports the ITEM fact immediately but does NOT fire the immediate
/// character re-report (<c>OnInventoryChanged</c>). A cut inside that window would store two
/// placements for one id; the restore then applies both halves independently. These cases pin the
/// cut's reconcile: the kernel's placement wins, the file's slot is kept where it has one, and an
/// item the kernel has never judged is left alone (the report is then its only carrier).
/// </summary>
public class WorldCutCharacterReconcileTests
{
	private const ulong HostId = 1001UL;
	private const ulong GuestId = 2002UL;

	[Fact]
	public void ACarriedItemTheMembersFileMisses_IsWrittenIntoItWithNoBodySlot()
	{
		using var fixture = Start();

		// The window: the kernel learned the pickup (the item report), the member's last 1 Hz
		// report still describes the body without it. The kernel row states no body slot — a
		// pickup command carries no item data — so the file must carry the item UNPLACED and let
		// the restored body's own slot rules place it.
		Assert.True(fixture.Kernel.TrySpawnCarried(GuestId, 201UL, "bag", Item(201UL, "bag", slot: -1), out _, out _));

		Cut(fixture);

		var node = Assert.Single(CharacterNodes(fixture, GuestId), candidate => candidate.Id == 201UL);
		Assert.Equal(-1, node.Slot);
		Assert.Equal(0UL, node.ParentId);
	}

	[Fact]
	public void AnItemTheKernelPlacedInTheWorld_IsRemovedFromTheMembersFile()
	{
		using var fixture = Start();

		// The reverse window: the member's file still carries the bag (its last report), the
		// kernel holds it in the world (the drop's item report). Applying both halves would put
		// the item in a body AND in the world under ONE id.
		fixture.Characters.SaveCharacterData(GuestId, Character(Item(200UL, "rope", slot: 0), Item(201UL, "bag", slot: 2)));
		Assert.True(fixture.Kernel.TrySpawnCarried(GuestId, 201UL, "bag", Item(201UL, "bag", slot: 2), out _, out _));
		Assert.True(fixture.Kernel.TryDrop(GuestId, 201UL, ItemLocation.World(1f, 2f), null, out _, out _));

		Cut(fixture);

		Assert.DoesNotContain(CharacterNodes(fixture, GuestId), candidate => candidate.Id == 201UL);
		Assert.Equal(1, ItemLocationKindOf(fixture, 201UL));
	}

	[Fact]
	public void ACarriedContainerTheMembersFileMisses_ArrivesWithItsContentsUnderIt()
	{
		using var fixture = Start();

		Assert.True(fixture.Kernel.TrySpawnCarried(GuestId, 201UL, "bag", Item(201UL, "bag", slot: -1), out _, out _));
		Assert.True(fixture.Kernel.TrySpawn(
			GuestId,
			new ItemIdentity(202UL, "rope"),
			ItemLocation.Contained(new ActorId(GuestId), 201UL),
			Item(202UL, "rope", slot: -1),
			out _,
			out _));

		Cut(fixture);

		var bag = Assert.Single(CharacterNodes(fixture, GuestId), candidate => candidate.Id == 201UL);
		var rope = Assert.Single(CharacterNodes(fixture, GuestId), candidate => candidate.Id == 202UL);
		Assert.Equal(201UL, rope.ParentId);
		Assert.Equal(0UL, bag.ParentId);
	}

	[Fact]
	public void AContainerChildTheKernelMovedOut_MovesToTheTopAndKeepsTheReportsState()
	{
		using var fixture = Start();

		// The last 1 Hz report still shows the rope inside the bag; the kernel says the member took
		// it out into its carried inventory. The placement is the kernel's, the state is the report's.
		var statedRope = Item(202UL, "rope", slot: 0);
		statedRope.Condition = 0.42f;
		var statedBag = Item(201UL, "bag", slot: 0);
		statedBag.Contents = [statedRope];
		fixture.Characters.SaveCharacterData(GuestId, Character(statedBag));
		Assert.True(fixture.Kernel.TrySpawnCarried(GuestId, 201UL, "bag", Item(201UL, "bag", slot: 0), out _, out _));
		var kernelRope = Item(202UL, "rope", slot: -1);
		kernelRope.Condition = 0.1f;
		Assert.True(fixture.Kernel.TrySpawnCarried(GuestId, 202UL, "rope", kernelRope, out _, out _));

		Cut(fixture);

		var rope = Assert.Single(CharacterNodes(fixture, GuestId), candidate => candidate.Id == 202UL);
		Assert.Equal(0UL, rope.ParentId);
		Assert.Equal(-1, rope.Slot);
		Assert.Equal(0.42f, rope.Condition, 3);
		var bag = Assert.Single(CharacterNodes(fixture, GuestId), candidate => candidate.Id == 201UL);
		Assert.Equal(0, bag.Children);
	}

	[Fact]
	public void AnIdTheKernelHasNeverJudged_IsKeptWhereTheFileCarriesIt()
	{
		using var fixture = Start();

		// The registration cadence has not caught up: the member created the item locally and the
		// kernel has no row for it. The character report is the id's ONLY carrier, so the cut must
		// not treat the kernel's silence as "this item is not on the body".
		fixture.Characters.SaveCharacterData(GuestId, Character(Item(200UL, "rope", slot: 0), Item(203UL, "dogfood", slot: 1)));

		Cut(fixture);

		Assert.Contains(CharacterNodes(fixture, GuestId), candidate => candidate.Id == 203UL);
		Assert.Equal(1, CharacterNodes(fixture, GuestId).Count(candidate => candidate.Id == 203UL));
	}

	[Fact]
	public void AFileSlotIsKept_WhenTheKernelCarriesTheSameItem()
	{
		using var fixture = Start();

		// Both carriers agree about the item: the file's slot is the body's own fact and must not
		// be replaced by the kernel row's (a pickup row carries the world item's stale data).
		fixture.Characters.SaveCharacterData(GuestId, Character(Item(201UL, "bag", slot: 3)));
		Assert.True(fixture.Kernel.TrySpawnCarried(GuestId, 201UL, "bag", Item(201UL, "bag", slot: -1), out _, out _));

		Cut(fixture);

		var node = Assert.Single(CharacterNodes(fixture, GuestId), candidate => candidate.Id == 201UL);
		Assert.Equal(3, node.Slot);
	}

	[Fact]
	public void TheLiveCharacterTableIsNotMutated_ByTheCutsCorrection()
	{
		using var fixture = Start();
		var stored = fixture.Characters.GetSavedCharacter(GuestId)!;
		var before = JsonSerializer.Serialize(stored);

		Assert.True(fixture.Kernel.TrySpawnCarried(GuestId, 201UL, "bag", Item(201UL, "bag", slot: -1), out _, out _));

		Cut(fixture);

		// The cut writes a COPY: the table stays the member's last report, which is what the next
		// report and the live clone rendering expect. The serialized comparison covers the item
		// state a mutation would really change (slot, condition, contents), not just the id list.
		Assert.Same(stored, fixture.Characters.GetSavedCharacter(GuestId));
		Assert.Equal(before, JsonSerializer.Serialize(stored));
	}

	[Fact]
	public void TwoUnboundItems_AreBothKept()
	{
		using var fixture = Start();

		// The starting supplies can be reported before the registration edge stamps their ids: they all
		// carry instanceId 0, and 0 is not an identity — one must not collapse the others.
		fixture.Characters.SaveCharacterData(GuestId, Character(
			Item(0UL, "bandage", slot: 0),
			Item(0UL, "dogfood", slot: 1)));

		Cut(fixture);

		var nodes = CharacterNodes(fixture, GuestId);
		Assert.Equal(2, nodes.Count);
		Assert.All(nodes, node => Assert.Equal(0UL, node.Id));
	}

	[Fact]
	public void AnUnknownContainerInsideARemovedOne_IsLiftedOnce_AndReleasesTheItemTheKernelHolds()
	{
		using var fixture = Start();

		// The file's removed bag holds an unknown toolbox, whose rope the kernel says the member took
		// out into its carried inventory. The toolbox is lifted; the rope must not be serialized twice
		// (once inside the lifted toolbox and once at the top).
		var rope = Item(202UL, "rope", slot: 0);
		rope.Condition = 0.42f;
		var toolbox = Item(900UL, "toolbox", slot: 0);
		toolbox.Contents = [rope];
		var bag = Item(201UL, "bag", slot: 0);
		bag.Contents = [toolbox];
		fixture.Characters.SaveCharacterData(GuestId, Character(bag));
		Assert.True(fixture.Kernel.TrySpawnCarried(GuestId, 201UL, "bag", Item(201UL, "bag", slot: 0), out _, out _));
		Assert.True(fixture.Kernel.TryDrop(GuestId, 201UL, ItemLocation.World(1f, 2f), null, out _, out _));
		Assert.True(fixture.Kernel.TrySpawnCarried(GuestId, 202UL, "rope", Item(202UL, "rope", slot: -1), out _, out _));

		Cut(fixture);

		var nodes = CharacterNodes(fixture, GuestId);
		Assert.Single(nodes, node => node.Id == 202UL);
		Assert.DoesNotContain(nodes, node => node.Id == 201UL);
		var lifted = Assert.Single(nodes, node => node.Id == 900UL);
		Assert.Equal(0, lifted.Children);
		Assert.Equal(0UL, lifted.ParentId);
		Assert.Equal(-1, lifted.Slot);
		Assert.Equal(0UL, Assert.Single(nodes, node => node.Id == 202UL).ParentId);
	}

	[Fact]
	public void ALayerEndCutReconcilesTheMemberFileToo()
	{
		using var fixture = Start();

		// The layer-end cut drops its in-layer item ROWS from the payload, but its character files
		// cross the boundary with the body — and an item the kernel holds on that body must still
		// be in the file, or the member loses it on the next Continue.
		Assert.True(fixture.Kernel.TrySpawnCarried(GuestId, 202UL, "rope", Item(202UL, "rope", slot: -1), out _, out _));

		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, WorldSaveCaptureTests.Run(layerIndex: 1), out _, out _));

		Assert.Contains(CharacterNodes(fixture, GuestId), candidate => candidate.Id == 202UL);
	}

	// ---- fixture ----

	private static WorldSaveFixture Start()
	{
		var fixture = WorldSaveFixture.Create("cut-character-reconcile");
		fixture.Session.AddMember(GuestId, "Guest");
		fixture.Characters.SaveCharacterData(GuestId, Character(Item(200UL, "rope", slot: 0)));
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));
		return fixture;
	}

	private static void Cut(WorldSaveFixture fixture)
	{
		var report = WorldSaveCaptureTests.MenuReturnCut(fixture, WorldSaveCaptureTests.Character(100UL, "bag"));
		Assert.True(report.Captured, report.Summary);
	}

	private static CharacterDataMsg Character(params CharacterItemMsg[] items) => new()
	{
		SlotCount = 5,
		Items = [.. items],
	};

	private static CharacterItemMsg Item(ulong instanceId, string definitionId, int slot) => new()
	{
		InstanceId = instanceId,
		ItemId = definitionId,
		Condition = 1f,
		SlotIndex = slot,
	};

	private readonly record struct CharacterNode(ulong Id, int Slot, ulong ParentId, float Condition, int Children);

	private static IReadOnlyList<CharacterNode> CharacterNodes(WorldSaveFixture fixture, ulong peerId)
	{
		var path = Path.Combine(LiveDirectory(fixture), SaveArchiveFormat.CharactersFolderName, $"steam-{peerId}.json");
		using var document = JsonDocument.Parse(File.ReadAllBytes(path));
		var nodes = new List<CharacterNode>();
		CollectNodes(document.RootElement[0].GetProperty("items"), 0UL, nodes);
		return nodes;
	}

	private static void CollectNodes(JsonElement items, ulong parentId, List<CharacterNode> nodes)
	{
		foreach (var item in items.EnumerateArray())
		{
			var id = item.GetProperty("instanceId").GetUInt64();
			var contents = item.GetProperty("contents");
			nodes.Add(new CharacterNode(
				id,
				item.GetProperty("slotIndex").GetInt32(),
				parentId,
				item.GetProperty("condition").GetSingle(),
				contents.GetArrayLength()));
			CollectNodes(contents, id, nodes);
		}
	}

	/// <summary>The kernel row's location kind as the archive's <c>items.json</c> states it (the WIRE spelling: World = 1, Carried = 2, …), or -1 when the payload carries no row for the id.</summary>
	private static int ItemLocationKindOf(WorldSaveFixture fixture, ulong instanceId)
	{
		var path = Path.Combine(LiveDirectory(fixture), SaveArchiveFormat.ItemsFileName);
		using var document = JsonDocument.Parse(File.ReadAllBytes(path));
		foreach (var row in document.RootElement.EnumerateArray())
		{
			if (row.GetProperty("identity").GetProperty("instanceId").GetUInt64() == instanceId)
			{
				return row.GetProperty("location").GetProperty("kind").GetInt32();
			}
		}

		return -1;
	}

	private static string LiveDirectory(WorldSaveFixture fixture) =>
		fixture.Repository.Workspace.LiveDirectory(fixture.WorldId);
}
