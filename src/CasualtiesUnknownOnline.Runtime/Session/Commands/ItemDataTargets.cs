using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Which item one parsed target names: the one-item verdict both halves of the
/// family take, in the two vocabularies a target has — the record's ids and slots,
/// and the local scene's pointer.
///
/// <para>
/// The grammar admits every target shape to every verb; the verdict is taken HERE,
/// on the resolved set at execution (user ruling 2026-10-14), rather than by
/// narrowing the grammar. Two reasons this is its own type instead of a method on
/// the command handler: the read verb needs the same body resolution the write does,
/// and the pointer answer is a scene question whose answer is not an id at all — it
/// is one of three refusals, each with its own remedy.
/// </para>
///
/// <para>
/// The pointer target invents nothing. It reads the answer the LOCAL scene gives
/// (<see cref="ILocalItemAccess.ResolveCursorItem"/>) and refuses when that answer
/// cannot be carried as an identity: another member's item has no local id, and a
/// world-generation item deliberately has none. Minting one here would be an identity
/// nothing reports, which is what the member-report bound forbids rather than a
/// feature this target may add.
/// </para>
/// </summary>
internal sealed class ItemDataTargets(
	IKernelItemFacts items,
	ISessionControl session,
	IEntitySyncControl entities,
	ILocalItemAccess localItems)
{
	private readonly IKernelItemFacts _items = items;
	private readonly ISessionControl _session = session;
	private readonly IEntitySyncControl _entities = entities;
	private readonly ILocalItemAccess _localItems = localItems;

	/// <summary>
	/// The ONE item a write names. The read half's grammar is admitted unchanged —
	/// the verdict is taken here, on the resolved set (user ruling 2026-10-14) — but
	/// the listing shape has no meaning for a write, and a body that is not the
	/// caller's own has no local object to write into.
	/// </summary>
	internal bool TryResolveWrite(ItemDataQuery target, IReadOnlyList<ItemState> all, out ulong instanceId, out string? refusal)
	{
		instanceId = 0;
		refusal = null;
		switch (target.Kind)
		{
			case ItemDataQueryKind.InstanceId:
				if (_items.FindItem(target.InstanceId) is null)
				{
					refusal = $"No item #{target.InstanceId} is recorded in this session.";
					return false;
				}

				instanceId = target.InstanceId;
				return true;

			case ItemDataQueryKind.Slot:
				var owners = ResolveOwners(target.Selector, out refusal);
				if (refusal is not null)
				{
					return false;
				}

				if (owners.Count > 1)
				{
					refusal = $"That selector matched {owners.Count} bodies ({Owners(owners)}) — a write names ONE item, so name one body: @p, a selector with [limit=1], or the item's own id.";
					return false;
				}

				if (owners[0] != _session.LocalSteamId)
				{
					refusal = $"That item is carried by {owners[0]} — a write is the holder's own act, and no route edits another player's carried item yet.";
					return false;
				}

				var item = SlotItem(all, owners[0], target.Slot);
				if (item is null)
				{
					refusal = $"No item is recorded for {owners[0]} in slot {target.Slot} yet — a carried item joins the record on the holding client's first report of it (a use, a move or a drop).";
					return false;
				}

				instanceId = item.Value.Identity.InstanceId;
				return true;

			case ItemDataQueryKind.Cursor:
				if (!TryResolveCursor(out var underPointer, out refusal))
				{
					return false;
				}

				if (_items.FindItem(underPointer) is null)
				{
					// The scene holds an object under an id the record has no row for: writing it
					// would report the value through a route chosen without knowing where the item
					// is, which the report's own location chain decides.
					refusal = $"#{underPointer} is under the pointer but not recorded in this session yet — a write needs the row its report travels under.";
					return false;
				}

				instanceId = underPointer;
				return true;

			default:
				refusal = $"That target names every item the body carries — a write names ONE item: add 'slot <n>', or use 'cursor' or 'id <n>'. Usage: {ItemDataWriteParser.Usage}";
				return false;
		}
	}

	/// <summary>
	/// The item the pointer names, or the reason nothing can be named. The refusals are
	/// the local scene's own classification, and each says what to do instead rather
	/// than falling back to a nearby item: the pointer's answer is the game's own query's
	/// answer, and a target that supplied a different item would be editing something
	/// the operator did not point at.
	///
	/// <para>
	/// Another member's item has no answer here at all — its local form is a display
	/// proxy or an inert incarnation, which the pointer query cannot return — so naming
	/// it stays the business of the <c>id</c> and slot targets.
	/// </para>
	/// </summary>
	internal bool TryResolveCursor(out ulong instanceId, out string? refusal)
	{
		instanceId = 0;
		refusal = null;
		switch (_localItems.ResolveCursorItem(out var resolved))
		{
			case LocalCursorItemKind.Resolved:
				instanceId = resolved;
				return true;

			case LocalCursorItemKind.NoInstanceId:
				refusal = "The item under the pointer has no CUO instance id on this client yet — an id is assigned when a world item enters the item domain (the host's publish at a generation's end, or its item snapshot on a guest; a runtime item gets one as it enters). The pointer names it once that id is here.";
				return false;

			default:
				refusal = "Nothing is under the pointer — the cursor target names the item the game's own pickup query selects under the mouse, and it reads that when the command runs.";
				return false;
		}
	}

	/// <summary>
	/// Every body a target names. A selector that matches nobody is refused by
	/// name rather than answered with an empty list, because "no such player" and
	/// "that player carries nothing" are different facts to the operator.
	/// </summary>
	internal IReadOnlyList<ulong> ResolveOwners(string? selector, out string? refusal)
	{
		refusal = null;
		if (selector is null)
		{
			return [_session.LocalSteamId];
		}

		var owners = CommandPlayerTargets.Resolve(_session, _entities, selector);
		if (owners.Count == 0)
		{
			refusal = $"No players match selector '{selector}'.";
		}

		return owners;
	}

	/// <summary>The recorded row of one body's slot, or null when the record names none there.</summary>
	internal static ItemState? SlotItem(IReadOnlyList<ItemState> all, ulong owner, int slot)
	{
		foreach (var item in all)
		{
			if (item.Location.Kind == ItemLocationKind.Carried
				&& item.Location.Owner.Value == owner
				&& item.Data.SlotIndex == slot)
			{
				return item;
			}
		}

		return null;
	}

	/// <summary>A body list as an answer names it, so both halves of the family print it one way.</summary>
	internal static string Owners(IReadOnlyList<ulong> owners)
	{
		var builder = new StringBuilder();
		foreach (var owner in owners)
		{
			if (builder.Length > 0)
			{
				builder.Append(", ");
			}

			builder.Append(owner.ToString(CultureInfo.InvariantCulture));
		}

		return builder.ToString();
	}
}
