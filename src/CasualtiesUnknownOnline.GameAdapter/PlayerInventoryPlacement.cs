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
	/// item falls back to a slot on this body with one Warning naming the container's reason — the older ruling for
	/// a handed-over item ("the receiver's own rules refuse it and one observable line is written"). Since the give's
	/// OUTCOME landed, the ruling that governs this race arm is the 2026-10-22 one: this method answers where the
	/// item really landed, the receiving client reports that place, and a place that is not the answered one sends
	/// the item back to the giver — so the fallback is no longer a silent local answer.
	///
	/// <para>
	/// The fallback PLACES the object the restore materialized, never a second one:
	/// <see cref="ItemStateCodec.RestoreContent"/> materializes the item before the container can be asked, and a
	/// destroy of that object is reported as its instance id's end at the end of the frame — the second object the
	/// fallback used to build with the same id was then swept, and the handed-over item was gone from both players
	/// (batch <c>20261021-a</c>, row 4).
	/// </para>
	/// <para>
	/// <paramref name="suppressUnplacedEnd"/> is the GIVE's own arm: the host returns the row on the outcome this
	/// client is about to report, so an item it could place nowhere must not announce its end
	/// (<see cref="UnplacedGiveItemOrigin"/>). A returned item that finds no place has nothing behind it and keeps
	/// its honest end report, which is why the flag is the CALLER's and not this class's.
	/// </para>
	/// </summary>
	internal LocalPlacement AddCarriedItemToLocalContainer(Body body, CharacterItemMsg item, ulong parentItemId, bool suppressUnplacedEnd = false)
	{
		var parent = CarriedItemLocator.FindById(body, parentItemId);
		var container = parent == null ? null : parent.GetComponent<Container>(); // Unity object — ==
		if (container == null) // Unity object — ==
		{
			// Nothing was materialized, so the fallback must create the item itself.
			var why = parent == null ? "the parent is not on the local body" : "the target is not a container";
			_domains.Log.LogWarning("[PlayerInteraction] {ItemId} (id {InstanceId}) not placed into container {ParentId} ({Why}) — falling back to a slot.",
				item.ItemId, item.InstanceId, parentItemId, why);
			return FallBackToSlot(body, item, why, suppressUnplacedEnd);
		}

		var restored = ItemStateCodec.RestoreContent(parent!, container, item);
		if (restored.Loaded)
		{
			_domains.Log.LogInformation("[PlayerInteraction] placed {ItemId} (id {InstanceId}) into container {Parent} ({ParentId}).",
				item.ItemId, item.InstanceId, parent!.id, parentItemId);
			return LocalPlacement.InContainer(parentItemId);
		}

		if (restored.Item is not { } materialized)
		{
			// Nothing came into existence on this client (its definition's prefab does not load here), so there is no
			// object to hand on — and a rung that would create it from the message fails for the same reason.
			_domains.Log.LogWarning("[PlayerInteraction] {ItemId} (id {InstanceId}) not placed into container {ParentId} — the item could not be created on this client, so there is nothing to place.",
				item.ItemId, item.InstanceId, parentItemId);
			return LocalPlacement.NotPlaced("the item could not be created on this client");
		}

		var refusal = restored.Refusal ?? "the container's own rules refused it";
		_domains.Log.LogWarning("[PlayerInteraction] {ItemId} (id {InstanceId}) not placed into container {ParentId} ({Why}) — falling back to a slot.",
			item.ItemId, item.InstanceId, parentItemId, refusal);

		// The item's own slot is not what a container destination named: the message carries the PARENT's slot for a
		// nested item, and letting the placement read that as a request would log a substitution nobody asked for.
		item.SlotIndex = -1;
		if (TryPlaceInBodySlot(body, item, materialized, out var placedSlot))
		{
			return LocalPlacement.InSlot(placedSlot, refusal);
		}

		DiscardUnplaceableItem(item, materialized, suppressUnplacedEnd);
		return LocalPlacement.NotPlaced("nothing on this client's body accepted the item");
	}

	/// <summary>A transferred item enters a body slot: the one the transfer named when it is free, else the first free one.</summary>
	internal LocalPlacement AddCarriedItemToLocalBody(Body body, CharacterItemMsg item, bool suppressUnplacedEnd = false)
	{
		if (ItemStateCodec.MaterializeItem(item, body.transform.position) is not { } created)
		{
			return LocalPlacement.NotPlaced("the item could not be created on this client");
		}

		if (TryPlaceInBodySlot(body, item, created, out var placedSlot))
		{
			return LocalPlacement.InSlot(placedSlot);
		}

		DiscardUnplaceableItem(item, created, suppressUnplacedEnd);
		return LocalPlacement.NotPlaced("nothing on this client's body accepted the item");
	}

	/// <summary>
	/// The container is not on this body at all, so nothing was materialized: place the item in a slot instead, and
	/// keep the container's own sentence as the reason the place differs from the one this client answered.
	/// </summary>
	private LocalPlacement FallBackToSlot(Body body, CharacterItemMsg item, string reason, bool suppressUnplacedEnd)
	{
		var placed = AddCarriedItemToLocalBody(body, item, suppressUnplacedEnd);
		return placed.Applied ? placed with { Reason = reason } : placed;
	}

	/// <summary>
	/// Puts an item that already exists into a slot this body accepts. The rule lives in
	/// <see cref="LocalBodyPlacement"/> because the character restore's unplaced row needs the same
	/// one (an item the archive carries with no body slot).
	/// </summary>
	private bool TryPlaceInBodySlot(Body body, CharacterItemMsg item, Item placed, out int placedSlot) =>
		LocalBodyPlacement.TryPlaceInSlot(body, item, placed, _domains.Log, "PlayerInteraction", out placedSlot);

	/// <summary>
	/// Nothing on this body accepts the item, so its object is discarded. On the GIVE's arm the item's END is NOT
	/// reported (<paramref name="suppressUnplacedEnd"/>): the client reports that nothing was placed, the host
	/// returns the row to the giver, and a deferred <c>Item.OnDestroy</c> would report that id as dead while the
	/// host is putting it back (the marker keeps the hook silent, the same shape as the limb-component marker). A
	/// returned item that finds no place is the other arm — no outcome follows, so the item really is gone and its
	/// end report stands. A world drop is not the answer for either: the id is already a carried row on the host,
	/// so a spawn report for it is refused by the kernel as a conflict.
	/// </summary>
	private void DiscardUnplaceableItem(CharacterItemMsg item, Item placed, bool suppressUnplacedEnd)
	{
		_domains.Log.LogWarning(
			suppressUnplacedEnd
				? "[PlayerInteraction] {ItemId} (id {InstanceId}) found no place this body accepts — reporting that nothing was placed so the host returns it to the giver."
				: "[PlayerInteraction] {ItemId} (id {InstanceId}) found no place this body accepts, and nothing will return it — reporting the item's end so both sides agree it is gone.",
			item.ItemId, item.InstanceId);
		if (suppressUnplacedEnd)
		{
			placed.gameObject.AddComponent<UnplacedGiveItemOrigin>();
		}

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
			// THE ITEM DID NOT DIE — IT MOVED to another owner, and the authoritative transfer already carries that fact. Zero the instance id
			// first, the same step KillRemoteItem takes for a remote deletion, so ItemWorldSync.OnItemDestroyed sees id 0 and reports nothing.
			// Reporting a destroy here is what marked the transferred item TERMINAL in the kernel, after which every later state update from its
			// new owner was refused as an invalid transition and its row left the inventory — the report "gave an item, rearranged it on that
			// player, and the item was swallowed" (host log 2026-10-25 18:02:00: OnItemDestroyed committed for the item the give had just moved,
			// then the owner's ItemUpdateState refused with InvalidTransition four hundred milliseconds later).
			id.Id = 0;

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
