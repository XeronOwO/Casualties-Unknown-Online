using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using UnityEngine;
using CommitStatus = CasualtiesUnknownOnline.GameAdapter.Items.ItemReportCommitter.CommitStatus;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The pickup chain's owner: every way an item enters an inventory from the
/// world (drag pickup, auto-pickup, commands, WEARING — all land in
/// Body.PickUpItem). One pickup operation = one report; the drag-to-hand
/// reorder is cancelled against the drop state machine (the same-frame
/// DropItem + PickUpItem sequence is a body-internal move, PlayerCamera.cs:
/// 1623/1629), and a GENERATION-TIME item (no instance id — world-gen
/// determinism covers it) is bound and removed on the peer via
/// spawn-then-pickup. The pickup-start hook records the ground position
/// explicitly — the picked-up hook runs after the re-parent moved the item
/// (CLAUDE.md #9: explicit state between hooks, never scene inference).
/// </summary>
internal sealed class PickupSync(
	ItemService items,
	ItemApplication application,
	ItemDropState dropState,
	ItemIdAllocator ids,
	OperationTrace trace,
	ItemReportCommitter reports)
{
	private readonly ItemService _items = items;
	private readonly ItemApplication _application = application;
	private readonly ItemDropState _dropState = dropState;
	private readonly ItemIdAllocator _ids = ids;
	private readonly OperationTrace _trace = trace;
	private readonly ItemReportCommitter _reports = reports;

	/// <summary>True while a remote message is being applied — local reports must stay silent (call identity lives in CallContext).</summary>
	private bool IsRemoteApply => CallContext.Current == CallContext.Origin.RemoteApply;

	/// <summary>The pickup-start position of the last PickUpItem call — still on the ground HERE, the picked-up hook runs after the re-parent. Id-less generation-time items have no PickupOrigins key — this covers them.</summary>
	private (Item Item, Vector2 Pos)? _lastPickupStart;

	/// <summary>The Body.slots index the item sits in (the item's parent is the slot's transform, Body.cs:1350), or -1 when it is not in a body slot (worn, in a container, still in the world).</summary>
	private static int SlotIndexOf(Item item)
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
		if (body == null) // Unity object — ==
		{
			return -1;
		}

		for (var i = 0; i < body.slots.Length; i++)
		{
			if (body.slots[i].transform.childCount > 0
				&& body.slots[i].transform.GetChild(0).GetComponent<Item>() == item) // Unity objects — ==
			{
				return i;
			}
		}

		return -1;
	}

	internal void OnPickupStart(Item item)
	{
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null && _application.PickupOrigins.Count < 256) // Unity object — ==; bounded, oldest overwritten
		{
			_application.PickupOrigins[idComp.Id] = item.transform.position;
		}

		_lastPickupStart = (item, item.transform.position); // the ground position, before the pickup re-parents the item
	}

	internal void OnPickedUp(Item item)
	{
		if (IsRemoteApply)
		{
			return;
		}

		// The item re-entered an inventory — a pending drop of it is cancelled
		// and NOT reported: the same-frame drag sequence (PlayerCamera.cs:1623
		// DropItem + 1629 PickUpItem — a body-internal reorder) dropped the
		// item from a slot and re-picked it in one player input. The drop got
		// an instance id but the item never entered the world table, so
		// reporting the pickup made the host refuse the unknown id and roll the
		// item back out of the inventory ("dragged from a slot to the hand —
		// immediately dropped, forever unpickable").
		if (_dropState.TryCancel(item, out var pickedOp))
		{
			_trace.End(pickedOp, OperationTrace.IdOf(item), "OnItemPickedUp", "Cancelled", "RePick");
			return;
		}

		var op = _trace.NextOperationId();
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null) // Unity object — ==
		{
			// A picked-up CONTAINER carries its contents out of the world: each
			// content item that had an instance id leaves the world table too
			// (without this, the entries linger as ghosts — "the bag swallowed
			// its contents / the dog food came back as a separate item"). The
			// landed check is Indeterminate (report still goes out), not
			// Rejected: a pickup ending with the item STILL a world item is
			// suspicious, but PickUpItem's landing variants are not fully
			// enumerated — observe first, tighten later.
			var status = ItemWorldSync.IsWorldItem(item) ? CommitStatus.Indeterminate : CommitStatus.Committed;
			_reports.CommitReport(idComp.Id, op, "OnItemPickedUp", status,
				() =>
				{
					var msgs = 0;
					var container = item.GetComponent<Container>();
					if (container != null)
					{
						for (var i = 0; i < container.transform.childCount; i++)
						{
							var child = container.transform.GetChild(i).GetComponent<Item>();
							var childId = child != null ? child.GetComponent<ItemInstanceId>() : null; // Unity objects — ==
							if (childId != null && childId.Id != 0)
							{
								// Each carried content leaves the world too — its
								// digest rides the report (the host checks the
								// contents it claimed against its own entry).
								_items.SendItemPickedUp(childId.Id, ItemStateCodec.CaptureDigest(child!)); // childId non-null ⇒ child non-null
								msgs++;
							}
						}
					}

					// The slot rides the evidence (a carried item's slot is the
					// owner's local fact — the host adopts it into the transfer
					// table; the reconnect restore needs a real slot or the
					// item would not restore).
					_items.SendItemPickedUp(idComp.Id, ItemStateCodec.CaptureDigest(item, SlotIndexOf(item)));
					return msgs + 1;
				},
				"Pickup");
		}
		else
		{
			// A GENERATION-TIME item (no id — world-gen determinism covers it)
			// left the world through a pickup/wear: the peer's scene holds the
			// same generation-time object, and without an id it can neither bind
			// nor delete it — "the worn item still lies on the ground on the
			// peer's side". Allocate the id and report spawn-then-pickup: the
			// spawn binds the peer's same-spot object (SpawnWorldItem →
			// FindExistingAt), the pickup removes it (OnRemoteItemPickedUp).
			var id = _ids.EnsureId(item);
			if (id != 0)
			{
				var startPos = _lastPickupStart is { } start && start.Item == item // Unity object — ==
					? start.Pos
					: (Vector2)item.transform.position;
				_reports.CommitReport(id, op, "OnItemPickedUp", CommitStatus.Committed,
					() =>
					{
						_items.SendItemSpawned(id, ItemStateCodec.CaptureItem(item, -1),
							new NetVector2(startPos.x, startPos.y), new NetVector2(0f, 0f),
							item.transform.eulerAngles.z, false, 0f);
						_items.SendItemPickedUp(id, ItemStateCodec.CaptureDigest(item, SlotIndexOf(item)));
						return 2;
					},
					"Pickup", "GenerationItem");
			}
			else
			{
				_trace.End(op, 0, "OnItemPickedUp", "Skipped", "NoId");
			}
		}
	}
}
