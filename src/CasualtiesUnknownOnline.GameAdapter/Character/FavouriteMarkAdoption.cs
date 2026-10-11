using CasualtiesUnknownOnline.GameAdapter.Items;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The other half of requirement 3 of the local-mark ruling: a mark becomes REAL
/// when the item does. The moment a marked item is the local player's own, the
/// mark stops being a local overlay and becomes that item's own
/// <c>favourited</c> flag — its fact from then on, riding the item's own state
/// and the world save like any other item field — and the mark entry is spent so
/// there is no double state.
/// </summary>
internal static class FavouriteMarkAdoption
{
	/// <summary>
	/// Spend every mark whose item is now carried by the local body. It runs from
	/// the character capture, which is the ONE place the local carried set is
	/// enumerated on every path that can hand the player an item — a pickup, a
	/// custody transfer, a craft product, a restored character — so a path that
	/// nobody enumerated still converges within the report that follows it. A
	/// display proxy is never adopted: it is another player's item shown in this
	/// player's view, not this player's inventory.
	/// </summary>
	internal static void Adopt(Body body, ILogger log)
	{
		if (body == null || LocalFavouriteMarkStore.Current.Count == 0) // Unity object — ==
		{
			return;
		}

		// The carried set is a recursive tree (a backpack's contents are carried
		// items too), and this is the same enumeration the carried-item lookup uses
		// — direct children alone would miss an item inside a bag.
		foreach (var item in body.GetComponentsInChildren<Item>(true))
		{
			if (item == null || item.GetComponentInParent<RemoteCloneRender>() != null) // Unity objects — ==
			{
				continue;
			}

			var holder = item.GetComponent<ItemInstanceId>();
			if (holder == null || holder.Id == 0 || !LocalFavouriteMarkStore.Current.TrySpend(holder.Id, item.id)) // Unity object — ==
			{
				continue;
			}

			item.favourited = true;
			log.LogInformation("[FavouriteMark] item {Item} ({InstanceId}) is the local player's own now — the local mark became its own favourite flag.",
				item.id, holder.Id);
		}
	}
}
