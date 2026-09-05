using CasualtiesUnknownOnline.GameAdapter.Items;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Local-body carried-item lookup by authoritative instance id.
///
/// The body's real inventory is a recursive tree: a slot item can be a
/// container whose children are themselves carried items. Remote inventory
/// apply/transfer paths must therefore search the whole carried subtree, not
/// only direct slot/limb children. A direct-children-only search silently loses
/// items inside a backpack/trash-bag: a container move cannot find the parent,
/// a move-to-slot cannot find the source, and the owner's next character
/// snapshot reports a different home from the one the event-driven clone just
/// showed.
/// </summary>
internal static class CarriedItemLocator
{
	/// <summary>
	/// Finds one real local-body item by its authoritative instance id anywhere
	/// in the carried subtree. Display proxies (remote clone renders) are
	/// intentionally never resolved — this is the owner-side body, and a proxy
	/// has no authority over the real item.
	/// </summary>
	public static Item? FindById(Body body, ulong instanceId)
	{
		if (body == null || instanceId == 0) // Unity object — ==
		{
			return null;
		}

		foreach (var item in body.GetComponentsInChildren<Item>(true))
		{
			if (item == null || item.GetComponentInParent<RemoteCloneRender>() != null) // Unity objects — ==
			{
				continue;
			}

			var id = item.GetComponent<ItemInstanceId>();
			if (id != null && id.Id == instanceId) // Unity object — ==
			{
				return item;
			}
		}

		return null;
	}
}
