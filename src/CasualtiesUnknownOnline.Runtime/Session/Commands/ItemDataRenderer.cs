using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The item-data verb's text form: the item's own state as the one data language, with the
/// location and the direct contents around it.
///
/// <para>
/// Pure — the command handler owns the session, the kernel and the document, this owns
/// what the player reads — so the wording of every line is pinned by unit tests instead of
/// by a live console. The state itself is written by <see cref="SnbtWriter"/>, never by a
/// second per-property formatter, so `/data get` prints exactly what `/data merge` and
/// `/data modify` accept.
/// </para>
/// </summary>
internal static class ItemDataRenderer
{
	/// <summary>One line per item a body holds, in slot order, each with its state document.</summary>
	internal static string List(ulong owner, IReadOnlyList<ItemState> items, IReadOnlyList<SnbtValue> documents)
	{
		var builder = new StringBuilder();
		builder.Append("Items recorded for ").Append(owner).Append(" (").Append(items.Count).Append("):");
		for (var i = 0; i < items.Count; i++)
		{
			builder.Append('\n').Append(ListLine(items[i], documents[i]));
		}

		return builder.ToString();
	}

	/// <summary>One item's line in a listing: its identity, where it sits, and its state in the canonical form.</summary>
	internal static string ListLine(ItemState item, SnbtValue document) =>
		$"#{item.Identity.InstanceId} {item.Identity.DefinitionId} [{Slot(item)}] {SnbtWriter.Write(document)}";

	/// <summary>
	/// The state of one item: where it is, the revision, the state document, and its
	/// direct contents by id (a nested item is read by its own id — contents are their own
	/// rows, not part of this document).
	/// </summary>
	internal static string One(ItemState item, SnbtValue document, IReadOnlyList<ItemState> all)
	{
		var builder = new StringBuilder();
		builder.Append('#').Append(item.Identity.InstanceId)
			.Append(' ').Append(item.Identity.DefinitionId)
			.Append(" [").Append(LocationOf(item)).Append(']')
			.Append(" revision ").Append(item.Revision)
			.Append('\n').Append(SnbtWriter.Write(document));

		var contents = Children(item, all);
		if (contents.Count > 0)
		{
			builder.Append("\ncontents (").Append(contents.Count).Append("):");
			foreach (var child in contents)
			{
				builder.Append(" #").Append(child.Identity.InstanceId).Append(' ').Append(child.Identity.DefinitionId);
			}
		}

		return builder.ToString();
	}

	/// <summary>One item and one resolved path: the value in the canonical form.</summary>
	internal static string PathLine(ItemState item, NbtPath path, SnbtValue value) =>
		$"#{item.Identity.InstanceId} {item.Identity.DefinitionId}: {path.Text} = {SnbtWriter.Write(value)}";

	/// <summary>One item whose path did not resolve: the item, the path and the resolver's reason.</summary>
	internal static string PathFailure(ItemState item, NbtPath path, string reason) =>
		$"#{item.Identity.InstanceId} {item.Identity.DefinitionId}: {path.Text} — {reason}";

	/// <summary>
	/// One write command's answer: the item, one "path before -> after" clause per change,
	/// and the one consequence a condition at 0 carries. Kept here rather than built in the
	/// command so the exact wording is unit-pinned.
	/// </summary>
	internal static string Wrote(CharacterItemMsg item, IReadOnlyList<string> changes, bool awaitingHost, string? note)
	{
		var builder = new StringBuilder();
		builder.Append('#').Append(item.InstanceId).Append(' ').Append(item.ItemId).Append(": ");
		for (var i = 0; i < changes.Count; i++)
		{
			if (i > 0)
			{
				builder.Append("; ");
			}

			builder.Append(changes[i]);
		}

		builder.Append(awaitingHost
			? " — written here and reported to the host, which judges a member's write by its own rule; a refusal puts the old value back and says so in this console."
			: " — written and reported as this client's own item state.");

		if (note is not null)
		{
			builder.Append(' ').Append(note);
		}

		return builder.ToString();
	}

	/// <summary>The item's own state as one line, for a log or an answer that already names the item.</summary>
	internal static string Document(SnbtValue document) => SnbtWriter.Write(document);

	/// <summary>The short form for a listing: which slot or limb of its owner the item sits in.</summary>
	private static string Slot(ItemState item) => item.Location.Kind switch
	{
		ItemLocationKind.Carried when item.Data.SlotIndex >= 0 => $"slot {item.Data.SlotIndex}",
		ItemLocationKind.Carried => "carried",
		ItemLocationKind.Applied => $"limb {item.Location.LimbIndex}",
		_ => LocationOf(item),
	};

	/// <summary>The full form for one item: where it is, and whose it is when somebody owns it.</summary>
	internal static string LocationOf(ItemState item) => item.Location.Kind switch
	{
		ItemLocationKind.Carried when item.Data.SlotIndex >= 0 =>
			$"carried by {item.Location.Owner.Value}, slot {item.Data.SlotIndex}",
		ItemLocationKind.Carried => $"carried by {item.Location.Owner.Value}",
		ItemLocationKind.Contained => $"inside #{item.Location.ParentItemId}",
		ItemLocationKind.Applied => $"applied to {item.Location.Owner.Value}, limb {item.Location.LimbIndex}",
		ItemLocationKind.World => $"world ({Number(item.Location.X)}, {Number(item.Location.Y)})",
		ItemLocationKind.Terminal => "terminal (destroyed or consumed)",
		_ => item.Location.Kind.ToString(),
	};

	/// <summary>The items the kernel records INSIDE this one, by the parent linkage.</summary>
	private static List<ItemState> Children(ItemState item, IReadOnlyList<ItemState> all)
	{
		var children = new List<ItemState>();
		foreach (var row in all)
		{
			if (row.Location.Kind == ItemLocationKind.Contained
				&& row.Location.ParentItemId == item.Identity.InstanceId)
			{
				children.Add(row);
			}
		}

		return children;
	}

	private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
