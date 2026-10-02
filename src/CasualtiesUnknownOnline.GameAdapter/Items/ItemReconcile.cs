using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Tutorial;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// World-item reconciliation: the authoritative snapshot (periodic keyframe /
/// world entry) aligned against the local scene — kill the stale, materialize
/// the missing, re-align drifted condition. Split out of ItemApplication
/// (gate-driven — the application class kept growing with every remote
/// message shape). The materialization primitives live in
/// <see cref="ItemApplication"/>; this class owns the reconcile-only logic.
/// </summary>
internal sealed class ItemReconcile(
	IItemControl items,
	ItemApplication itemApplication,
	DropProtectionGuard guard,
	ILogger<ItemReconcile> log)
{
	private readonly IItemControl _items = items;
	private readonly ItemApplication _app = itemApplication;
	private readonly DropProtectionGuard _guard = guard;
	private readonly ILogger<ItemReconcile> _log = log;

	/// <summary>The frame the late-local sweep last ran in: UnityEngine.Object.Destroy lands at end of frame, so two applies inside one frame would otherwise see — and count — the same objects twice (batch 20261002-j's review, minor-2).</summary>
	private int _straySweepFrame = -1;

	internal void BindToSession() => _items.ItemSnapshotReceived += OnRemoteItemSnapshot;

	internal void Unbind() => _items.ItemSnapshotReceived -= OnRemoteItemSnapshot;

	/// <summary>
	/// The authoritative world-item snapshot arrived (world entry): reconcile —
	/// destroy local world items missing from the snapshot, materialize the
	/// snapshot's items (world first, then container contents — the parent
	/// objects must exist).
	/// Runs inside a RemoteApply scope like every remote application — the
	/// parity is neutral by design (KillRemoteItem zeroes ids and SpawnWorldItem
	/// attaches them before Item.Start runs, so the local-report hooks observe
	/// the same things with or without the scope), and it makes "every remote
	/// mutation carries its call identity" an invariant rather than a habit.
	/// A snapshot is applied only while this side has a LIVE world: in the menu
	/// (or mid-generation) the rows would materialize the table into a scene that
	/// cannot host it — the next keyframe re-delivers once the world is live
	/// (batch 20261002-h).
	/// </summary>
	// The layer modifier rides the snapshot — LayerModifierSync applies it
	// (its own subscription); this domain only consumes the item entries.
	private void OnRemoteItemSnapshot(IReadOnlyList<WorldItem> items, int layerModifierIndex, byte[]? layerModifierRandomState)
	{
		if (!HarmonyTraverse.HasLiveWorld)
		{
			_log.LogDebug("[Reconcile] {Count} snapshot row(s) ignored: no live world on this side.", items.Count);
			return;
		}

		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var killed = 0;
			var spawned = 0;
			var deferredNow = 0;
			var stray = 0;
			var snapshot = items.ToDictionary(w => w.ItemId);

			foreach (var item in Item.allItems.ToList()) // copy: destroying while iterating
			{
				var idComp = item.GetComponent<ItemInstanceId>();
				// STANDALONE, not just world: a container's contents (a bag's
				// carried items) have an id but NO independent table entry — the
				// entry travels INSIDE the container's Contents. With IsWorldItem
				// here the keyframe killed them as stale ("put an item in the
				// legpouch, dropped it — the host sees it inside, the guest's
				// copy is empty"), which also later fed the "equip the empty
				// pouch → the item is swallowed" chain (the host's container
				// copy with the real contents gets deleted by the pickup).
				// Inventory items are character data (IsStandaloneWorldItem is
				// false on the Body chain).
				if (idComp == null || !ItemWorldSync.IsStandaloneWorldItem(item)) // Unity object — ==
				{
					continue;
				}

				if (!snapshot.ContainsKey(idComp.Id))
				{
					// Snapshot-race guard: a fresh local drop registered AFTER the
					// keyframe was generated is not in it yet — killing it would
					// loop (destroy → ItemDestroy report → the host deletes the
					// table entry → the next keyframe misses it → reconcile kills
					// it again, forever).
					if (_guard.IsProtected(idComp.Id))
					{
						continue;
					}

					_app.KillRemoteItem(item);
					_guard.Remove(idComp.Id);
					killed++;
				}
			}

			// Late locals: this side's own generation can still land objects AFTER the table was
			// applied (batch 20261002-j's 5 fps stress run left 67 id-less world items beside
			// their materialized rows — the deferred-landing grace could not reach them). An
			// id-less standalone world item on a live, finished world is either one of those
			// late locals (its row was already materialized, or will be re-delivered) or a local
			// object the authority's table does not know — the same rule
			// GeneratedItemReconcile.Apply runs at apply time, here on the keyframe's cadence so
			// convergence cannot strand late arrivals. Tutorial props stay: they are deliberately
			// id-less until picked up (ItemWorldSync.OnItemInstantiated). A row still waiting in the
			// deferred landing is skipped — its object IS the adopt target (review, major-1: the
			// sweep used to run first and ate the six objects the deferred rows were waiting for,
			// which is why `adopted` stayed zero in every staging run).
			if (_straySweepFrame != Time.frameCount)
			{
				_straySweepFrame = Time.frameCount;
				foreach (var item in Item.allItems.ToList())
				{
					if (item.GetComponent<ItemInstanceId>() != null) // Unity object — ==
					{
						continue;
					}

					if (!ItemWorldSync.IsStandaloneWorldItem(item) || item.GetComponent<TutorialClawProp>() != null) // Unity objects — ==
					{
						continue;
					}

					if (_app.IsWaitingForDeferred(item))
					{
						continue;
					}

					_app.KillRemoteItem(item);
					_log.LogInformation("[Reconcile] dropped late id-less {Type} at ({X:F1},{Y:F1}) — no authority row and no deferred landing holds it.",
						item.id, item.transform.position.x, item.transform.position.y);
					stray++;
				}
			}

			// State alignment: decay (Item.HandleDecay) runs per side on
			// Time.deltaTime — with the generation-time guard the sides start
			// decaying together, but edge conditions (an item wet on one side at
			// a liquid-block boundary, a Geiger counter toggled on one side) can
			// still drift the rate. The keyframe re-aligns the condition (the
			// host refreshed the table's condition right before sending).
			// Battery charge decays INTO the condition (BatteryItem.DrainCharge,
			// BatteryItem.cs:136) — a placed device's power drain is covered by
			// this same alignment. POSITION stays owned by the position stream —
			// never placed here.
			// The same keyframe now also re-aligns the top-level item state that
			// is not covered by a dedicated event: favourited, liquid stacks and
			// [Saveable] component states (flashlight mode, gun state, custom
			// behaviours). Before this, component/liquid state of an existing
			// world item only advanced at its last report/correction time, so a
			// dropped event was not self-healed. Contents are intentionally left
			// to the content/container message family — this is the periodic
			// top-level self-heal, not a full recursive reconcile.
			var aligned = 0;
			foreach (var item in Item.allItems)
			{
				var idComp = item.GetComponent<ItemInstanceId>();
				if (idComp == null || !ItemWorldSync.IsStandaloneWorldItem(item)) // Unity object — ==
				{
					continue;
				}

				if (!snapshot.TryGetValue(idComp.Id, out var w))
				{
					continue;
				}

				// CaptureDigest is the cheap top-level surface (no recursive
				// contents); TopLevelMatches ignores the content ids.
				if (!ItemStateEquality.TopLevelMatches(ItemStateCodec.CaptureDigest(item), w.Item, 0.0005f))
				{
					item.condition = w.Item.Condition;
					item.favourited = w.Item.Favourited;
					ItemStateCodec.RestoreLiquids(item, w.Item.Liquids);
					ItemStateCodec.RestoreComponentStates(item, w.Item.Components);
					aligned++;
				}
			}

			if (aligned > 0)
			{
				_log.LogInformation("[Reconcile] aligned condition of {Aligned} items.", aligned);
			}

			// POSITION is aligned continuously by the 10 Hz position stream (every
			// item, sleeping included) — the reconcile does NOT place anything:
			// a 5 s direct placement after the stream already lerped the copy there
			// would be a jump, and if the copy drifted again it would be yanked
			// back every keyframe ("bounces back every few seconds"). Only the
			// missing ones are materialized here (the snapshot-race window).
			foreach (var w in items.Where(w => w.ParentItemId == 0))
			{
				Land(w, ref spawned, ref deferredNow);
			}

			foreach (var w in items.Where(w => w.ParentItemId != 0))
			{
				Land(w, ref spawned, ref deferredNow);
			}

			if (killed > 0 || spawned > 0 || stray > 0 || deferredNow > 0)
			{
				_log.LogInformation("[Reconcile] {Count} items: killed {Killed}, spawned {Spawned}, deferred {Deferred}, dropped {Stray} late id-less world item(s).",
					items.Count, killed, spawned, deferredNow, stray);
			}
		}
	}

	/// <summary>
	/// Hand one missing row to the landing seam and count what actually happened: an id present
	/// afterwards is landed (adopted or materialized); one still missing was DEFERRED by the landing
	/// grace — or refused with no world — and must not be reported as a spawn (batch 20261002-j's
	/// review, major-4: the count used to be taken from the call, not from the result, so a deferred
	/// row read as materialized in the acceptance evidence).
	/// </summary>
	private void Land(WorldItem w, ref int spawned, ref int deferred)
	{
		if (ItemApplication.FindWorldItem(w.ItemId) != null) // Unity object — ==
		{
			return;
		}

		_app.SpawnWorldItem(w);
		if (ItemApplication.FindWorldItem(w.ItemId) == null) // Unity object — ==
		{
			deferred++;
			return;
		}

		spawned++;
	}
}
