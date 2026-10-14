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
/// <item><c>id &lt;n&gt;</c> — one item by its CUO instance id, wherever it is.</item>
/// </list>
/// A target is parsed, never guessed: a token that is neither a keyword nor a
/// selector is refused with the usage line instead of being read as an id, so a
/// typo cannot silently read somebody else's item.
/// </summary>
internal static class ItemDataQueryParser
{
	internal const string Usage = "/data get [<selector> | slot <n> | id <n>]";

	private const string IdKeyword = "id";

	private const string SlotKeyword = "slot";

	/// <summary>Parses the tokens that follow the verb, or reports why they are not a target.</summary>
	internal static bool TryParse(IReadOnlyList<string> target, out ItemDataQuery query, out string? error)
	{
		query = new ItemDataQuery(ItemDataQueryKind.Bodies, null, -1, 0);
		error = null;

		if (target.Count == 0)
		{
			return true;
		}

		var first = target[0];
		if (IsKeyword(first, IdKeyword))
		{
			return TryParseInstanceId(target, out query, out error);
		}

		if (IsKeyword(first, SlotKeyword))
		{
			return target.Count == 2 && TryParseSlot(target[1], out var slot)
				? Accept(new ItemDataQuery(ItemDataQueryKind.Slot, null, slot, 0), out query, out error)
				: Refuse($"Usage: {Usage}", out query, out error);
		}

		if (first.Length < 2 || !first.StartsWith("@", StringComparison.Ordinal))
		{
			return Refuse($"'{first}' is not a target — a player selector is '@' plus a type, e.g. @a or @s. Usage: {Usage}", out query, out error);
		}

		return target.Count switch
		{
			1 => Accept(new ItemDataQuery(ItemDataQueryKind.Bodies, first, -1, 0), out query, out error),
			3 when IsKeyword(target[1], SlotKeyword) && TryParseSlot(target[2], out var slot) =>
				Accept(new ItemDataQuery(ItemDataQueryKind.Slot, first, slot, 0), out query, out error),
			_ => Refuse($"Usage: {Usage}", out query, out error),
		};
	}

	/// <summary>
	/// A slot is a body slot index, which the game numbers from 0. The negative
	/// encodings the wire uses (a worn item is <c>-(limbIndex + 2)</c>) are NOT
	/// accepted here: they are a transport detail, and the limb-installed items
	/// are listed by the body form and read by their id.
	/// </summary>
	private static bool TryParseSlot(string text, out int slot) =>
		int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out slot) && slot >= 0;

	private static bool TryParseInstanceId(IReadOnlyList<string> target, out ItemDataQuery query, out string? error)
	{
		if (target.Count == 2
			&& ulong.TryParse(target[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var instanceId))
		{
			return Accept(new ItemDataQuery(ItemDataQueryKind.InstanceId, null, -1, instanceId), out query, out error);
		}

		return Refuse($"Usage: {Usage}", out query, out error);
	}

	private static bool IsKeyword(string token, string keyword) =>
		string.Equals(token, keyword, StringComparison.OrdinalIgnoreCase);

	private static bool Accept(ItemDataQuery parsed, out ItemDataQuery query, out string? error)
	{
		query = parsed;
		error = null;
		return true;
	}

	private static bool Refuse(string message, out ItemDataQuery query, out string? error)
	{
		query = new ItemDataQuery(ItemDataQueryKind.Bodies, null, -1, 0);
		error = message;
		return false;
	}
}
