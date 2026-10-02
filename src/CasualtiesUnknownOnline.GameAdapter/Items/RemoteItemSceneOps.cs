using System.Collections;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Tutorial;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Logger = Microsoft.Extensions.Logging.ILogger;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Remote world-item scene operations: materialize, bind, kill and look up
/// remote world-item copies. Split out of <see cref="ItemApplication"/> when
/// the remote-application coordinator reached the architecture line gate; the
/// operations own the same-frame materialization guard and the scene scan
/// primitives while <see cref="ItemApplication"/> keeps the message-handler
/// routing.
/// </summary>
internal sealed class RemoteItemSceneOps(ISessionControl session, Logger log)
{
	private readonly ISessionControl _session = session;
	private readonly Logger _log = log;
	private readonly Dictionary<Item, int> _materializedFrame = [];

	/// <summary>How close a generation-time object must sit to an authority row's reported position to count as that row's copy. Shared with the deferred landing's "is a row waiting for this object" query, so the sweep and the adopt pass can never disagree about which object a row wants.</summary>
	internal const float AdoptTolerance = 1.5f;

	/// <summary><see cref="AdoptTolerance"/> squared — the deferred landing compares squared distances.</summary>
	internal const float AdoptToleranceSquared = AdoptTolerance * AdoptTolerance;

	/// <summary>The deferred-landing strategy for rows whose generation-time object had not landed yet; created lazily because its delegates point back at this instance's scene primitives.</summary>
	private DeferredWorldItemLanding? _landing;

	private DeferredWorldItemLanding Landing => _landing ??= new DeferredWorldItemLanding(
		TryBindExistingAt,
		itemId => FindWorldItem(itemId) != null, // Unity object — ==
		MaterializeWorldItem,
		_log);

	/// <summary>Remove an item object as a REMOTE application: zero its instance id(s)
	/// immediately, then Destroy. UnityEngine.Object.Destroy is deferred to
	/// end-of-frame, so the OnDestroy hook fires AFTER the reentry guard has
	/// been restored — without zeroing the ids first, every remote deletion
	/// would echo back as a local destroy report and kill the peer's own copy
	/// (observed: picking up an item destroyed it on the picker's side too).
	/// The whole SUBTREE is zeroed, contents included: a destroyed container
	/// takes its contents with it (Unity destroys children with the parent),
	/// and a child's id still set would report ITS destruction the same way —
	/// the picker's just-picked-up content vanished right after the pickup
	/// (the deferred destroy of a remote-killed bag re-reported each carried
	/// item as locally destroyed; the peer then deleted the content it had
	/// just received).</summary>
	internal void KillRemoteItem(Item item)
	{
		// A kill in the same frame as the materialization destroys before
		// Start ran (observed: FreshItemDrop.OnDestroy NRE on the guest — the
		// outline is created in Start). Defer one frame; the next frame the
		// id is zeroed and the object destroyed with its components complete.
		if (_materializedFrame.TryGetValue(item, out var frame) && frame == Time.frameCount)
		{
			item.StartCoroutine(KillNextFrame(item));
			return;
		}

		KillNow(item);
	}

	private void KillNow(Item item)
	{
		_materializedFrame.Remove(item);
		foreach (var child in item.GetComponentsInChildren<Item>(true)) // the root is included — zeroing twice is harmless
		{
			var childId = child.GetComponent<ItemInstanceId>();
			if (childId != null) // Unity object — ==
			{
				childId.Id = 0;
			}
		}

		Object.Destroy(item.gameObject);
	}

	private IEnumerator KillNextFrame(Item item)
	{
		yield return null;
		if (item != null) // Unity object — ==
		{
			KillNow(item);
		}
	}

	/// <summary>Find an item by its instance id. Item.allItems registers in Item.Start
	/// (Item.cs:118) — ONE frame after Instantiate — so a message arriving in
	/// the same frame as a materialization misses the table (observed: a pickup
	/// relay 3 ms after the drop left the world phantom forever — "moving an
	/// item makes a duplicate drop on the peer"). Fall back to a scene scan
	/// (slow, but only on the miss path) to cover the not-yet-registered window.</summary>
	internal static Item? FindWorldItem(ulong itemId)
	{
		// O(1) fast path: the ItemInstanceId index is maintained when ids are
		// stamped/zeroed/destroyed, so the guest's per-frame item-follow pump
		// no longer scans every item in the scene for every followed id.
		if (ItemInstanceId.TryFindItem(itemId, out var indexed)
			&& indexed.GetComponentInParent<RemoteCloneRender>() == null) // Unity object — ==
		{
			return indexed;
		}

		// Fallback for any historical/edge path where an id-bearing object was
		// not registered (or the indexed hit was a display proxy). This is not
		// on the steady-state hot path any more.
		foreach (var item in Item.allItems)
		{
			// Remote clone inventory items are display proxies, not domain
			// objects. Even if an id leaks into a proxy, domain application
			// must never address the proxy (a drop/correction would unparent it
			// from the clone and produce a ghost world item).
			if (item.GetComponentInParent<RemoteCloneRender>() != null) // Unity object — ==
			{
				continue;
			}

			var idComp = item.GetComponent<ItemInstanceId>();
			if (idComp != null && idComp.Id == itemId) // Unity object — ==
			{
				return item;
			}
		}

		foreach (var item in Object.FindObjectsOfType<Item>())
		{
			if (item.GetComponentInParent<RemoteCloneRender>() != null) // Unity object — ==
			{
				continue;
			}

			var idComp = item.GetComponent<ItemInstanceId>();
			if (idComp != null && idComp.Id == itemId) // Unity object — ==
			{
				return item;
			}
		}

		return null;
	}

	/// <summary>Find a generation-time (id-less) item of the same definition near Pos —
	/// the materialization bind target. Only items outside any inventory count
	/// (world-gen determinism put them there on every side).</summary>
	internal static Item? FindExistingAt(NetVector2 pos, string itemId)
	{
		var target = new Vector2(pos.X, pos.Y);
		foreach (var item in Item.allItems)
		{
			// A remote clone proxy is never a world-generation bind target.
			if (item.GetComponentInParent<RemoteCloneRender>() != null) // Unity object — ==
			{
				continue;
			}

			if (item.id != itemId || !ItemWorldSync.IsWorldItem(item)) // Unity object — ==
			{
				continue;
			}

			// A per-player tutorial prop is never a bind target — binding a
			// shared item to it would let one player's pickup remove another
			// player's private course object (the claw double-give fix must
			// not become a cross-player course stall).
			if (item.GetComponent<TutorialClawProp>() != null) // Unity object — ==
			{
				continue;
			}

			if (item.GetComponent<ItemInstanceId>() != null) // Unity object — ==; already an item-domain object
			{
				continue;
			}

			if (Vector2.Distance(item.transform.position, target) > AdoptTolerance)
			{
				continue;
			}

			return item;
		}

		return null;
	}

	/// <summary>Place an item inside its world container: the container's instance id
	/// (parentItemId) was allocated by the ORIGINATOR (possibly a generation-time
	/// container on first use — trash bags have no id until then), so the local
	/// container may not have it yet — bind it by the carried position, mirroring
	/// the game's LoadItem semantics (position + physics off + visibility).</summary>
	internal void BindToContainer(Item item, ulong parentItemId, NetVector2 parentPos)
	{
		var parent = FindWorldItem(parentItemId);
		if (parent != null && parent.GetComponent<Container>() != null) // Unity objects — ==
		{
			_log.LogInformation("[ItemBind] container {ContainerId} found — loading {Type} into it.", parentItemId, item.id);
			parent.GetComponent<Container>()?.LoadItem(item); // the game's own load semantics (position, physics, visibility)
			return;
		}

		// Generation-time container not bound yet — find it by position and
		// stamp the originator's id onto it (idempotent: already bound to a
		// different id → not ours, keep looking). The position tolerance is
		// generous: the container may have been nudged by physics since the
		// report; a lone unbound container of the same type is accepted as a
		// fallback.
		var candidates = Object.FindObjectsOfType<Container>();
		foreach (var container in candidates)
		{
			var containerItem = container.GetComponent<Item>();
			if (containerItem == null) // Unity object — ==
			{
				continue;
			}

			if (Vector2.Distance(container.transform.position, new Vector2(parentPos.X, parentPos.Y)) > 3f)
			{
				continue;
			}

			var idComp = containerItem.GetComponent<ItemInstanceId>();
			if (idComp != null && idComp.Id != parentItemId) // Unity object — ==; bound to a different container
			{
				continue;
			}

			if (idComp == null) // Unity object — ==
			{
				idComp = containerItem.gameObject.AddComponent<ItemInstanceId>();
				idComp.Id = parentItemId;
			}

			_log.LogInformation("[ItemBind] container {ContainerId} bound by position ({X:F1},{Y:F1}) — loading {Type} into it.",
				parentItemId, parentPos.X, parentPos.Y, item.id);
			container.LoadItem(item);
			return;
		}

		// Fallback: a lone unbound container of the same definition anywhere —
		// the position report may be stale (the container moved after the
		// report was sent).
		foreach (var container in candidates)
		{
			var containerItem = container.GetComponent<Item>();
			if (containerItem == null || containerItem.id != item.id) // Unity object — ==
			{
				continue;
			}

			if (containerItem.GetComponent<ItemInstanceId>() != null) // Unity object — ==; already bound
			{
				continue;
			}

			containerItem.gameObject.AddComponent<ItemInstanceId>().Id = parentItemId;
			_log.LogInformation("[ItemBind] container {ContainerId} bound as the lone {Type} (stale position {X:F1},{Y:F1}).",
				parentItemId, item.id, parentPos.X, parentPos.Y);
			container.LoadItem(item);
			return;
		}

		_log.LogWarning("[ItemBind] container {ParentItemId} for {Type} not found at ({X:F1}, {Y:F1}) — item stays where it is.",
			parentItemId, item.id, parentPos.X, parentPos.Y);
	}

	/// <summary>Recursive authoritative-state apply: the top-level fields, then per-content
	/// (recurse into an existing one, materialize a missing one with its id).</summary>
	internal static void ApplyAuthoritativeState(Item target, CharacterItemMsg authoritative)
	{
		target.condition = authoritative.Condition;
		target.favourited = authoritative.Favourited;
		ItemStateCodec.RestoreLiquids(target, authoritative.Liquids);
		ItemStateCodec.RestoreComponentStates(target, authoritative.Components);
		RemoteItemPresentation.ApplyDynamiteFuse(target, authoritative);

		var container = target.GetComponent<Container>();
		if (container == null || authoritative.Contents.Count == 0) // Unity object — ==
		{
			return;
		}

		var children = new Dictionary<ulong, Item>();
		for (var i = 0; i < container.transform.childCount; i++)
		{
			var child = container.transform.GetChild(i).GetComponent<Item>();
			var idComp = child != null ? child.GetComponent<ItemInstanceId>() : null; // Unity objects — ==
			if (idComp != null && idComp.Id != 0)
			{
				children[idComp.Id] = child!; // idComp non-null ⇒ child non-null
			}
		}

		foreach (var childData in authoritative.Contents)
		{
			if (childData.InstanceId != 0 && children.TryGetValue(childData.InstanceId, out var child))
			{
				ApplyAuthoritativeState(child!, childData); // found ⇒ non-null
			}
			else
			{
				ItemStateCodec.RestoreContent(target, container, childData);
			}
		}
	}

	/// <summary>
	/// Materialize a world item from its carried state: instantiate the
	/// definition prefab, restore condition/components/liquids/contents, attach
	/// the instance id and place it (into its container when the parent exists).
	/// The Item.Start hook sees the already-attached id and does not re-report.
	/// </summary>
	internal void SpawnWorldItem(WorldItem w)
	{
		// A remote world item is a world fact: materializing one without a world
		// scene is exactly the menu-scene burst of batch 20261002-h (each copy's
		// Item.Update dereferences WorldGeneration.world every frame). The host
		// also keeps its copy while its own generation runs — there it is the only
		// side that will ever create the item — while the guest refuses that race
		// (the periodic keyframe re-delivers once the world is live).
		if (!HarmonyTraverse.HasWorld)
		{
			_log.LogDebug("[ItemSpawn] ignored {Type} (id {ItemId}): no world scene on this side.", w.Item.ItemId, w.ItemId);
			return;
		}

		if (_session.Role == SessionRole.Guest && HarmonyTraverse.IsGenerating())
		{
			_log.LogDebug("[ItemSpawn] ignored {Type} (id {ItemId}): this side's world is still generating.", w.Item.ItemId, w.ItemId);
			return;
		}

		// Idempotency: if a scene object with this instance id already exists,
		// never materialize a second copy. The most common case is the
		// originator's own local item: the guest reports an ItemSpawn command,
		// the host commits and broadcasts the batch back to every guest
		// (originator included), and without this guard ItemApplication would
		// instantiate a duplicate beside the local original. Duplicate ids are
		// also how "one copy syncs with the host, two extra copies are
		// unsynced/frozen" appears — the position stream can only drive one
		// object, so the extras stay kinematic/twitching forever.
		if (FindWorldItem(w.ItemId) != null) // Unity object — ==
		{
			_log.LogDebug("[ItemSpawn] {Type} (id {ItemId}) already present locally — skipping duplicate materialization.",
				w.Item.ItemId, w.ItemId);
			return;
		}

		// A generation-time object may already exist at this spot (world-gen
		// determinism puts the same objects on every side): bind the instance
		// id to it instead of materializing a duplicate — a second copy would
		// also be killed by the next snapshot reconcile (one table entry, two
		// scene objects) and a generation-time container that was already
		// bound must NOT be re-materialized either ("items overlapping").
		var existing = FindExistingAt(w.Pos, w.Item.ItemId);
		if (existing != null && BindExistingItem(existing, w)) // Unity object — ==
		{
			return;
		}

		// The receiver's own generation can still be registering its last objects when the
		// row arrives (batch 20261002-j: a re-entry's world-entry repair bound 276 of 282
		// rows and materialized 6 whose generation-time object had not landed yet — each of
		// those 6 then appeared as an id-less duplicate beside the materialized copy).
		// Defer the row while the generation is still landing so the object that is about
		// to arrive is ADOPTED instead; Update() materializes it once the grace expires so
		// an authority row can never be lost to a slow local load.
		if (Landing.IsGenerationStillLanding())
		{
			if (Landing.Defer(w))
			{
				_log.LogInformation("[ItemSpawn] deferred {Type} (id {ItemId}) at ({X:F1},{Y:F1}) — the local generation may still be landing its object; it materializes in a couple of seconds if none appears.",
					w.Item.ItemId, w.ItemId, w.Pos.X, w.Pos.Y);
			}

			return;
		}

		MaterializeWorldItem(w);
	}

	/// <summary>Materialize the authority row as a fresh object — the ordinary landing for a row with no local generation-time object to adopt.</summary>
	private void MaterializeWorldItem(WorldItem w)
	{
		_log.LogInformation("[ItemSpawn] materializing {Type} (id {ItemId}) at ({X:F1},{Y:F1}), vel ({VX:F1},{VY:F1}), container {ContainerId}.",
			w.Item.ItemId, w.ItemId, w.Pos.X, w.Pos.Y, w.Vel.X, w.Vel.Y, w.ParentItemId);
		var prefab = ItemPrefabResolver.Load(w.Item.ItemId);
		if (prefab == null) // Unity object — ==
		{
			_log.LogWarning("Cannot materialize item {ItemId}: definition '{Type}' not found.", w.ItemId, w.Item.ItemId);
			return;
		}

		var obj = Object.Instantiate(prefab, new Vector3(w.Pos.X, w.Pos.Y, 0f), Quaternion.Euler(0f, 0f, w.Rotation)) as GameObject;
		if (obj == null) // Unity object — ==
		{
			_log.LogWarning("Cannot materialize item {ItemId}: instantiate returned null.", w.ItemId);
			return;
		}

		obj.SetActive(true); // the cached custom template is inactive; every item instance must be live
		var item = obj.GetComponent<Item>(); // the definition prefab carries Item — Instantiate succeeded, so it exists
		item.condition = w.Item.Condition; // direct write, like the save restore (SaveSystem.cs:306) — SetCondition would drain water by ratio
		item.favourited = w.Item.Favourited;
		item.gameObject.AddComponent<ItemInstanceId>().Id = w.ItemId;
		ItemStateCodec.RestoreLiquids(item, w.Item.Liquids);
		ItemStateCodec.RestoreComponentStates(item, w.Item.Components);
		ItemStateCodec.RestoreContents(item, w.Item.Contents);
		if (w.FreshItemDrop)
		{
			item.gameObject.AddComponent<FreshItemDrop>(); // the glowing floating pickup effect (self-destroys when the setting is off)
		}

		// A same-frame remote kill would destroy before Start ran (the
		// FreshItemDrop NRE) — KillRemoteItem defers one frame via this entry.
		_materializedFrame[item] = Time.frameCount;

		if (w.ParentItemId != 0)
		{
			BindToContainer(item, w.ParentItemId, w.ParentPosition);
		}

		item.rb.velocity = new Vector2(w.Vel.X, w.Vel.Y);
		item.rb.angularVelocity = w.AngularVelocity;
		if (_session.Role == SessionRole.Guest)
		{
			// The guest's world items are frozen (kinematic) from birth — a
			// dynamic materialization would simulate locally until the position
			// stream takes over: the same non-authoritative window as a local
			// roll-out (see ItemWorldSync.OnItemDropped), then a yank-back when
			// the stream arrives. Frozen at the reported spot = the host's
			// simulation start, same phase; ItemPositionFollow switches it to
			// local physics on the first stream tick.
			item.rb.bodyType = RigidbodyType2D.Kinematic;
		}
		else
		{
			// The game's throw sets continuous collision detection (Body.cs:1664) —
			// a Discrete materialization misses fast wall hits and the item
			// tunnels INTO the wall, which the host's position stream then pins
			// on every guest ("thrown item through the wall, stuck inside").
			// Same for a fast roll-out; the extra cost is one item.
			item.rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
		}
	}

	/// <summary>
	/// Adopt an already-present generation-time object as the authority row's copy: stamp the
	/// instance id, restore the originator's state over it (condition/liquids/components AND
	/// contents), align it to the reported transform and place it into its container. Returns
	/// false when the object carries a DIFFERENT non-zero id — it is not this row's copy and the
	/// row must materialize beside it instead. That guard is defensive: both call sites pass a
	/// <see cref="FindExistingAt"/> result, which already excludes any object carrying the
	/// component, so the false branch is unreachable today (batch 20261002-j's review, minor-1).
	/// </summary>
	private bool BindExistingItem(Item existing, WorldItem w)
	{
		var existingId = existing.GetComponent<ItemInstanceId>();
		if (existingId != null && existingId.Id != w.ItemId) // Unity object — ==; bound to a different id
		{
			return false;
		}

		if (existingId == null) // Unity object — ==
		{
			existingId = existing.gameObject.AddComponent<ItemInstanceId>();
			existingId.Id = w.ItemId;
			// Restore = exact rebuild (the bind target is the peer's
			// generation-time object, but its state must become the
			// originator's): condition/liquids/components AND contents.
			// The contents were previously never restored here — a
			// generation-time container that the originator loaded items
			// into and dropped came out EMPTY on the peer ("host put a
			// water bottle in the trash bag, dropped it — the guest
			// picked up an empty bag"). ApplyAuthoritativeState matches
			// contents by id so the container's own generation-time
			// contents are not duplicated.
			ApplyAuthoritativeState(existing, w.Item);
			if (w.FreshItemDrop)
			{
				existing.gameObject.AddComponent<FreshItemDrop>();
			}

			// Align the bound object to the reported state — the
			// generation-time object sits where the world-gen put it,
			// which may differ from the originator's current spot
			// ("item in the wrong place / overlapping" class of bugs).
			existing.transform.position = new Vector3(w.Pos.X, w.Pos.Y, 0f);
			existing.transform.eulerAngles = new Vector3(0f, 0f, w.Rotation);
			existing.rb.velocity = new Vector2(w.Vel.X, w.Vel.Y);
			existing.rb.angularVelocity = w.AngularVelocity;

			_log.LogInformation("[ItemBind] bound existing {Type} at ({X:F1}, {Y:F1}) to id {ItemId} (no materialization).",
				w.Item.ItemId, w.Pos.X, w.Pos.Y, w.ItemId);
		}

		if (w.ParentItemId != 0)
		{
			BindToContainer(existing, w.ParentItemId, w.ParentPosition);
		}

		return true;
	}

	/// <summary>
	/// The adopt half of <see cref="SpawnWorldItem"/>, callable on its own: bind the row to the
	/// generation-time object already at its spot and DO NOT materialize when none is present yet.
	/// A receiver whose own generation is still registering its last objects (corpse loot's Start)
	/// uses this to wait out that window — adopting there instead of materializing is what keeps
	/// one authority row from becoming two physical objects (batch 20261002-i: 257 bound / 9
	/// materialized at a re-entry, the 9 left as id-less locals beside their materialized copies).
	/// False means "nothing to adopt yet"; the caller decides when to stop waiting.
	/// </summary>
	internal bool TryBindExistingAt(WorldItem w)
	{
		if (!HarmonyTraverse.HasWorld)
		{
			return false;
		}

		if (_session.Role == SessionRole.Guest && HarmonyTraverse.IsGenerating())
		{
			return false;
		}

		var existing = FindExistingAt(w.Pos, w.Item.ItemId);
		return existing != null && BindExistingItem(existing, w); // Unity object — ==
	}

	/// <summary>Pump, once per frame: delegate to the deferred-landing strategy — it tracks the generation falling edge and retries the rows whose generation-time object had not landed yet.</summary>
	internal void Update() => Landing.Update();

	/// <summary>Drop the deferred rows: the session ended or the world was replaced — they describe a table the next scene must not materialize (batch 20261002-j's review, major-3).</summary>
	internal void ClearDeferredLanding() => Landing.Clear();

	/// <summary>True when a deferred row is still waiting for this local object — the keyframe's late-local sweep asks before it drops an id-less world item, so the sweep cannot eat a row's own adopt target (batch 20261002-j's review, major-1).</summary>
	internal bool IsWaitingForDeferred(Item item) => Landing.IsWaitingFor(item);

	/// <summary>True when a row for this instance id is still waiting — DEFERRED rather than refused; the apply paths keep their landing counts honest with it (batch 20261002-j's review, major-4).</summary>
	internal bool IsDeferredLandingPending(ulong itemId) => Landing.IsWaitingFor(itemId);
}
