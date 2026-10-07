using System.Collections;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The standing item object's materialize / update / destroy path (ticket
/// <c>mod-cross-player-solid-food-semantics</c>, §6 step 2): every item the authoritative data carries as
/// a member's CARRIED row becomes a local, inert, id-addressable game object on every other client, kept
/// aligned by the same data that created it.
///
/// <para>
/// WHAT DRIVES IT. The carried fact table (<see cref="CloneFactTable"/>) already holds exactly the rows
/// this needs — the 1 Hz snapshot per owner (wholesale and recursive), the single carried fact, the
/// starting-supply merge, the drop — and every one of those edges fires
/// <see cref="CloneFactTable.CloneSnapshotUpdated"/>. This class is that edge's second consumer beside the
/// display-proxy renderer, so the object set cannot drift from the data it mirrors: each update rebuilds
/// the wanted set (<see cref="StandingItemPlan"/>) and reconciles the scene against it.
/// </para>
///
/// <para>
/// HOW IT IS BUILT (the recipe, §6.2/§6.3 — <see cref="StandingItemRecipe"/> owns it). The object is
/// created with its instance id AND the <see cref="StandingItemObject"/> marker attached in the same frame,
/// before <c>Item.Start</c> runs, and with its world-facing surface switched off: <c>rb.simulated = false</c>,
/// every renderer, every collider and every <c>Light2D</c> in the subtree off, and the components that write
/// outside the object or put it back on screen disabled (<c>WaterContainerItem</c> drinks the local fluid,
/// <c>CustomItemBehaviour</c> explodes and spawns entities, <c>LightItem</c> and <c>WatchScript</c> present
/// the copy, <c>EPdaScript</c> re-enables its own glow every frame). It stays an ordinary running item — in
/// <c>Item.allItems</c>, with its whole lifecycle, addressable by id — because that is what the eat needs
/// (§"The design" 4).
/// </para>
///
/// <para>
/// WHERE IT LIVES. Every standing object is parented to ONE plain CUO-owned holder and NOTHING else — not
/// the local body (it would block a slot and enter every recipe sweep), not a clone (that category is the
/// display proxy's), and not a real <c>Container</c>. §6 constraint 5 named the two shapes and this is the
/// second one: the game's own load path cannot build a nested carried tree (<c>Container.LoadItem</c>
/// refuses a child that already holds weight and refuses outright when the receiving container is itself
/// nested — each refusal a player-facing <c>PlayerCamera.main.DoAlert</c>), and hand-attaching children
/// would make <c>Container.itemCount</c> — which is <c>transform.childCount</c> — feed
/// <c>ContainerBroke</c>/<c>UnloadAllItems</c>, turning standing children into live world items. The
/// parent link therefore lives in THIS class (each object records its data parent), and the data, not the
/// scene hierarchy, is the tree's source of truth.
/// </para>
///
/// <para>
/// LIFETIME IS CUO'S, from four signals: the row left the data (retire), the row's id was destroyed
/// (retire now rather than up to a second later), its owner left the world (retire its set), and the
/// session ended (retire everything and drop the holder). An object that outlived the world would
/// dereference <c>WorldGeneration.world</c> in <c>Item.Update</c> every frame — batch <c>20261002-h</c> —
/// which is why materialization is gated on <see cref="HarmonyTraverse.HasWorld"/> as well.
/// </para>
/// </summary>
internal sealed class StandingItemMaterializer
{
	/// <summary>The holder every standing object is parented to. A plain GameObject on purpose: a <c>Container</c> would put the objects into the game's container machinery.</summary>
	private const string HolderName = "CUO Standing Items";

	private readonly ISessionControl _session;
	private readonly IItemControl _items;
	private readonly CloneFactTable _facts;
	private readonly ILogger<StandingItemMaterializer> _log;

	/// <summary>Instance id → the local incarnation of that carried row.</summary>
	private readonly Dictionary<ulong, Incarnation> _objects = [];

	/// <summary>Owner → the item tree the last reconcile saw, by reference (see <see cref="SeenTree"/>): what keeps the fact edge's non-item fires cheap.</summary>
	private readonly Dictionary<ulong, SeenTree> _seen = [];

	/// <summary>Bounds a per-object failure line (a prefab the local scene cannot serve repeats on every snapshot).</summary>
	private readonly LogRepetitionGuard _failures = new(suppressAfter: 3, capacity: 64);

	private GameObject? _holder;

	internal StandingItemMaterializer(
		ISessionControl session,
		IItemControl items,
		CloneFactTable facts,
		ILogger<StandingItemMaterializer> log)
	{
		_session = session;
		_items = items;
		_facts = facts;
		_log = log;
	}

	internal void BindToSession()
	{
		_facts.CloneSnapshotUpdated += OnOwnerFactsChanged;
		_session.RemoteSceneChanged += OnRemoteSceneChanged;
	}

	internal void Unbind()
	{
		_facts.CloneSnapshotUpdated -= OnOwnerFactsChanged;
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
	}

	// ===== the item domain's four questions =====

	/// <summary>
	/// Retire the standing object for an id, with its standing contents, and answer whether there was one.
	/// The data-moved-into-the-world sites ask this instead of asking the CATEGORY: by the time a drop or a
	/// world row arrives the data may already hold the id as a world item, so
	/// <see cref="StandingItems.Is"/> answers false while the object still carries every switch of the
	/// recipe. The marker is what the object was CREATED as, and that is the question these sites have.
	/// </summary>
	internal bool Retire(ulong itemId, string reason)
	{
		if (!_objects.TryGetValue(itemId, out var incarnation) || incarnation.Item == null) // Unity object — ==
		{
			return false;
		}

		RetireObject(itemId, reason);
		return true;
	}

	/// <summary>
	/// Apply one authoritative carried fact to its standing object (§6.2's explicit apply path — the one
	/// <c>ItemApplication.OnItemCorrection</c> already runs for world items, routed here because the
	/// contents of a standing object live in the data and not under a <c>Container</c>). Contents are
	/// applied to rows that already stand; a row the data has not delivered yet is the fact table's to
	/// create, never a correction's — one create path, and it is the plan's.
	/// </summary>
	internal void ApplyCorrection(CharacterItemMsg item)
	{
		if (!_objects.TryGetValue(item.InstanceId, out var incarnation) || incarnation.Item == null) // Unity object — ==
		{
			_log.LogDebug("[StandingItem] correction for {Type} (id {ItemId}) has no standing object — ignored.", item.ItemId, item.InstanceId);
			return;
		}

		ApplyState(incarnation.Item, item);
		ApplyContents(incarnation.Item, item.Contents);
		_log.LogInformation("[StandingItem] applied the authoritative fact to {Type} (id {ItemId}).", item.ItemId, item.InstanceId);
	}

	/// <summary>Session ended (the session binding's teardown, like every other domain's reset): the objects belong to the dead session, and the holder goes with them.</summary>
	internal void ResetSessionState()
	{
		var retired = _objects.Count;
		foreach (var id in new List<ulong>(_objects.Keys))
		{
			RetireObject(id, "the session ended");
		}

		_failures.Clear();
		_seen.Clear();
		if (_holder != null) // Unity object — ==
		{
			Object.Destroy(_holder);
			_holder = null;
		}

		if (retired > 0)
		{
			_log.LogInformation("[StandingItem] session ended: retired {Count} standing object(s).", retired);
		}
	}

	// ===== the data edges =====

	/// <summary>
	/// One owner's carried facts changed: bring the local objects in line with them. An owner outside the
	/// world carries nothing this side must incarnate — its rows are the last thing it reported, and its
	/// set goes with it (the same signal the clone renderer tears its clone down on).
	/// <para>
	/// This edge is not item-only: the fact table also fires it for an enemy bite/lunge/effect, a medical
	/// state and a limb event (eleven fire sites in all), because the clone renderer re-renders on those
	/// too. The reconcile is therefore gated on the item tree itself: a fire that left every list and entry
	/// instance in place is answered by one reference walk (<see cref="SeenTree"/>) and nothing else.
	/// </para>
	/// </summary>
	private void OnOwnerFactsChanged(ulong owner)
	{
		if (!HarmonyTraverse.HasWorld)
		{
			_log.LogDebug("[StandingItem] {Owner}'s carried facts changed with no world scene on this side — nothing materialized.", owner);
			return;
		}

		if (!_session.IsRemoteInWorld(owner))
		{
			RetireOwned(owner, "the owner is not in the world");
			return;
		}

		if (!_facts.CloneData.TryGetValue(owner, out var data))
		{
			_seen.Remove(owner);
			RetireOwned(owner, "the data no longer carries this owner");
			return;
		}

		// The edge is not item-only (enemy bites, medical state and limb events fire it too), so ask the
		// tree by reference first: the fact table rewrites what it changes, and an unchanged tree means the
		// objects are already aligned — no plan, no digest, no retire scan.
		if (_seen.TryGetValue(owner, out var seen) && seen.Matches(data.Items))
		{
			_log.LogDebug("[StandingItem] {Owner}: the fact edge fired with an unchanged item tree — nothing to reconcile.", owner);
			return;
		}

		_seen[owner] = SeenTree.Of(data.Items);
		var plan = StandingItemPlan.Build(owner, data.Items, _items.IsWorldItemRegistered);
		foreach (var id in plan.Ids)
		{
			Ensure(id, plan);
		}

		RetireUnwanted(plan);
		_log.LogDebug("[StandingItem] {Owner}: {Wanted} carried row(s) in the data, {Standing} standing object(s) alive.", owner, plan.Count, _objects.Count);
	}

	private void OnRemoteSceneChanged(ulong steamId, bool inWorld)
	{
		if (!inWorld)
		{
			RetireOwned(steamId, "the owner left the world");
		}
	}

	// ===== reconcile =====

	/// <summary>
	/// One carried row: keep the standing object in line with it, or create it. An id that already has a
	/// local object which is NOT ours is never incarnated a second time — the object is either this side's
	/// own copy of the item (a cross-player transfer in flight, a rollback) or a world item whose row the
	/// data has already moved, and both of those own the id: one id, one domain object, or the
	/// id-addressed apply path answers with whichever object registered last.
	/// </summary>
	private void Ensure(ulong itemId, StandingItemPlan plan)
	{
		var data = plan.Row(itemId);
		var parent = plan.ParentOf(itemId);
		if (_objects.TryGetValue(itemId, out var incarnation))
		{
			if (incarnation.Item != null) // Unity object — ==
			{
				var local = RemoteItemSceneOps.FindWorldItem(itemId);
				if (local != null && local != incarnation.Item) // Unity objects — ==
				{
					_log.LogDebug("[StandingItem] {Type} (id {ItemId}) is this side's own object now ({Other}) — the standing incarnation is retired for it.",
						data.ItemId, itemId, local.id);
					RetireObject(itemId, "this side holds the item itself now");
					return;
				}

				incarnation.ParentId = parent;
				ApplyState(incarnation.Item, data);
				return;
			}

			// A scene change (or a layer teardown) destroyed it under us: the row is still wanted, so it
			// is created below rather than left as a stale record.
			_objects.Remove(itemId);
		}

		if (RemoteItemSceneOps.FindWorldItem(itemId) != null) // Unity object — ==
		{
			_log.LogDebug("[StandingItem] {Type} (id {ItemId}) already has a local object that this side did not create — not incarnated.", data.ItemId, itemId);
			return;
		}

		Create(plan.Owner, itemId, parent, data);
	}

	private void Create(ulong owner, ulong itemId, ulong parentId, CharacterItemMsg data)
	{
		var item = StandingItemRecipe.Build(Holder.transform, itemId, data, out var failure);
		if (item == null) // Unity object — ==
		{
			ReportFailure(data.ItemId, $"[StandingItem] cannot materialize {data.ItemId} (id {itemId}) for {owner}: {failure}.");
			return;
		}

		_objects[itemId] = new Incarnation
		{
			Item = item,
			Owner = owner,
			ParentId = parentId,
			CreatedFrame = Time.frameCount,
		};

		_log.LogInformation("[StandingItem] materialized {Type} (id {ItemId}) for {Owner}, data parent {Parent}, condition {Condition:F2}.",
			data.ItemId, itemId, owner, parentId, data.Condition);
	}

	/// <summary>
	/// Realign one standing object with its row: the object's LIVE state is compared with the data (the
	/// same shape the world reconcile uses for its decay self-heal), and only a difference is written. The
	/// expensive half of that comparison — <see cref="ItemStateCodec.CaptureDigest"/> reads the components
	/// back by reflection — is paid per row only on a fire that actually changed the owner's item tree; the
	/// fact edge fires for enemy bites, medical state and limb events too, and those fires are answered by
	/// one reference walk (<see cref="SeenTree"/>).
	/// </summary>
	private static void ApplyState(Item target, CharacterItemMsg data)
	{
		if (ItemStateEquality.TopLevelMatches(ItemStateCodec.CaptureDigest(target), data, 0.0005f))
		{
			return;
		}

		target.condition = data.Condition;
		target.favourited = data.Favourited;
		ItemStateCodec.RestoreLiquids(target, data.Liquids);
		ItemStateCodec.RestoreComponentStates(target, data.Components);
	}

	private void ApplyContents(Item parent, List<CharacterItemMsg> contents)
	{
		foreach (var child in contents)
		{
			if (child.InstanceId == 0 || !_objects.TryGetValue(child.InstanceId, out var incarnation) || incarnation.Item == null) // Unity object — ==
			{
				continue;
			}

			ApplyState(incarnation.Item, child);
			ApplyContents(incarnation.Item, child.Contents);
		}
	}

	private void RetireUnwanted(StandingItemPlan plan)
	{
		var stale = new List<ulong>();
		foreach (var candidate in _objects)
		{
			if (candidate.Value.Owner == plan.Owner && !plan.Contains(candidate.Key))
			{
				stale.Add(candidate.Key);
			}
		}

		foreach (var id in stale)
		{
			RetireObject(id, "the data no longer carries the row");
		}
	}

	private void RetireOwned(ulong owner, string reason)
	{
		_seen.Remove(owner); // a member that comes back reconciles its tree from scratch
		var owned = new List<ulong>();
		foreach (var candidate in _objects)
		{
			if (candidate.Value.Owner == owner)
			{
				owned.Add(candidate.Key);
			}
		}

		foreach (var id in owned)
		{
			RetireObject(id, reason);
		}
	}

	/// <summary>
	/// Remove one standing object and its standing contents. The id is zeroed BEFORE the destroy so the
	/// world path's own idempotency lookups answer "absent" in this same frame
	/// (<c>FindWorldItem</c>, the adopt scan's "already synced" clause) — that is what lets a drop retire
	/// the carried incarnation and materialize the world copy in the same pass. A row created in THIS frame
	/// is retired on the next one: destroying an object whose <c>Start</c> never ran is the order the item
	/// family has already paid for (the same reason <c>RemoteItemSceneOps.KillRemoteItem</c> defers).
	/// </summary>
	private void RetireObject(ulong itemId, string reason)
	{
		if (!_objects.TryGetValue(itemId, out var incarnation))
		{
			return;
		}

		_objects.Remove(itemId);

		var contents = new List<ulong>();
		foreach (var candidate in _objects)
		{
			if (candidate.Value.ParentId == itemId)
			{
				contents.Add(candidate.Key);
			}
		}

		foreach (var id in contents)
		{
			RetireObject(id, reason);
		}

		var item = incarnation.Item;
		if (item == null) // Unity object — ==; a scene teardown took it, nothing left to retire
		{
			return;
		}

		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null) // Unity object — ==
		{
			idComp.Id = 0;
		}

		var definitionId = item.id;
		_log.LogInformation("[StandingItem] retired {Type} (id {ItemId}, owner {Owner}): {Reason}.", definitionId, itemId, incarnation.Owner, reason);
		if (incarnation.CreatedFrame == Time.frameCount)
		{
			item.StartCoroutine(RetireNextFrame(item));
			return;
		}

		Object.Destroy(item.gameObject);
	}

	private static IEnumerator RetireNextFrame(Item item)
	{
		yield return null;
		if (item == null) // Unity object — ==
		{
			yield break;
		}

		Object.Destroy(item.gameObject);
	}

	/// <summary>The holder every standing object hangs under — created on first use, so a session with nobody else in the world creates no scene object at all.</summary>
	private GameObject Holder
	{
		get
		{
			if (_holder == null) // Unity object — ==; a scene change destroys the holder with the objects under it
			{
				_holder = new GameObject(HolderName);
			}

			return _holder;
		}
	}

	private void ReportFailure(string definitionId, string line)
	{
		if (_failures.TryLog(definitionId, value: null, out _))
		{
			_log.LogWarning("{Line}", line);
			return;
		}

		_log.LogDebug("{Line} ({Count} identical line(s) suppressed)", line, _failures.Suppressed(definitionId));
	}

	/// <summary>
	/// One id's local incarnation: the object, whose carried rows it is, its parent row in the DATA, and the frame it was created in.
	/// </summary>
	private sealed class Incarnation
	{
		internal Item? Item;
		internal ulong Owner;
		internal ulong ParentId;
		internal int CreatedFrame;
	}

	/// <summary>
	/// One owner's item tree as the last reconcile saw it, by REFERENCE: the list instance, its element
	/// instances and their contents, recursively. The fact table rewrites what it changes — a snapshot
	/// replaces the whole message (and its list), a carried fact or a nested move replaces an element, a drop
	/// removes one — so a tree whose references all still match has not changed, and the objects built from
	/// it are already aligned. That is what lets the fact edge's non-item fires (an enemy bite, a medical
	/// state, a limb event) cost one walk of the tree instead of a plan rebuild and a reflection-backed
	/// digest of every row; a tree that DID change still gets the full realign, which is what keeps a local
	/// copy's own decay corrected against the data (§6.8).
	/// </summary>
	private sealed class SeenTree
	{
		private object? _list;
		private CharacterItemMsg[] _items = [];
		private SeenTree[] _contents = [];

		internal static SeenTree Of(List<CharacterItemMsg> items)
		{
			var seen = new SeenTree { _list = items, _items = [.. items], _contents = new SeenTree[items.Count] };
			for (var i = 0; i < items.Count; i++)
			{
				seen._contents[i] = Of(items[i].Contents);
			}

			return seen;
		}

		internal bool Matches(List<CharacterItemMsg> items)
		{
			if (!ReferenceEquals(_list, items) || _items.Length != items.Count)
			{
				return false;
			}

			for (var i = 0; i < _items.Length; i++)
			{
				if (!ReferenceEquals(_items[i], items[i]) || !_contents[i].Matches(items[i].Contents))
				{
					return false;
				}
			}

			return true;
		}
	}
}
