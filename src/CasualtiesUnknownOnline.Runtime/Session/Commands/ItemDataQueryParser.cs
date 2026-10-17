using System;
using System.Collections.Generic;
using System.Globalization;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The item-data target grammar, as tokens after the <c>get</c> verb:
/// <list type="bullet">
/// <item>nothing — the caller's own body;</item>
/// <item><c>&lt;selector&gt;</c> — the bodies that selector names, in CUO's own
/// Minecraft-style selector vocabulary (<see cref="CommandSelectorResolver"/>);</item>
/// <item><c>slot &lt;n&gt;</c>, or <c>&lt;selector&gt; slot &lt;n&gt;</c> — the
/// item the body carries in that slot;</item>
/// <item><c>cursor</c> — the item the pointer is over on this client's own scene;</item>
/// <item><c>id &lt;n&gt;</c> — one item by its CUO instance id, wherever it is.</item>
/// </list>
/// A target is parsed, never guessed: a token that is neither a keyword nor a
/// selector is refused with the usage line instead of being read as an id, so a
/// typo cannot silently read somebody else's item.
///
/// <para>
/// One grammar serves both verbs: the read verb's target is the whole line
/// (<see cref="TryParse"/>), the write verb's is the line's first half
/// (<see cref="TryParseTarget"/>), so a new target shape cannot reach one verb and
/// miss the other.
/// </para>
/// </summary>
internal static class ItemDataQueryParser
{
	internal const string Usage = "/data get [<selector> | slot <n> | cursor | id <n>]";

	private const string IdKeyword = "id";

	private const string SlotKeyword = "slot";

	private const string CursorKeyword = "cursor";

	/// <summary>Parses the tokens that follow the verb, or reports why they are not a target. The whole list must be the target.</summary>
	internal static bool TryParse(IReadOnlyList<string> target, out ItemDataQuery query, out string? error)
	{
		if (!TryParseTarget(target, 0, out query, out var next, out error))
		{
			return false;
		}

		return next == target.Count
			|| Refuse($"Usage: {Usage}", out query, out next, out error);
	}

	/// <summary>
	/// Parses ONE target starting at <paramref name="start"/> and reports the index
	/// of the first token after it in <paramref name="next"/> — the write verb's
	/// property follows the target on the same line.
	/// </summary>
	internal static bool TryParseTarget(IReadOnlyList<string> tokens, int start, out ItemDataQuery query, out int next, out string? error)
	{
		query = new ItemDataQuery(ItemDataQueryKind.Bodies, null, -1, 0);
		next = start;
		error = null;

		var remaining = tokens.Count - start;
		if (remaining <= 0)
		{
			return true; // nothing names the caller's own body
		}

		var first = tokens[start];
		if (IsKeyword(first, IdKeyword))
		{
			return remaining >= 2 && TryParseInstanceId(tokens[start + 1], out var instanceId)
				? Accept(new ItemDataQuery(ItemDataQueryKind.InstanceId, null, -1, instanceId), start + 2, out query, out next, out error)
				: Refuse($"Usage: {Usage}", out query, out next, out error);
		}

		if (IsKeyword(first, SlotKeyword))
		{
			return remaining >= 2 && TryParseSlot(tokens[start + 1], out var slot)
				? Accept(new ItemDataQuery(ItemDataQueryKind.Slot, null, slot, 0), start + 2, out query, out next, out error)
				: Refuse($"Usage: {Usage}", out query, out next, out error);
		}

		if (IsKeyword(first, CursorKeyword))
		{
			return Accept(new ItemDataQuery(ItemDataQueryKind.Cursor, null, -1, 0), start + 1, out query, out next, out error);
		}

		if (first.Length < 2 || !first.StartsWith("@", StringComparison.Ordinal))
		{
			return Refuse($"'{first}' is not a target — a player selector is '@' plus a type, e.g. @a or @s. Usage: {Usage}", out query, out next, out error);
		}

		if (remaining >= 2 && IsKeyword(tokens[start + 1], SlotKeyword))
		{
			return remaining >= 3 && TryParseSlot(tokens[start + 2], out var selectedSlot)
				? Accept(new ItemDataQuery(ItemDataQueryKind.Slot, first, selectedSlot, 0), start + 3, out query, out next, out error)
				: Refuse($"Usage: {Usage}", out query, out next, out error);
		}

		return Accept(new ItemDataQuery(ItemDataQueryKind.Bodies, first, -1, 0), start + 1, out query, out next, out error);
	}

	/// <summary>
	/// A slot is a body slot index, which the game numbers from 0. The negative
	/// encodings the wire uses (a worn item is <c>-(limbIndex + 2)</c>) are NOT
	/// accepted here: they are a transport detail, and the limb-installed items
	/// are listed by the body form and read by their id.
	/// </summary>
	private static bool TryParseSlot(string text, out int slot) =>
		int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out slot) && slot >= 0;

	private static bool TryParseInstanceId(string text, out ulong instanceId) =>
		ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out instanceId);

	private static bool IsKeyword(string token, string keyword) =>
		string.Equals(token, keyword, StringComparison.OrdinalIgnoreCase);

	private static bool Accept(ItemDataQuery parsed, int next, out ItemDataQuery query, out int consumed, out string? error)
	{
		query = parsed;
		consumed = next;
		error = null;
		return true;
	}

	private static bool Refuse(string message, out ItemDataQuery query, out int next, out string? error)
	{
		query = new ItemDataQuery(ItemDataQueryKind.Bodies, null, -1, 0);
		next = 0;
		error = message;
		return false;
	}
}
