using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// One row of a destroyed thing's OWN drop table — the unit a reported drop is
/// bounded against, whichever table it came from: the game's block roll
/// (<c>WorldGeneration.DamageBlock</c>, one switch arm per block id), a CUO
/// custom tile's authored drops (<c>IModTileDefinition.Drops</c>), or a building
/// entity's own prefab data (<c>BuildingEntity.itemsDropOnDestroy</c> /
/// <c>alwaysDrop</c> / <c>guaranteedDropAmount</c>).
/// <para>
/// A row is one ROLL. The game rolls each row at most once — a chance-gated row
/// (<c>Random.value &lt; 0.5f</c>) produces 0 or 1 item, an ungated one exactly 1
/// — so a row accounts for at most ONE reported drop. <see cref="ItemIds"/> is
/// plural because ONE roll can name several outcomes: the wood arm asks
/// <c>Random.Range(0, 3)</c> and creates woodscraps OR stick OR woodpanel, so
/// that arm is one row naming three ids, never three rows. Spelled as three
/// rows it would accept a report claiming all three items from a single break.
/// </para>
/// <para>
/// <see cref="MinCondition"/> / <see cref="MaxCondition"/> carry the condition
/// window when the row's own roll declares one (the game's
/// <c>item.condition = Random.Range(0.05f, 0.2f)</c>, or a custom drop's
/// authored window). A row the table leaves at the item's own default declares
/// none: nothing about that condition is verifiable, so nothing is checked.
/// </para>
/// </summary>
internal sealed record DropBound(IReadOnlyList<string> ItemIds, float? MinCondition = null, float? MaxCondition = null)
{
	/// <summary>True when this row's single roll can produce exactly this item at exactly this condition.</summary>
	internal bool AccountsFor(string itemId, float condition, float slack) =>
		Names(itemId)
		&& (MinCondition is not { } min || condition >= min - slack)
		&& (MaxCondition is not { } max || condition <= max + slack);

	private bool Names(string itemId)
	{
		foreach (var id in ItemIds)
		{
			if (string.Equals(id, itemId, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}
}
