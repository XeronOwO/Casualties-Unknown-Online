using CasualtiesUnknownOnline.GameAdapter.Content;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Puts ONE worn item back on the local body: the limbs a character snapshot
/// carries as negative slot indices. Both write paths need exactly this — the
/// restore (via <see cref="CharacterRestoreApplier"/>) and a cross-player
/// "dress the local body" result (a container interaction that hands the local
/// player a wearable, <c>PlayerInteractionApply</c>) — so it is its own type
/// instead of a method one of them happens to expose. Split out of
/// <see cref="CharacterDataSync"/> together with the restore applier.
/// </summary>
internal sealed class WearableRestorer(ILogger<WearableRestorer> log)
{
	private readonly ILogger<WearableRestorer> _log = log;

	/// <summary>
	/// Restore a worn item onto its limb (mirrors WearWearable, Body.cs:1480:
	/// parented to the limb, physics off, identity pose). The limb comes from the
	/// captured negative SlotIndex — the restore path never had the item in a
	/// backpack, so the game's slot-driven wear flow cannot run.
	/// </summary>
	internal void RestoreWearable(CharacterItemMsg itemData, Body body)
	{
		var limbIndex = -itemData.SlotIndex - 2;
		if (limbIndex < 0 || limbIndex >= body.limbs.Length)
		{
			_log.LogWarning("Restore: worn {ItemId} has limb index {Limb} out of range — skipped.", itemData.ItemId, limbIndex);
			return;
		}

		var prefab = ItemPrefabResolver.Load(itemData.ItemId);
		if (prefab == null) // Unity object — ==
		{
			_log.LogWarning("Restore: {ItemId} has no prefab — skipped.", itemData.ItemId);
			return;
		}

		var go = Object.Instantiate(prefab, body.transform.position, Quaternion.identity);
		go.SetActive(true);
		var item = go.GetComponent<Item>();
		if (item == null) // Unity object — ==
		{
			Object.Destroy(go);
			_log.LogWarning("Restore: {ItemId} has no Item component — skipped.", itemData.ItemId);
			return;
		}

		if (itemData.InstanceId != 0)
		{
			// Identity restore — same rationale as ItemStateCodec.RestoreItem:
			// the reconnect-merge ids keep the restored item the SAME instance
			// the host knows (an id-less restore reads as a runtime spawn).
			item.gameObject.AddComponent<ItemInstanceId>().Id = itemData.InstanceId;
		}

		item.condition = itemData.Condition;
		item.favourited = itemData.Favourited;
		ItemStateCodec.RestoreLiquids(item, itemData.Liquids);
		ItemStateCodec.RestoreComponentStates(item, itemData.Components);
		ItemStateCodec.RestoreContents(item, itemData.Contents);

		var limb = body.limbs[limbIndex];
		item.rb.simulated = false;
		item.transform.SetParent(limb.transform);
		item.transform.localScale = Vector3.one;
		item.transform.localRotation = Quaternion.identity;
		item.transform.localPosition = Vector3.zero;
		var sr = item.GetComponent<SpriteRenderer>();
		if (sr != null) // Unity object — ==
		{
			sr.sortingOrder = limb.GetComponent<SpriteRenderer>().sortingOrder + item.Stats.wearableVisualOffset;
		}

		// A restored worn item is already on its limb; a custom worn-sprite
		// visual must be applied here because the restore path never runs the
		// vanilla WearWearable flow (and therefore neither the wear patch).
		item.GetComponent<CustomItemVisualState>()?.ApplyWornVisual();
		item.GetComponent<CustomItemVisualState>()?.EnsureSecondarySprites(body);
	}
}
