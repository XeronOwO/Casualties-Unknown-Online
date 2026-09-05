using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Holds a runtime world item's CUO instance id — (local counter, spawner
/// account id), globally unique without host allocation so the spawner can
/// apply its own item immediately (local compute). Attached by the Item.Start
/// hook when a runtime-generated item appears; remote applications attach it
/// first, which is how the hook recognizes them and does not re-report.
/// Generation-time items never carry one — world-gen determinism covers them.
/// </summary>
public sealed class ItemInstanceId : MonoBehaviour
{
	/// <summary>
	/// Runtime id → component index. The previous item-domain lookup scanned
	/// every <c>Item</c> in the scene (and, on a miss, every
	/// <c>Object.FindObjectsOfType</c> result) for every followed id every
	/// frame — O(items × followedItems) guest-side work that showed up as a
	/// low guest frame rate. This component is the natural owner of the id it
	/// carries, so it registers/unregisters itself here and lookup becomes O(1).
	/// </summary>
	private static readonly Dictionary<ulong, ItemInstanceId> ById = [];

	private ulong _id;

	/// <summary>
	/// The instance id. Setting it registers the live scene object in the
	/// runtime index; zeroing it (remote kill) unregisters it; destroying the
	/// object unregisters it. The index is the authoritative scene-side
	/// lookup for <see cref="RemoteItemSceneOps.FindWorldItem"/> and replaces
	/// the per-frame linear scans.
	/// </summary>
	public ulong Id
	{
		get => _id;
		set
		{
			if (_id == value)
			{
				return;
			}

			Unregister();
			_id = value;
			Register();
		}
	}

	/// <summary>
	/// O(1) id → scene object lookup for the item domain. Returns the <see cref="Item"/>
	/// component that owns this instance id, or false when the id is unknown /
	/// stale. Unity destroyed objects are detected via the operator overload and
	/// removed lazily so a stale entry cannot be returned.
	/// </summary>
	public static bool TryFindItem(ulong id, out Item item)
	{
		item = null!;
		if (id == 0 || !ById.TryGetValue(id, out var holder))
		{
			return false;
		}

		// Unity object — ==: a destroyed MonoBehaviour compares null even though
		// the managed reference is still in the dictionary. Remove it lazily so
		// the index self-heals if OnDestroy could not run.
		if (holder == null)
		{
			ById.Remove(id);
			return false;
		}

		var candidate = holder.GetComponent<Item>();
		if (candidate == null) // Unity object — ==
		{
			ById.Remove(id);
			return false;
		}

		item = candidate!;
		return true;
	}

	private void Register()
	{
		if (_id != 0)
		{
			ById[_id] = this;
		}
	}

	private void Unregister()
	{
		if (_id == 0)
		{
			return;
		}

		// Only remove our own mapping. If another component with the same id
		// was registered after us (id reuse after a remote kill / rejoin), we
		// must not delete the current owner.
		if (ById.TryGetValue(_id, out var current) && current == this)
		{
			ById.Remove(_id);
		}
	}

	private void OnDestroy() => Unregister();
}
