using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Where a transfer's item is placed on the LOCAL body — the receiving half of cross-player custody, split
/// from the message handlers because placement is its own responsibility: remove the source's object, put a
/// carried item into a body slot or a container, re-home a same-owner move, and materialize the give ladder's
/// world drop.
///
/// <para>
/// The drop is the one placement that leaves the body: it creates the item's world object at the local
/// body's position, stamps the id the host minted BEFORE the object's own <c>Start</c> runs (an id-less
/// object would be reported as a runtime spawn with a NEW id) and reports the creation through the item
/// domain's own spawn path — the same call the native world-item hook makes. Reporting it here is what puts
/// the row in the host's kernel and the peers' tables; creating the object without the report leaves a
/// phantom the host never heard of.
/// </para>
/// </summary>
internal sealed class PlayerInventoryPlacement(GameAdapterDomains domains)
{
	private readonly GameAdapterDomains _domains = domains;

	/// <summary>A carried item leaves this body: destroy the local object at any depth (a slot, a worn limb or a container).</summary>
	internal void RemoveCarriedItemFromLocalBody(Body body, ulong instanceId)
	{
		if (instanceId == 0)
		{
			return;
		}

		// A carried item may live at any depth: a top-level slot, a worn limb,
		// or inside a carried container. The authoritative transfer can lift an
		// item out of a nested container, so the local body removal must search
		// the whole carried-item subtree, not just direct slot/limb children.
		foreach (var item in body.GetComponentsInChildren<Item>(true))
		{
			if (TryDestroyById(item, instanceId))
			{
				return;
			}
		}
	}

	/// <summary>The same-owner container move: re-home the existing object through the native container, never destroying it first.</summary>
	internal bool TryMoveExistingItemToLocalContainer(Body body, CharacterItemMsg item, ulong parentItemId)
	{
		var source = CarriedItemLocator.FindById(body, item.InstanceId);
		var parent = CarriedItemLocator.FindById(body, parentItemId);
		if (source == null || parent == null) // Unity objects — ==
		{
			return false;
		}

		var container = parent.GetComponent<Container>();
		if (container == null) // Unity object — ==
		{
			return false;
		}

		// The native container move routes through Container.LoadItem only:
		// it re-parents the item from its old slot/container directly and
		// leaves it untouched when the target cannot hold it. Do NOT unload the
		// source first — that would orphan the real item if LoadItem refuses
		// (wrong weight/tag/distance) and the fallback would have to rebuild.
		if (!container.CanHoldItem(source))
		{
			_domains.Log.LogWarning(
				"[PlayerInteraction] same-owner container move refused by native capacity/tags: {ItemId} (id {InstanceId}, weight {Weight:F2}) into {Parent} ({ParentId}, maxPerItem {MaxPerItem:F2}, maxTotal {MaxTotal:F2}, current {Current:F2}).",
				item.ItemId, item.InstanceId, source.totalWeight, parent.id, parentItemId,
				container.maxWeightPerItem, container.maxWeight, container.GetHoldingWeight());
			return false;
		}

		container.LoadItem(source);
		if (source.transform.parent != container.transform) // Unity object — ==
		{
			_domains.Log.LogWarning("[PlayerInteraction] same-owner container move failed: {ItemId} (id {InstanceId}) did not land in {Parent} ({ParentId}) despite CanHoldItem.",
				item.ItemId, item.InstanceId, parent.id, parentItemId);
			return false;
		}

		_domains.Log.LogInformation("[PlayerInteraction] re-homed existing {ItemId} (id {InstanceId}) into container {Parent} ({ParentId}).",
			item.ItemId, item.InstanceId, parent.id, parentItemId);
		return true;
	}

	/// <summary>
	/// A transferred item enters a container. The container's own rules are the final judge. A give
	/// (<paramref name="dropWhenRefused"/>) has no source body left to fall back to and its answer already
	/// named that container, so a refusal is answered by the ladder's last rung — the drop — rather than a
	/// silent slot swap the operator was not told about. Every other transfer keeps the slot fallback: its
	/// item did come off a body and a slot is still the better place for it.
	/// </summary>
	internal bool AddCarriedItemToLocalContainer(Body body, CharacterItemMsg item, ulong parentItemId, bool dropWhenRefused)
	{
		var parent = CarriedItemLocator.FindById(body, parentItemId);
		var container = parent == null ? null : parent.GetComponent<Container>(); // Unity object — ==
		var why = parent == null // Unity object — ==
			? "the parent is not on the local body"
			: container == null ? "the target is not a container" : null;

		if (container == null || ItemStateCodec.RestoreContent(parent!, container, item) == null) // Unity object — ==
		{
			if (dropWhenRefused)
			{
				_domains.Log.LogWarning(
					"[PlayerInteraction] {ItemId} (id {InstanceId}) not placed into container {ParentId} ({Why}) — the give ladder's last rung: dropping it at this body instead.",
					item.ItemId, item.InstanceId, parentItemId, why ?? "the container's own rules refused it");
				DropCreatedItemAtLocalBody(body, item);
				return true;
			}

			// The container's own rules are the final judge (the user's ruling for a handed-over item), so anything
			// that cannot host it falls back to a slot: the item has already left the giver by this point, and
			// "not loaded" with no fallback is how an item vanishes from both players' ledgers.
			_domains.Log.LogWarning("[PlayerInteraction] {ItemId} (id {InstanceId}) not placed into container {ParentId} ({Why}) — falling back to a slot.",
				item.ItemId, item.InstanceId, parentItemId, why ?? "the container's own rules refused it");
			return AddCarriedItemToLocalBody(body, item);
		}

		_domains.Log.LogInformation("[PlayerInteraction] placed {ItemId} (id {InstanceId}) into container {Parent} ({ParentId}).",
			item.ItemId, item.InstanceId, parent!.id, parentItemId);
		return true;
	}

	/// <summary>A transferred item enters a body slot: the one the transfer named when it is free, else the first free one.</summary>
	internal bool AddCarriedItemToLocalBody(Body body, CharacterItemMsg item)
	{
		int slot;
		if (item.SlotIndex >= 0
			&& item.SlotIndex < body.slots.Length
			&& !body.HoldingItem(item.SlotIndex))
		{
			slot = item.SlotIndex;
		}
		else
		{
			var empty = body.FirstEmptySlot();
			if (empty is not { } fallback)
			{
				_domains.Log.LogWarning("[PlayerInteraction] cannot place {ItemId} (id {InstanceId}) — no empty inventory slot.", item.ItemId, item.InstanceId);
				return false;
			}

			slot = fallback;
		}

		item.SlotIndex = slot;
		ItemStateCodec.RestoreItem(item, body);
		_domains.Log.LogInformation("[PlayerInteraction] placed {ItemId} (id {InstanceId}) in local slot {Slot}.", item.ItemId, item.InstanceId, slot);
		return true;
	}

	/// <summary>
	/// The give ladder's last rung on the receiving side: create the item's world object where this body
	/// stands, give it the host's instance id, apply the fact's state and REPORT the creation through the
	/// item domain — the row the host and the peers keep is that report, not the local object.
	/// </summary>
	internal void DropCreatedItemAtLocalBody(Body body, CharacterItemMsg item)
	{
		var prefab = ItemPrefabResolver.Load(item.ItemId);
		if (prefab == null) // Unity object — ==
		{
			_domains.Log.LogWarning("[Give] drop of {ItemId} (id {InstanceId}) skipped: its prefab does not load on this client.", item.ItemId, item.InstanceId);
			return;
		}

		var go = Object.Instantiate(prefab, body.transform.position, Quaternion.identity);
		go.SetActive(true);
		var created = go.GetComponent<Item>();
		if (created == null) // Unity object — ==
		{
			Object.Destroy(go);
			_domains.Log.LogWarning("[Give] drop of {ItemId} (id {InstanceId}) skipped: its prefab has no Item component.", item.ItemId, item.InstanceId);
			return;
		}

		// Stamp the id the host minted BEFORE the item's own Start runs: an id-less world object would be
		// reported as a runtime spawn with a NEW id, and the give's row would name an item nothing carries.
		if (item.InstanceId != 0)
		{
			created.gameObject.AddComponent<ItemInstanceId>().Id = item.InstanceId;
		}

		created.condition = item.Condition;
		created.favourited = item.Favourited;
		ItemStateCodec.RestoreLiquids(created, item.Liquids);
		ItemStateCodec.RestoreComponentStates(created, item.Components);
		ItemStateCodec.RestoreContents(created, item.Contents);

		// The report is the row: a created object nobody reported is invisible to the host's kernel and is
		// killed by the next world-item keyframe. This is the same report the native world-item hook makes.
		_domains.Items.SendItemSpawned(
			item.InstanceId,
			ItemStateCodec.CaptureItem(created, -1),
			new NetVector2(created.transform.position.x, created.transform.position.y),
			new NetVector2(0f, 0f),
			0f,
			freshItemDrop: false,
			angularVelocity: 0f);
		_domains.Log.LogInformation(
			"[Give] dropped {ItemId} (id {InstanceId}) at the local body's position ({X:F1},{Y:F1}) and reported its creation.",
			item.ItemId, item.InstanceId, created.transform.position.x, created.transform.position.y);
	}

	private static bool TryDestroyById(Item item, ulong instanceId)
	{
		if (item == null) // Unity object — ==
		{
			return false;
		}

		var id = item.GetComponent<ItemInstanceId>();
		if (id != null && id.Id == instanceId) // Unity object — ==
		{
			// Detach before Destroy. Object.Destroy is deferred to the end of
			// the frame; if a nested container child is left parented, the
			// immediate re-report after the transfer still captures the item
			// (one ghost frame / visible→invisible after the next snapshot).
			item.transform.SetParent(null);
			Object.Destroy(item.gameObject);
			return true;
		}

		return false;
	}
}
