using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The custody projection's pure half: the kernel's rows against the objects a client has. The rule under test is the design page's
/// (`docs/design/model/item-custody.md`) — the row is the only authority, an object is real when the row is this client's and a display when
/// it is somebody else's — and every disagreement is one named kind, because each kind has its own repair and only one of them may ever be
/// reported as an item dying.
/// </summary>
public class ItemCustodyComparisonTests
{
	private const ulong Local = 1000;
	private const ulong Other = 2000;

	[Fact]
	public void AnObjectWithNoRow_IsAnOrphan()
	{
		var rows = new Dictionary<ulong, ItemState>();
		var objects = new[] { new ItemCustodyComparison.LocalObject(7, IsDisplay: false) };

		var mismatch = Assert.Single(ItemCustodyComparison.Compare(rows, objects, Local));

		Assert.Equal(7UL, mismatch.InstanceId);
		Assert.Equal(ItemCustodyComparison.MismatchKind.ObjectWithoutRow, mismatch.Kind);
	}

	[Fact]
	public void ALiveRowWithNoObject_MustBeShown()
	{
		var rows = Rows((7, ItemLocation.World(1f, 2f)));
		var objects = Array.Empty<ItemCustodyComparison.LocalObject>();

		var mismatch = Assert.Single(ItemCustodyComparison.Compare(rows, objects, Local));

		Assert.Equal(ItemCustodyComparison.MismatchKind.RowWithoutObject, mismatch.Kind);
	}

	[Fact]
	public void ATerminalRowWithNoObject_IsClean()
	{
		var rows = Rows((7, ItemLocation.Terminal()));
		var objects = Array.Empty<ItemCustodyComparison.LocalObject>();

		Assert.Empty(ItemCustodyComparison.Compare(rows, objects, Local));
	}

	[Fact]
	public void ATerminalRowThatStillHasAnObject_IsAGhost()
	{
		var rows = Rows((7, ItemLocation.Terminal()));
		var objects = new[] { new ItemCustodyComparison.LocalObject(7, IsDisplay: false) };

		var mismatch = Assert.Single(ItemCustodyComparison.Compare(rows, objects, Local));

		Assert.Equal(ItemCustodyComparison.MismatchKind.TerminalRowWithObject, mismatch.Kind);
	}

	[Fact]
	public void ThisClientsCarriedRow_IsReal()
	{
		var rows = Rows((7, ItemLocation.Carried(new ActorId(Local))));
		var real = new[] { new ItemCustodyComparison.LocalObject(7, IsDisplay: false) };
		var display = new[] { new ItemCustodyComparison.LocalObject(7, IsDisplay: true) };

		Assert.Empty(ItemCustodyComparison.Compare(rows, real, Local));
		var mismatch = Assert.Single(ItemCustodyComparison.Compare(rows, display, Local));
		Assert.Equal(ItemCustodyComparison.MismatchKind.KindDisagrees, mismatch.Kind);
	}

	[Fact]
	public void AnotherMembersCarriedRow_IsADisplay()
	{
		var rows = Rows((9, ItemLocation.Carried(new ActorId(Other))));
		var display = new[] { new ItemCustodyComparison.LocalObject(9, IsDisplay: true) };
		var real = new[] { new ItemCustodyComparison.LocalObject(9, IsDisplay: false) };

		Assert.Empty(ItemCustodyComparison.Compare(rows, display, Local));
		var mismatch = Assert.Single(ItemCustodyComparison.Compare(rows, real, Local));
		Assert.Equal(ItemCustodyComparison.MismatchKind.KindDisagrees, mismatch.Kind);
	}

	[Fact]
	public void AWorldRow_IsRealOnEveryClient()
	{
		// Corrected by the first shadow run on the machine (guest log 2026-10-25 19:05: 279 world rows disagreed): a world item is simulated
		// locally wherever the scene shows it, so it is never another member's incarnation — only a carried/contained/applied row of somebody
		// else is a display.
		var rows = Rows((11, ItemLocation.World(3f, 4f)));
		var real = new[] { new ItemCustodyComparison.LocalObject(11, IsDisplay: false) };
		var display = new[] { new ItemCustodyComparison.LocalObject(11, IsDisplay: true) };

		Assert.Empty(ItemCustodyComparison.Compare(rows, real, Local));
		var mismatch = Assert.Single(ItemCustodyComparison.Compare(rows, display, Local));
		Assert.Equal(ItemCustodyComparison.MismatchKind.KindDisagrees, mismatch.Kind);
	}

	[Fact]
	public void TheRowInSlot_ComesFromTheAuthority_NotASecondTable()
	{
		// The reader switch's query: a clone render asks "which row is in slot 3 of that body" and gets the kernel's answer. The slot is
		// the row's own fact (`ItemData.SlotIndex`), the owner is the location's.
		var rows = new Dictionary<ulong, ItemState>
		{
			[21] = Row(21, ItemLocation.Carried(new ActorId(Other)), slot: 3),
			[22] = Row(22, ItemLocation.Carried(new ActorId(Other)), slot: 4),
			[23] = Row(23, ItemLocation.Carried(new ActorId(Local)), slot: 3),
		};

		Assert.True(ItemCustodyComparison.TryFindRowInSlot(rows, Other, 3, out var found));
		Assert.Equal(21UL, found);
		Assert.False(ItemCustodyComparison.TryFindRowInSlot(rows, Other, 9, out _));
	}

	[Fact]
	public void ATerminalRow_NeverAnswersASlot()
	{
		var rows = new Dictionary<ulong, ItemState> { [31] = Row(31, ItemLocation.Terminal(), slot: 2) };

		Assert.False(ItemCustodyComparison.TryFindRowInSlot(rows, Other, 2, out _));
	}

	private static ItemState Row(ulong id, ItemLocation location, int slot) =>
		new(new ItemIdentity(id, "cu:widget"), 1, location) { Data = ItemData.Empty with { SlotIndex = slot } };

	private static Dictionary<ulong, ItemState> Rows(params (ulong Id, ItemLocation Location)[] rows)
	{
		var map = new Dictionary<ulong, ItemState>();
		foreach (var (id, location) in rows)
		{
			map[id] = new ItemState(new ItemIdentity(id, "cu:widget"), 1, location);
		}

		return map;
	}
	[Fact]
	public void TheCloneRender_PrefersTheRowOverTheSnapshotsOwnSlot()
	{
		// Stage 4's audit question, pinned as a test: the snapshot entry's slot is a FALLBACK for the frames before the kernel judged the row, and the
		// row wins whenever it has an answer. Deleting the fallback would blink a clone out during a restore's first snapshot (documented in
		// CloneItemSelection), and deleting the row's priority would put a second authority back — this test fails on either change.
		var rows = new Dictionary<ulong, ItemState> { [42] = Row(42, ItemLocation.Carried(new ActorId(Other)), slot: 2) };
		var data = Snapshot(Other, (42, 3), (43, 2));

		var selected = CloneItemSelection.ItemInSlot(rows, data, Other, 2);

		Assert.NotNull(selected);
		Assert.Equal(42UL, selected!.InstanceId);
	}

	[Fact]
	public void TheCloneRender_FallsBackToTheSnapshotsSlotWhenNoRowAnswers()
	{
		var rows = new Dictionary<ulong, ItemState>();
		var data = Snapshot(Other, (43, 2));

		var selected = CloneItemSelection.ItemInSlot(rows, data, Other, 2);

		Assert.NotNull(selected);
		Assert.Equal(43UL, selected!.InstanceId);
	}

	private static CharacterDataMsg Snapshot(ulong owner, params (ulong Id, int Slot)[] items) => new()
	{
		OwnerSteamId = owner,
		Items = [.. items.Select(item => new CharacterItemMsg { InstanceId = item.Id, ItemId = "cu:widget", SlotIndex = item.Slot })],
	};
	[Fact]
	public void ARowOwnedByADepartedMember_IsNotReported()
	{
		// Host log 2026-10-26 20:18 reported `5197989525:RowWithoutObject:Carried` and two more for a guest who had LEFT: the kernel keeps a departed
		// member's carried rows (their inventory lives there) while the display objects are destroyed when they leave, so demanding an object for
		// them reports the inventory of everyone who ever left.
		var rows = RowsOf((5197989525, ItemLocation.Carried(new ActorId(Other))));
		var objects = Array.Empty<ItemCustodyComparison.LocalObject>();

		var withoutRoster = Assert.Single(ItemCustodyComparison.Compare(rows, objects, Local));
		Assert.Equal(ItemCustodyComparison.MismatchKind.RowWithoutObject, withoutRoster.Kind);

		Assert.Empty(ItemCustodyComparison.Compare(rows, objects, Local, presentOwners: []));
	}

	[Fact]
	public void ARowOwnedByAPresentMember_IsStillReported()
	{
		var rows = RowsOf((5197989525, ItemLocation.Carried(new ActorId(Other))));
		var objects = Array.Empty<ItemCustodyComparison.LocalObject>();

		var mismatch = Assert.Single(ItemCustodyComparison.Compare(rows, objects, Local, presentOwners: [Other]));

		Assert.Equal(ItemCustodyComparison.MismatchKind.RowWithoutObject, mismatch.Kind);
	}

	private static Dictionary<ulong, ItemState> RowsOf(params (ulong Id, ItemLocation Location)[] rows)
	{
		var map = new Dictionary<ulong, ItemState>();
		foreach (var (id, location) in rows)
		{
			map[id] = new ItemState(new ItemIdentity(id, "cu:widget"), 1, location);
		}

		return map;
	}
	[Fact]
	public void AKindDisagreement_CarriesTheMarkerTheObjectWore()
	{
		// The reading that opened the World-row ticket named an id and nothing else, so the next step needed a second run just to learn which display it
		// was. The marker now rides the mismatch (acceptance row 3 of that ticket).
		var rows = new Dictionary<ulong, ItemState> { [7] = Row(7, ItemLocation.World(1f, 2f), slot: 0) };
		var objects = new[] { new ItemCustodyComparison.LocalObject(7, IsDisplay: true, Marker: "standing") };

		var mismatch = Assert.Single(ItemCustodyComparison.Compare(rows, objects, Local));

		Assert.Equal(ItemCustodyComparison.MismatchKind.KindDisagrees, mismatch.Kind);
		Assert.Equal("standing", mismatch.Marker);
	}
	[Fact]
	public void ContainedIn_AnswersOnlyTheDirectChildrenInIdOrder()
	{
		// A world container's view is built from these rows (a dropped toolbox arrived empty when it was built from the message instead).
		var rows = new Dictionary<ulong, ItemState>
		{
			[7] = Row(7, ItemLocation.World(0f, 0f), slot: -1),
			[9] = Row(9, ItemLocation.Contained(new ActorId(Other), 7), slot: -1),
			[8] = Row(8, ItemLocation.Contained(new ActorId(Other), 7), slot: -1),
			[10] = Row(10, ItemLocation.Contained(new ActorId(Other), 9), slot: -1),
		};

		var children = ItemCustodyComparison.ContainedIn(rows, 7);

		Assert.Equal([8UL, 9UL], children.Select(child => child.Identity.InstanceId));
		Assert.Single(ItemCustodyComparison.ContainedIn(rows, 9));
		Assert.Empty(ItemCustodyComparison.ContainedIn(rows, 8));
	}
	[Fact]
	public void AnItemInsideAContainerInTheWorld_IsRealForEveryone()
	{
		// The receiver materializes a world container's contents from the rows, so those objects are real on every client even though the row is
		// CONTAINED and owned by another member (2026-10-26: the dropped toolbox's water bottle was real and the projection called it a disagreement).
		var rows = new Dictionary<ulong, ItemState>
		{
			[7] = Row(7, ItemLocation.World(0f, 0f), slot: -1),
			[8] = Row(8, ItemLocation.Contained(new ActorId(Other), 7), slot: -1),
		};

		Assert.False(ItemCustodyComparison.IsDisplayFor(rows, rows[8].Location, Local));
	}

	[Fact]
	public void AnItemInsideAContainerAMemberCarries_IsThatMembersDisplay()
	{
		var rows = new Dictionary<ulong, ItemState>
		{
			[7] = Row(7, ItemLocation.Carried(new ActorId(Other)), slot: 0),
			[8] = Row(8, ItemLocation.Contained(new ActorId(Other), 7), slot: -1),
		};

		Assert.True(ItemCustodyComparison.IsDisplayFor(rows, rows[8].Location, Local));
		Assert.False(ItemCustodyComparison.IsDisplayFor(rows, rows[8].Location, Other));
	}
}
