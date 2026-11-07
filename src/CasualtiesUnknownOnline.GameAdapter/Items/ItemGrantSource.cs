using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using GameObject = UnityEngine.GameObject;
using Object = UnityEngine.Object;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The game's own answer behind the console <c>/give</c> verb: resolve a definition
/// id to its prefab, create an INSTANCE the way the game's own <c>/spawn</c> does,
/// capture the fresh save-shaped fact that instance carries, overlay the operator's
/// state onto it, and allocate the instance id it will be known by.
///
/// <para>
/// The capture reads a freshly created instance, never the prefab ASSET: an asset's
/// components never ran <c>Awake</c>, so its condition is the serialized 0 and a water
/// container's stack is null — the wound view then refuses the item and the guest
/// disagrees about the same item (batch <c>20261107-o</c>). The game's own creation path
/// instantiates first and then sets the default condition (<c>ConsoleScript</c>'s
/// <c>/spawn</c> verb), and the grant follows it exactly. The instance never starts: it
/// is deactivated and destroyed before its <c>Start</c> runs, so no item-domain report
/// can fire for a temporary object. The overlay happens BEFORE the id is allocated: a
/// state the definition cannot carry refuses the whole give and mints nothing.
/// </para>
///
/// <para>
/// The id comes from the one allocator the rest of the adapter uses
/// (<see cref="ItemIdAllocator.AllocateId"/>): ids are (counter, account id), and a
/// counter anywhere else would mint an identity the host's watermark, the peers'
/// snapshots and the save archive never saw.
/// </para>
/// </summary>
internal sealed class ItemGrantSource(ItemIdAllocator ids, ILogger<ItemGrantSource> log) : IItemGrantSource
{
	private readonly ItemIdAllocator _ids = ids;
	private readonly ILogger _log = log;

	public bool TryCreate(string definitionId, SnbtValue? state, out CharacterItemMsg? item, out GrantRefusal? refusal)
	{
		item = null;
		refusal = null;
		if (!ItemDefinitionTable.TryResolve(definitionId, out var template, out var tableRefusal) || template is null)
		{
			refusal = tableRefusal ?? new GrantRefusal(
				"the definition could not be resolved.",
				"item.answer.refused_unknown_definition",
				definitionId);
			return false;
		}

		var createdObject = ItemPrefabResolver.InstantiateResource(template.gameObject, Vector3.zero, Quaternion.identity);
		if (createdObject is not GameObject createdRoot)
		{
			refusal = new GrantRefusal(
				"the definition has no prefab this build can load.",
				"item.answer.refused_no_prefab",
				definitionId);
			return false;
		}

		var created = createdRoot.GetComponent<Item>();
		if (created == null) // Unity object — ==
		{
			Object.Destroy(createdRoot);
			refusal = new GrantRefusal(
				"the definition's prefab has no Item component.",
				"item.answer.refused_no_item_component",
				definitionId);
			return false;
		}

		// The game's own /spawn sets the default condition on the instance it creates; the prefab asset's
		// serialized 0 is never what a spawn hands a player.
		created.SetCondition(1f);
		var capture = ItemStateCodec.CaptureItem(created, slotIndex: -1);

		// The temporary instance must never start: Item.Start is what reports a world item to the item
		// domain, so it is deactivated and destroyed in the same frame it was created.
		createdRoot.SetActive(false);
		Object.Destroy(createdRoot);

		var document = state is null
			? ItemStateDocument.Describe(capture)
			: SnbtTree.Overlay(ItemStateDocument.Describe(capture), state);

		if (!ItemStateDocument.TryBind(created, capture, document, out var bound, out var stateText))
		{
			refusal = new GrantRefusal(
				stateText ?? "the state text was not accepted for this definition.",
				"item.answer.refused_state_text",
				stateText ?? string.Empty);
			return false;
		}

		bound.InstanceId = _ids.AllocateId();
		item = bound;
		// One tag for the port, not for one of its callers: the console's give verb and its summon verb both create
		// facts through this seam, and a line naming one of them would misattribute the other.
		_log.LogInformation("[ItemCreate] prepared {Definition} (id {InstanceId}) from a fresh instance of its prefab{State}.",
			definitionId, bound.InstanceId, state is null ? "" : " with the operator's state");
		return true;
	}
}
