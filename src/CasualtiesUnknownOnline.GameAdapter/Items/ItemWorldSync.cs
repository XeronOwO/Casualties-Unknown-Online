using CasualtiesUnknownOnline.Runtime.Protocol;
using CommitStatus = CasualtiesUnknownOnline.GameAdapter.Items.ItemReportCommitter.CommitStatus;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// World-item report side: every way an item enters or leaves the world domain
/// (drops, throws, container loads/unloads, pickups, destruction) funnels into
/// one report here — instance-id allocation, the merged one-report-per-drop
/// initial vectors and the snapshot-race protection marking. Local compute →
/// report → host relay/arbitration; the drop state machine and report commit
/// live in <see cref="ItemDropState"/> / <see cref="ItemReportCommitter"/>, the
/// materialization side in <see cref="ItemApplication"/>.
/// </summary>
internal sealed class ItemWorldSync(
	SessionService session,
	ItemService items,
	DropProtectionGuard guard,
	ItemDropState dropState,
	BlockBreakPendingState breakState,
	OperationTrace trace,
	ItemReportCommitter reports,
	ItemIdAllocator ids,
	ILogger<ItemWorldSync> log)
{
	private readonly SessionService _session = session;
	private readonly ItemService _items = items;
	private readonly DropProtectionGuard _guard = guard;
	private readonly ItemDropState _dropState = dropState;
	private readonly BlockBreakPendingState _breakState = breakState;
	private readonly OperationTrace _trace = trace;
	private readonly ItemReportCommitter _reports = reports;
	private readonly ItemIdAllocator _ids = ids;
	private readonly ILogger<ItemWorldSync> _log = log;

	/// <summary>True while a remote message is being applied — the local-report hooks must stay silent (call identity lives in CallContext, not a bool).</summary>
	private bool IsRemoteApply => CallContext.Current == CallContext.Origin.RemoteApply;

	internal void BindToSession()
	{
		_items.ItemSpawned += OnRemoteItemBecameWorld;
		_items.ItemDropped += OnRemoteItemBecameWorld;
	}

	internal void Unbind()
	{
		_items.ItemSpawned -= OnRemoteItemBecameWorld;
		_items.ItemDropped -= OnRemoteItemBecameWorld;
	}

	/// <summary>An item was materialized from a remote message (spawn/drop broadcast) — same snapshot-race protection as a local drop.</summary>
	private void OnRemoteItemBecameWorld(WorldItem item) => _guard.Mark(item.ItemId);

	private void OnRemoteItemBecameWorld(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, float angularVelocity, NetVector2 parentPos) => _guard.Mark(itemId);

	/// <summary>Instance-id allocation (ids are (counter, account id) — see ItemIdAllocator).</summary>
	internal ulong EnsureItemId(Item item) => _ids.EnsureId(item);

	/// <summary>True when the item's parent chain ends outside any inventory/body — it is part of the world.</summary>
	internal static bool IsWorldItem(Item item)
	{
		var t = item.transform;
		while (t != null)
		{
			// == null on Unity objects (a scene-reload-destroyed parent is not managed-null)
			// Limb: worn items are parented to the limb (WearWearable, Body.cs:1508)
			// — they are character state, not world items.
			if (t.GetComponent<InventorySlot>() != null || t.GetComponent<Body>() != null || t.GetComponent<Limb>() != null)
			{
				return false;
			}

			t = t.parent;
		}

		return true;
	}

	/// <summary>
	/// True for a world item that is NOT inside another container — the
	/// standalone-item collectors (spawn reports, position stream, keyframe)
	/// must never treat container contents as independent items: a world bag's
	/// contents travel WITH the bag (Contents travel inside the bag's carried
	/// state), and a standalone duplicate would materialize on the peer and sit
	/// stuck in the same spot ("a bag with dog food dropped — two items stuck
	/// together"). The Container check rides the PARENT chain, starting above
	/// the item itself — a bag IS a container, its own Container component must
	/// not exclude it from the position stream, or the host never streams it
	/// and the peer's copy free-simulates on its own physics ("dropping a bag
	/// from the mouth — immediately desynced").
	/// </summary>
	internal static bool IsStandaloneWorldItem(Item item)
	{
		if (!IsWorldItem(item))
		{
			return false;
		}

		for (var t = item.transform.parent; t != null; t = t.parent)
		{
			if (t.GetComponent<Container>() != null) // Unity object — ==
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Called from the Item.Start patch after a runtime-generated item appeared
	/// (drops, creature loot, use-spawned items — every instantiation lands
	/// here). Generation-time items are skipped (world-gen determinism covers
	/// them); everything else gets an instance id and is reported. Solo play
	/// records too (no broadcast) — a solo-turned-lobby host hands its
	/// accumulated items to a joining guest via the snapshot.
	/// A BLOCK DROP (DropOrigin marker — created inside a local DamageBlock
	/// roll) is not reported standalone: it folds into the pending break report
	/// (one message, one verdict — the break's drops travel inside
	/// BlockDamagedMsg). The position is the marker's spawn value — a DETERMINED
	/// position (the Create call's argument), where physics may have bounced the
	/// transform by now.
	/// An item already inside an inventory/container when Start runs is NOT a
	/// world item: the game instantiates and picks up in the same frame (the
	/// starting supplies, WorldGeneration.cs:1904-1912) and MonoBehaviour.Start
	/// fires on the NEXT frame — after generation, so the IsGenerating guard
	/// alone would misclassify them as runtime spawns and duplicate them.
	/// </summary>
	internal void OnItemInstantiated(Item item)
	{
		if (IsRemoteApply || HarmonyTraverse.IsGenerating() || !IsStandaloneWorldItem(item))
		{
			return;
		}

		var op = _trace.NextOperationId();
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null) // Unity object — ==; remote application attached it first — already synced
		{
			_trace.End(op, OperationTrace.IdOf(item), "OnItemInstantiated", "Skipped", "AlreadySynced");
			return;
		}

		var itemId = _ids.Allocate(item);
		// The glowing floating pickup effect carries over (the proximity check
		// already ran on the attacker's side — the component is the truth).
		var fresh = item.GetComponent<FreshItemDrop>() != null; // Unity object — ==
		var dropOrigin = item.GetComponent<DropOrigin>(); // Unity object — ==
		if (dropOrigin != null) // Unity object — ==
		{
			// A block drop: fold into the pending break report (the break postfix
			// held it this frame; the flush at end of NEXT frame sends both).
			if (_breakState.TryAddDrop(new BlockDropEntryMsg
			{
				ItemId = itemId,
				Item = ItemStateCodec.CaptureItem(item, -1),
				Position = new NetVector2(dropOrigin.SpawnPosition.x, dropOrigin.SpawnPosition.y).ToNetVector2Msg(),
				Velocity = new NetVector2(item.rb.velocity.x, item.rb.velocity.y).ToNetVector2Msg(),
				Rotation = item.transform.eulerAngles.z,
				FreshItemDrop = fresh,
				AngularVelocity = item.rb.angularVelocity,
			}))
			{
				_trace.End(op, itemId, "OnItemInstantiated", "Committed", "DropCaptured");
				return;
			}

			// No break pending (the world left / session ended between the break
			// and this frame) — fall through to the standalone report; the item
			// still enters the domain, the session gate handles the rest.
		}

		_log.LogInformation("[ItemSpawned] local {Type} (id {ItemId}) reported at ({X:F1},{Y:F1}), vel ({VX:F1},{VY:F1}), fresh {Fresh}.",
			item.id, itemId, item.transform.position.x, item.transform.position.y,
			item.rb.velocity.x, item.rb.velocity.y, fresh);
		_reports.CommitReport(itemId, op, "OnItemInstantiated", CommitStatus.Committed,
			() =>
			{
				_items.SendItemSpawned(itemId, ItemStateCodec.CaptureItem(item, -1),
					new NetVector2(item.transform.position.x, item.transform.position.y),
					new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
					item.transform.eulerAngles.z,
					fresh, item.rb.angularVelocity);
				return 1;
			},
			"Instantiated");
	}

	internal void OnItemDestroyed(Item item)
	{
		if (IsRemoteApply || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		// A pending drop of a DESTROYED item is cancelled — it used to linger
		// until the next drop overwrote it (a permanent begin-without-end).
		if (_dropState.TryCancel(item, out var cancelledOp))
		{
			_trace.End(cancelledOp, OperationTrace.IdOf(item), "OnItemDestroyed", "Cancelled", "Destroyed");
		}

		var op = _trace.NextOperationId();
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null && idComp.Id != 0) // Unity object — ==; remote deletions zero the id (see KillRemoteItem)
		{
			_reports.CommitReport(idComp.Id, op, "OnItemDestroyed", CommitStatus.Committed,
				() =>
				{
					_items.SendItemDestroyed(idComp.Id);
					return 1;
				},
				"Destroyed");
		}
		// else: a domain-less item (generation-time, restored, decayed) — its
		// destruction is an ordinary event, not a player operation; no trace.
		// (The old "Skipped/NoId" line flooded the log on every corpse-loot /
		// clearing destroy.)
	}

	/// <summary>The world was left (scene switch / session end) — a pending drop cannot resolve anymore; cancel it so the operation trace stays balanced.</summary>
	internal void ResetPending()
	{
		if (_dropState.TryReset(out var op))
		{
			_trace.End(op, 0, "ResetPending", "Cancelled", "WorldLeft");
		}
	}

	internal void OnItemDropped(Item item)
	{
		if (IsRemoteApply || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		var itemId = EnsureItemId(item);
		if (itemId == 0)
		{
			return;
		}

		var op = _trace.NextOperationId();

		if (_session.Role == SessionRole.Guest)
		{
			_guard.Mark(itemId); // the frozen copy must not be killed by the reconcile before the host's stream takes over

			// Freeze the item at the drop spot — NO local simulation until the
			// host's stream arrives (ItemPositionFollow switches it to local
			// physics on its first tick). Without the freeze the copy diverges
			// from the host's trajectory: the report travels for the latency,
			// the host starts simulating from the drop spot, and a local copy
			// that already rolled gets yanked back THROUGH the wall ("thrown
			// item through the wall"). The frozen item plays the host's
			// simulation from the same spot — same phase, no rewind. (The
			// landmine argument from the earlier kinematic design is gone: the
			// layer isolation + MineScriptPatches shield items from tripping
			// mines, so local physics in the playback phase is safe.) The throw
			// report reads rb.velocity AFTER ThrowItem ran; the kinematic body
			// keeps the assigned property values. DropItem does not move the
			// item (Body.cs:1441-1451), so the drop spot IS the reported
			// position.
			item.rb.bodyType = RigidbodyType2D.Kinematic;
			item.rb.velocity = Vector2.zero;
			item.rb.angularVelocity = 0f;
		}

		if (_dropState.Current == ItemDropState.Phase.Dropped && !_dropState.IsPendingFor(item)) // two drops in one frame (rare) — flush the first first
		{
			FlushPendingDrop();
		}

		_trace.Begin(op, itemId, "OnItemDropped", "Drop");
		_dropState.EnterDrop(itemId, item, (Vector2)item.transform.position, op); // the throw velocity lands a moment later (ThrowItem) — merge into one report
	}

	internal void OnItemThrown(Item item)
	{
		if (IsRemoteApply || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		var itemId = EnsureItemId(item);
		if (itemId == 0)
		{
			return;
		}

		if (_session.Role == SessionRole.Guest)
		{
			_guard.Mark(itemId);
		}

		if (_dropState.TryConsumeByThrow(item, out var thrown))
		{
			// Landed check: a throw with the item NOT a standalone world item
			// (re-attached mid-flight — an inventory-internal sequence) never
			// left the world; reporting it would materialize a phantom drop.
			var status = IsStandaloneWorldItem(item) ? CommitStatus.Committed : CommitStatus.Rejected;
			_reports.CommitReport(itemId, thrown.Op, "OnItemThrown", status,
				() =>
				{
					_reports.SendDropReport(itemId, item, thrown.Pos);
					return 1;
				},
				"Drop", "Throw");
			return;
		}

		// No matching pending drop — the pump already flushed it on a previous
		// frame (a cross-frame DropItem → ThrowItem, rare) or a hook-order
		// anomaly. Report alone anyway, or the item never enters the domain;
		// the host's re-place covers the rare double report.
		var op = _trace.NextOperationId();
		var throwStatus = IsStandaloneWorldItem(item) ? CommitStatus.Committed : CommitStatus.Rejected;
		_reports.CommitReport(itemId, op, "OnItemThrown", throwStatus,
			() =>
			{
				_reports.SendDropReport(itemId, item, (Vector2)item.transform.position);
				return 1;
			},
			"Throw");
	}

	/// <summary>Next frame: a drop that was not thrown (a plain drop, velocity ~0)
	/// reports now — the one-frame wait lets the game's DropItem → ThrowItem
	/// sequence set the FINAL velocity (a zero-velocity report materialized a
	/// ghost on the host, "dropped — immediately desynced"). An item that
	/// meanwhile left the world does not re-report.</summary>
	internal void FlushPendingDrop()
	{
		if (!_dropState.TryFlush(out var flushed))
		{
			return; // no pending drop, or still waiting — the throw velocity may still land this frame, or the item is destroyed / still attached to the body (drag-to-hand re-pick). A pending drop that stays pending (destroyed, never freed, world left) shows up as a begin-without-end in the item trace — the baseline asserts on that leak.
		}

		// TryFlush already verified the item is a standalone world item — the
		// commit is Committed by construction.
		var itemId = EnsureItemId(flushed.Item);
		_reports.CommitReport(itemId, flushed.Op, "FlushPendingDrop", CommitStatus.Committed,
			() =>
			{
				_reports.SendDropReport(itemId, flushed.Item, flushed.Pos);
				return 1;
			},
			"Drop", "Flush");
	}

}
