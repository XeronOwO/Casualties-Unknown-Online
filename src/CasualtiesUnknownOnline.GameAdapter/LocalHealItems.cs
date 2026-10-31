using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The LOCAL body's heal items: whether it carries one, and which ones the Online UI's heal list offers. Both walk the whole subtree, because a heal item inside
/// a carried container is just as requestable as one in a direct slot, and skip the render clones — those are other players' presentation, not this body's items.
/// </summary>
/// <remarks>
/// Its own type because the two questions share one scan and one exclusion rule, and because the answer is a fact about the local body's contents rather than part
/// of applying an interaction: the apply path only asks what the player can heal with.
/// </remarks>
internal static class LocalHealItems
{
	/// <summary>Whether the local body carries any heal item at all (the UI's "you cannot heal" precheck).</summary>
	internal static bool Any()
	{
		foreach (var item in LocalItems())
		{
			if (RemoteHealProfiles.IsHealItem(item.id))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Every heal item the local body carries, by instance id and item id, in the game's own child order.</summary>
	internal static IReadOnlyList<LocalHealItem> List()
	{
		var result = new List<LocalHealItem>();
		foreach (var item in LocalItems())
		{
			if (!RemoteHealProfiles.IsHealItem(item.id))
			{
				continue;
			}

			var id = item.GetComponent<ItemInstanceId>();
			if (id != null && id.Id != 0) // Unity object — ==
			{
				result.Add(new LocalHealItem(id.Id, item.id));
			}
		}

		return result;
	}

	/// <summary>The local body's own items: the whole subtree, minus anything under a render clone.</summary>
	private static IEnumerable<Item> LocalItems()
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			yield break;
		}

		foreach (var item in body.GetComponentsInChildren<Item>(true))
		{
			if (item == null || item.GetComponentInParent<RemoteCloneRender>() != null) // Unity objects — ==
			{
				continue;
			}

			yield return item;
		}
	}
}
