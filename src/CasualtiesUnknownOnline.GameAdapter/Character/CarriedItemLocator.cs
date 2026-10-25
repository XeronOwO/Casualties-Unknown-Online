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
	public static Item? FindById(Body body, ulong instanceId) => TryFind(body, instanceId, out var item) ? item : null;

	/// <summary>
	/// The <c>Try</c> shape of <see cref="FindById"/>, for a caller whose next line is a refusal by
	/// name rather than a null check: a cross-player consume has to say that the eater does not carry
	/// the item, because the game's own consume reads the eater's slots and a stand-in's slot steps
	/// silently misfire.
	/// </summary>
	public static bool TryFind(Body body, ulong instanceId, out Item item)
	{
		item = null!;
		if (body == null || instanceId == 0) // Unity object — ==
		{
			return false;
		}

		foreach (var candidate in body.GetComponentsInChildren<Item>(true))
		{
			if (candidate == null || candidate.GetComponentInParent<RemoteCloneRender>() != null) // Unity objects — ==
			{
				continue;
			}

			var id = candidate.GetComponent<ItemInstanceId>();
			if (id != null && id.Id == instanceId) // Unity object — ==
			{
				item = candidate;
				return true;
			}
		}

		return false;
	}
}
