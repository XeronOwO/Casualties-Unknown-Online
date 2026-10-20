using CasualtiesUnknownOnline.GameState.Domains.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The attribution and retirement of the carried rows a craft report destroyed: WHO a
/// report's id belongs to (the anchor a Destroyed entry may be acted on with), and how the
/// HOST retires its own authority row for a row its own craft consumed. Kept apart from
/// <see cref="CraftSyncService"/> because it is the one place the "a report may not name
/// another member's item" rule is implemented, and because the line gate demanded the split.
/// </summary>
internal sealed class CraftCarriedRowRetirement(ISessionControl session, ItemKernelAuthority kernel, ILogger log)
{
	/// <summary>
	/// True when this host's kernel row says the item is carried or contained by
	/// <paramref name="owner"/> — the data anchor that attributes an untracked Destroyed entry
	/// to the report's sender when the transfer registration is the lost half. A terminal,
	/// world, applied or somebody-else's row is not the sender's.
	/// </summary>
	internal bool IsOwnedBy(ulong itemId, ulong owner)
	{
		if (kernel.FindItem(itemId) is not { } state)
		{
			return false;
		}

		return state.Location.Kind is ItemLocationKind.Carried or ItemLocationKind.Contained
			&& state.Location.Owner.Value == owner;
	}

	/// <summary>
	/// Retire the HOST's own authority row for a carried item its craft destroyed. A missing row
	/// is not an error (the item may never have entered the record: a floor item consumed straight
	/// off the ground, or a generated item the host never registered) — it is named at Information,
	/// not warned, because the scene consumed it either way and there is nothing left to align.
	/// </summary>
	internal void RetireOwnRow(ulong itemId)
	{
		if (kernel.TryDestroy(session.LocalSteamId, itemId, TerminalKind.Destroyed, out _, out var rejection))
		{
			return;
		}

		log.LogInformation("[Crafting] the host's own craft destroyed carried item {ItemId}, but its authority row was not there to retire ({Reason}).",
			itemId, rejection?.Reason);
	}
}
