using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using StringComparison = System.StringComparison;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// An item whose own action REPLACED its definition in place — the game's peel is the shape: the closed stone fruit
/// instantiates the opened one at its own position and puts it back into the SAME slot
/// (<c>Item.cs:2606-2620</c>), so the player's view of the result is "the item in my hand became the peeled one" and
/// nothing else moved.
///
/// <para>
/// It lives here rather than in the apply seam because it is the OWNER's half of a fact the AFFECTED side observed: the
/// eating client saw the object the delegate created and reported its definition, the host committed it onto the owner's
/// row, and this is what makes the owner's own scene show it. The instance id survives — the item never changed hands,
/// only its form — and the swap runs inside the caller's remote-apply scope, so neither the destroy nor the pickup is
/// reported as a local fact.
/// </para>
/// </summary>
internal static class ItemDefinitionSwap
{
	/// <summary>
	/// The owner's side of one cross-player item outcome: the item either kept its definition (the ordinary case, which
	/// is only a state write) or it BECAME another definition in place. One entry point so the apply seam reads one line
	/// and the two shapes cannot drift apart.
	/// </summary>
	internal static bool ApplyOwnerSide(Body body, Item item, CharacterItemMsg after, ulong instanceId, ILogger log)
	{
		if (string.Equals(after.ItemId, item.id, StringComparison.Ordinal))
		{
			item.condition = after.Condition;
			ItemStateCodec.RestoreLiquids(item, after.Liquids);
			ItemStateCodec.RestoreComponentStates(item, after.Components);
			log.LogInformation("[ItemUse] local item {ItemId} condition set to {Condition:F2} by cross-player use.", instanceId, after.Condition);
			return true;
		}

		return InPlace(body, item, after, instanceId, log);
	}

	/// <summary>
	/// Replace <paramref name="item"/> with the definition its row now names, at the same position, in the same slot or
	/// container. Answers whether the scene now holds the new form; a prefab this client cannot load, or an item it
	/// cannot locate, is refused with a logged line rather than half-applied.
	/// </summary>
	internal static bool InPlace(Body body, Item item, CharacterItemMsg after, ulong instanceId, ILogger log)
	{
		var slot = body.SlotOf(item);
		var container = item.transform.parent != null ? item.transform.parent.GetComponent<Container>() : null; // Unity object — ==
		if (slot < 0 && container == null) // Unity object — ==
		{
			log.LogWarning(
				"[ItemUse] the replaced item {ItemId} (id {InstanceId}) is neither in a slot nor in a container on this client — its new form cannot be placed.",
				item.id, instanceId);
			return false;
		}

		var position = item.transform.position;
		var rotation = item.transform.rotation;
		var prefab = ItemPrefabResolver.Load(after.ItemId);
		if (prefab == null) // Unity object — ==
		{
			log.LogWarning(
				"[ItemUse] the replaced item {ItemId} (id {InstanceId}) names '{Replacement}', whose prefab does not load on this client.",
				item.id, instanceId, after.ItemId);
			return false;
		}

		// THE OLD FORM MUST RELEASE ITS SLOT BEFORE THE DEFERRED DESTROY RUNS. UnityEngine.Object.Destroy executes at
		// end of frame, so the old object still occupied the slot when the pickup below ran: the game refused the
		// replacement and dropped it into the world, then the destroy hook reported the item ended — committing a
		// terminal row for an id that now had an object (measured 2026-11-06, session 20261106-f: `[PickUpResult]
		// stonefruitopen → world (slot 0) at (4.0,494.4)` and `[ItemCustody] … TerminalRowWithObject`). Detaching and
		// zeroing the id here makes the slot free for the pickup and keeps the destroy out of the item reports — the
		// same two steps RemoteItemSceneOps.KillRemoteItem takes for a remote deletion.
		item.transform.SetParent(null, worldPositionStays: true);
		foreach (var oldId in item.GetComponentsInChildren<ItemInstanceId>(true))
		{
			oldId.Id = 0;
		}

		Object.Destroy(item.gameObject);

		var go = Object.Instantiate(prefab, position, rotation);
		go.SetActive(true);
		var created = go.GetComponent<Item>();
		if (created == null) // Unity object — ==
		{
			Object.Destroy(go);
			log.LogWarning("[ItemUse] the replacement '{Replacement}' has no Item component on this client.", after.ItemId);
			return false;
		}

		created.gameObject.AddComponent<ItemInstanceId>().Id = instanceId;
		created.condition = after.Condition;
		ItemStateCodec.RestoreLiquids(created, after.Liquids);
		ItemStateCodec.RestoreComponentStates(created, after.Components);
		ItemStateCodec.RestoreContents(created, after.Contents);

		if (container != null) // Unity object — ==
		{
			container.LoadItem(created);
		}
		else
		{
			body.PickUpItem(created, slot, false);
		}

		log.LogInformation(
			"[ItemUse] local item {ItemId} (id {InstanceId}) became {Replacement} in place at the same slot.",
			item.id, instanceId, after.ItemId);
		return true;
	}
}
