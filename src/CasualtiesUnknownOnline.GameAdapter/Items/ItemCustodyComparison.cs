using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The comparison at the heart of the custody projection: the kernel's judged rows against the objects this client actually has, with
/// every disagreement named. It is PURE — no Unity, no scene — so the rules are covered without a game, and the live half
/// (<see cref="ItemCustodyProjection"/>) is only the wiring that feeds it.
///
/// <para>
/// The rule it enforces is the design page's (`docs/design/model/item-custody.md`): the kernel's row is the ONLY authority on where an
/// item is, and a local object is either REAL (this client simulates it because the row is its own) or DISPLAY (a local incarnation of
/// another member's row). So the mismatches it can report are exactly these:
/// </para>
///
/// <list type="bullet">
/// <item>an object with no row at all — the object is an orphan and must be destroyed, never reported as the item dying;</item>
/// <item>a row that is not <see cref="ItemLocationKind.Terminal"/> with no object — the item should be visible somewhere and is not;</item>
/// <item>a <see cref="ItemLocationKind.Terminal"/> row that still has an object — something that is gone is still standing;</item>
/// <item>an object whose kind disagrees with its row (a display object for this client's own row, or the reverse).</item>
/// </list>
///
/// <para>
/// The kinds are decided by the OWNER the row names: a row owned by this client is real, every other row this client shows is a display.
/// That single rule is what the adapter's five stores each decided for themselves, differently, which is how the losses of 2026-10-25
/// happened.
/// </para>
/// </summary>
internal static class ItemCustodyComparison
{
	/// <summary>One local object as the projection sees it: its row id, whether the client shows it as its own or as somebody else's, and WHICH display marker the object carries (`clone-render` / `standing`, empty for a real object).</summary>
	internal readonly record struct LocalObject(ulong InstanceId, bool IsDisplay, string Marker = "");

	/// <summary>One disagreement, addressed to the id it is about so the log line can name it and the repair can act on it. The marker is carried for the mismatches an object caused, so the line says what stood for the id.</summary>
	internal readonly record struct Mismatch(ulong InstanceId, MismatchKind Kind, string Marker = "");

	internal enum MismatchKind
	{
		/// <summary>An object exists with no judged row — an orphan.</summary>
		ObjectWithoutRow,

		/// <summary>A live row (any kind but Terminal) has no object this client can show.</summary>
		RowWithoutObject,

		/// <summary>A Terminal row still has an object standing for it.</summary>
		TerminalRowWithObject,

		/// <summary>The object's kind disagrees with the row's owner (real vs display).</summary>
		KindDisagrees,
	}

	/// <summary>
	/// The expected kind for a row seen by <paramref name="localSteamId"/>. The rule the first shadow run corrected (guest log 2026-10-25
	/// 19:05, 279 `KindDisagrees` on world rows): a WORLD row is REAL on every client — a world item is simulated locally wherever the scene
	/// shows it, host or guest, so it is not another member's row incarnated. DISPLAY is only ever another member's CARRIED / CONTAINED /
	/// APPLIED row, which is what the clone renderer and the standing objects produce. The first version asked "is this client the world's
	/// simulator", which no object answers.
	/// </summary>
	internal static bool IsDisplayFor(ItemLocation location, ulong localSteamId) =>
		location.Kind switch
		{
			ItemLocationKind.Carried or ItemLocationKind.Contained or ItemLocationKind.Applied => location.Owner.Value != localSteamId,
			_ => false,
		};

	internal static IReadOnlyList<Mismatch> Compare(
		IReadOnlyDictionary<ulong, ItemState> rows,
		IReadOnlyList<LocalObject> objects,
		ulong localSteamId,
		IReadOnlyCollection<ulong>? presentOwners = null)
	{
		var mismatches = new List<Mismatch>();
		var seen = new HashSet<ulong>();

		foreach (var local in objects)
		{
			seen.Add(local.InstanceId);
			if (!rows.TryGetValue(local.InstanceId, out var row))
			{
				mismatches.Add(new Mismatch(local.InstanceId, MismatchKind.ObjectWithoutRow));
				continue;
			}

			if (row.Location.Kind == ItemLocationKind.Terminal)
			{
				mismatches.Add(new Mismatch(local.InstanceId, MismatchKind.TerminalRowWithObject));
				continue;
			}

			if (IsDisplayFor(row.Location, localSteamId) != local.IsDisplay)
			{
				mismatches.Add(new Mismatch(local.InstanceId, MismatchKind.KindDisagrees, local.Marker));
			}
		}

		foreach (var row in rows)
		{
			if (seen.Contains(row.Key))
			{
				continue;
			}

			// A row owned by a member who is NOT here is not something this client should be showing: the kernel keeps a departed member's carried
			// rows (that is where their inventory lives), while the display objects for them are destroyed when they leave. Without this the
			// projection reports every departed member's inventory forever (host log 2026-10-26 20:18: `5197989525:RowWithoutObject:Carried`,
			// `9492956821:RowWithoutObject:Carried`, `13787924117:RowWithoutObject:Carried` — all of the guest who had left).
			if (presentOwners is { } present && !IsOwnedBySomeonePresent(row.Value.Location, localSteamId, present))
			{
				continue;
			}

			if (row.Value.Location.Kind != ItemLocationKind.Terminal)
			{
				mismatches.Add(new Mismatch(row.Key, MismatchKind.RowWithoutObject));
			}
		}

		return mismatches;
	}

	/// <summary>
	/// Whether the row's owner is a client whose objects belong on this screen: this client itself, or a member in the roster. A WORLD or
	/// TERMINAL row has no member owner and is always in scope.
	/// </summary>
	internal static bool IsOwnedBySomeonePresent(ItemLocation location, ulong localSteamId, IReadOnlyCollection<ulong> presentOwners) =>
		location.Kind switch
		{
			ItemLocationKind.Carried or ItemLocationKind.Contained or ItemLocationKind.Applied =>
				location.Owner.Value == localSteamId || presentOwners.Contains(location.Owner.Value),
			_ => true,
		};

	/// <summary>
	/// The row a clone render should show in one of its owner's slots — the query that lets a READER ask the authority instead of
	/// keeping a second table. The slot is the row's own fact (`ItemData.SlotIndex`, "the owner-local slot"), the owner is the
	/// location's, and the kind says whether it is worn: a worn item is CARRIED with a negative slot index, which is why this takes
	/// the slot the caller wants rather than assuming a range.
	///
	/// <para>
	/// A Terminal row is never returned: an item that is gone from the world is gone from the render, and the caller destroys whatever
	/// object still stands for it (that is the `TerminalRowWithObject` mismatch above, repaired rather than shown).
	/// </para>
	/// </summary>
	internal static bool TryFindRowInSlot(
		IReadOnlyDictionary<ulong, ItemState> rows,
		ulong ownerSteamId,
		int slotIndex,
		out ulong instanceId)
	{
		foreach (var row in rows)
		{
			if (row.Value.Location.Kind == ItemLocationKind.Terminal
				|| row.Value.Data.SlotIndex != slotIndex
				|| row.Value.Location.Owner.Value != ownerSteamId)
			{
				continue;
			}

			instanceId = row.Key;
			return true;
		}

		instanceId = 0;
		return false;
	}
}
