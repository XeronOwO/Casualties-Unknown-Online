using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Items;
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

	/// <summary>The one world-item creation path (the give ladder's drop rung and the console summon verb share it).</summary>
	private readonly WorldItemCreation _worldItems = new(domains.Items, domains.Log);

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
	/// A transferred item enters a container. The container's own rules are the final judge: when it refuses, the
	/// item falls back to a slot on this body with one Warning naming the container's reason — the user's ruling for
	/// a handed-over item ("the receiver's own rules refuse it and one observable line is written").
	///
	/// <para>
	/// The fallback PLACES the object the restore materialized, never a second one:
	/// <see cref="ItemStateCodec.RestoreContent"/> materializes the item before the container can be asked, and a
	/// destroy of that object is reported as its instance id's end at the end of the frame — the second object the
	/// fallback used to build with the same id was then swept, and the handed-over item was gone from both players
	/// (batch <c>20261021-a</c>, row 4).
	/// </para>
	/// </summary>
	internal bool AddCarriedItemToLocalContainer(Body body, CharacterItemMsg item, ulong parentItemId)
	{
		var parent = CarriedItemLocator.FindById(body, parentItemId);
		var container = parent == null ? null : parent.GetComponent<Container>(); // Unity object — ==
		if (container == null) // Unity object — ==
		{
			// Nothing was materialized, so the fallback must create the item itself.
			_domains.Log.LogWarning("[PlayerInteraction] {ItemId} (id {InstanceId}) not placed into container {ParentId} ({Why}) — falling back to a slot.",
				item.ItemId, item.InstanceId, parentItemId,
				parent == null ? "the parent is not on the local body" : "the target is not a container"); // Unity object — ==
			return AddCarriedItemToLocalBody(body, item);
		}

		var restored = ItemStateCodec.RestoreContent(parent!, container, item);
		if (restored.Loaded)
		{
			_domains.Log.LogInformation("[PlayerInteraction] placed {ItemId} (id {InstanceId}) into container {Parent} ({ParentId}).",
				item.ItemId, item.InstanceId, parent!.id, parentItemId);
			return true;
		}

		if (restored.Item is not { } materialized)
		{
			// Nothing came into existence on this client (its definition's prefab does not load here), so there is no
			// object to hand on — and a rung that would create it from the message fails for the same reason.
			_domains.Log.LogWarning("[PlayerInteraction] {ItemId} (id {InstanceId}) not placed into container {ParentId} — the item could not be created on this client, so there is nothing to place.",
				item.ItemId, item.InstanceId, parentItemId);
			return false;
		}

		_domains.Log.LogWarning("[PlayerInteraction] {ItemId} (id {InstanceId}) not placed into container {ParentId} ({Why}) — falling back to a slot.",
			item.ItemId, item.InstanceId, parentItemId, restored.Refusal ?? "the container's own rules refused it");

		// The item's own slot is not what a container destination named: the message carries the PARENT's slot for a
		// nested item, and letting the placement read that as a request would log a substitution nobody asked for.
		item.SlotIndex = -1;
		if (TryPlaceInBodySlot(body, item, materialized))
		{
			return true;
		}

		ReportUnplaceableItem(item, materialized);
		return false;
	}

	/// <summary>A transferred item enters a body slot: the one the transfer named when it is free, else the first free one.</summary>
	internal bool AddCarriedItemToLocalBody(Body body, CharacterItemMsg item)
	{
		if (ItemStateCodec.MaterializeItem(item, body.transform.position) is not { } created)
		{
			return false;
		}

		if (TryPlaceInBodySlot(body, item, created))
		{
			return true;
		}

		ReportUnplaceableItem(item, created);
		return false;
	}

	/// <summary>
	/// Puts an item that already exists into a slot this body accepts. The rule lives in
	/// <see cref="LocalBodyPlacement"/> because the character restore's unplaced row needs the same
	/// one (an item the archive carries with no body slot).
	/// </summary>
	private bool TryPlaceInBodySlot(Body body, CharacterItemMsg item, Item placed) =>
		LocalBodyPlacement.TryPlaceInSlot(body, item, placed, _domains.Log, "PlayerInteraction", out _);

	/// <summary>
	/// Nothing on this body accepts the item. It has already left the giver and no destination here can hold it, so
	/// its END is reported — a loud, consistent loss on both clients — instead of leaving an object nothing names (a
	/// guest's copy of that is deleted by the next world-item keyframe, silently). The world-drop answer for this
	/// case needs the host to re-record the item, which is
	/// <c>todo/placement-outcome-never-returns.md</c>; a spawn report from here is refused by the kernel, because the
	/// id is already a carried row (the finding that removed the first version of this rung).
	/// </summary>
	private void ReportUnplaceableItem(CharacterItemMsg item, Item placed)
	{
		_domains.Log.LogWarning("[PlayerInteraction] {ItemId} (id {InstanceId}) found no slot this body accepts — reporting the item's end so both sides agree it is gone.",
			item.ItemId, item.InstanceId);
		Object.Destroy(placed.gameObject);
	}

	/// <summary>
	/// The give ladder's last rung on the receiving side: create the item's world object where this body stands,
	/// give it the host's instance id, apply the fact's state and REPORT the creation through the item domain —
	/// the row the host and the peers keep is that report, not the local object. This rung is for an item the
	/// host never recorded as carried (<c>Destination.Drop</c>); a handed-over item already has a kernel row, so a
	/// spawn report for it is refused as a conflict.
	/// </summary>
	internal void DropCreatedItemAtLocalBody(Body body, CharacterItemMsg item) =>
		_worldItems.TryCreateAt(body.transform.position, item, "Give", out _);

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
