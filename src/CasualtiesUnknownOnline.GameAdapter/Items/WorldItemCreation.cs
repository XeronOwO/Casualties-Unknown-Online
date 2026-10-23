using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// ONE world-item object materialized from an already-created fact, at a point, and REPORTED through the item
/// domain — the single implementation shared by the give ladder's drop rung and the console <c>/summon</c> verb.
///
/// <para>
/// The fact (definition, instance id, state) is created elsewhere — the give's creation port, or the same port
/// behind the summon seam — and it already carries the id the host minted. The id is stamped BEFORE the object's
/// own <c>Start</c> runs: an id-less world object would be reported as a runtime spawn with a NEW id, and the
/// caller's row would name an item nothing carries. The report is the row — a created object nobody reported is
/// invisible to the host's kernel and is killed by the next world-item keyframe — so this class reports it with
/// the same <c>SendItemSpawned</c> the native world-item hook makes.
/// </para>
///
/// <para>
/// Two callers, one rule: the give's drop rung places the item at the receiving body, the summon verb at the point
/// the operator named. Neither may skip the report, which is why the report lives here rather than at each call
/// site (the give ladder's review found a drop rung that created the object without it).
/// </para>
/// </summary>
internal sealed class WorldItemCreation(IItemControl items, ILogger log)
{
	private readonly IItemControl _items = items;
	private readonly ILogger _log = log;

	/// <summary>
	/// Create the item's world object at <paramref name="position"/>, stamp the fact's instance id, apply the
	/// fact's state and report the creation. False with <paramref name="refusal"/> naming the ground when this
	/// client cannot materialize the prefab; nothing is created in that case.
	/// <paramref name="context"/> names the caller in the log (<c>Give</c>, <c>Summon</c>).
	/// </summary>
	internal bool TryCreateAt(Vector3 position, CharacterItemMsg item, string context, out string? refusal)
	{
		refusal = null;
		var prefab = ItemPrefabResolver.Load(item.ItemId);
		if (prefab == null) // Unity object — ==
		{
			refusal = $"the item prefab '{item.ItemId}' does not load on this client, so it cannot be created.";
			_log.LogWarning("[{Context}] {ItemId} (id {InstanceId}) skipped at ({X:F1},{Y:F1}): its prefab does not load on this client.",
				context, item.ItemId, item.InstanceId, position.x, position.y);
			return false;
		}

		var go = Object.Instantiate(prefab, position, Quaternion.identity);
		go.SetActive(true);
		var created = go.GetComponent<Item>();
		if (created == null) // Unity object — ==
		{
			Object.Destroy(go);
			refusal = $"the prefab '{item.ItemId}' has no Item component, so it cannot be created.";
			_log.LogWarning("[{Context}] {ItemId} (id {InstanceId}) skipped: its prefab has no Item component.", context, item.ItemId, item.InstanceId);
			return false;
		}

		// The id the host minted, stamped BEFORE the item's own Start runs: an id-less object would be reported
		// as a runtime spawn with a NEW id, and this fact's row would name an item nothing carries.
		if (item.InstanceId != 0)
		{
			created.gameObject.AddComponent<ItemInstanceId>().Id = item.InstanceId;
		}

		created.condition = item.Condition;
		created.favourited = item.Favourited;
		ItemStateCodec.RestoreLiquids(created, item.Liquids);
		ItemStateCodec.RestoreComponentStates(created, item.Components);
		ItemStateCodec.RestoreContents(created, item.Contents);

		// The report is the row: the same report the native world-item hook makes.
		_items.SendItemSpawned(
			item.InstanceId,
			ItemStateCodec.CaptureItem(created, -1),
			new NetVector2(created.transform.position.x, created.transform.position.y),
			new NetVector2(0f, 0f),
			0f,
			freshItemDrop: false,
			angularVelocity: 0f);
		_log.LogInformation("[{Context}] created {ItemId} (id {InstanceId}) at ({X:F1},{Y:F1}) and reported its creation.",
			context, item.ItemId, item.InstanceId, created.transform.position.x, created.transform.position.y);
		return true;
	}

	/// <summary>
	/// Report an EXISTING object as a world item where it stands: the refused restored child its container would
	/// not take (user ruling 2026-10-23 — it falls to the ground, never destroyed and never pushed into a slot).
	/// The restore already materialized the object, stamped its id and restored its state, so this only reports it
	/// with the same creation report <see cref="TryCreateAt"/> makes: without a report the host never learns of it
	/// and the next world-item keyframe sweeps it away.
	/// </summary>
	internal void DropAsWorldItem(Item existing, string context)
	{
		var marker = existing.GetComponent<ItemInstanceId>();
		var id = marker != null ? marker.Id : 0UL; // Unity object — ==
		var position = existing.transform.position;
		_items.SendItemSpawned(
			id,
			ItemStateCodec.CaptureItem(existing, -1),
			new NetVector2(position.x, position.y),
			new NetVector2(0f, 0f),
			0f,
			freshItemDrop: false,
			angularVelocity: 0f);
		_log.LogInformation(
			"[{Context}] refused content item {ItemId} (id {InstanceId}) fell to the ground at ({X:F1},{Y:F1}) and its creation was reported.",
			context, existing.id, id, position.x, position.y);
	}
}
