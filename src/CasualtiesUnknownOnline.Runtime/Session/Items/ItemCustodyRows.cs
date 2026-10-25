using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Items;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The one predicate that answers "does this member's own row say it carries this item", shared by every reader that used to ask the
/// arbitration's transfer cache. The cache is rebuilt from exactly the rows this reads
/// (<see cref="ItemArbitration.RebuildCarriedTableFromKernel"/> keeps the `Kind == Carried` rows), so a shared function — not an argument
/// about equivalence — is what makes the two answers the same one. Ticket <c>item-custody-single-authority</c>, stage 4.
/// </summary>
internal static class ItemCustodyRows
{
	internal static bool OwnsCarried(IReadOnlyDictionary<ulong, ItemState> rows, ulong steamId, ulong itemId) =>
		rows.TryGetValue(itemId, out var row)
		&& row.Location.Kind == ItemLocationKind.Carried
		&& row.Location.Owner.Value == steamId;
}
