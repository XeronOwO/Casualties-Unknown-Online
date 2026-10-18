using System;
using System.Collections.Generic;
using System.Globalization;
using CasualtiesUnknownOnline.Runtime.Data;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The <c>/give</c> grammar, as tokens after the verb:
/// <c>[&lt;selector&gt;] &lt;itemId&gt;[{&lt;snbt&gt;}] [slot &lt;n&gt;]</c>.
///
/// <para>
/// The state is ATTACHED to the item id (decision 264), the shape the game's own save and
/// Minecraft's item argument have: <c>/give @p waterbottle{Liquids:[…],Condition:0.8}</c>.
/// The item id stays free text — whether the game knows it is the creation port's answer,
/// never this parser's guess — and only the part before the first <c>{</c> is the id.
/// </para>
///
/// <para>
/// The state must be a compound: it is overlaid onto the definition's captured state, so a
/// bare value or a list has nothing to overlay onto. A second token after the state is a
/// usage error rather than a value silently ignored, and the same rule applies to the
/// optional destination slot, which follows the state.
/// </para>
/// </summary>
internal static class ItemGiveParser
{
	internal const string Usage = "/give [<selector>] <itemId>[{<snbt>}] [slot <n>]";

	/// <summary>The wire request's own bound — the one the creation verbs' shared state parser states (<see cref="ItemStateText.MaxLength"/>).</summary>
	internal const int MaxStateLength = ItemStateText.MaxLength;

	private const string SlotKeyword = "slot";

	private const string IdKeyword = "id";

	private const string CursorKeyword = "cursor";

	/// <summary>Parses the tokens after the verb, or reports why they are not a give.</summary>
	internal static bool TryParse(IReadOnlyList<string> tokens, out ItemGiveRequest request, out string? error)
	{
		request = new ItemGiveRequest(null, "", -1, "");
		error = null;

		var next = 0;
		string? selector = null;
		if (tokens.Count > 0 && tokens[0].StartsWith("@", StringComparison.Ordinal))
		{
			selector = tokens[0];
			next = 1;
		}

		if (next >= tokens.Count)
		{
			return Refuse($"Usage: {Usage}", out error);
		}

		var token = tokens[next++];
		var brace = token.IndexOf('{');
		var definitionId = brace < 0 ? token : token.Substring(0, brace);
		var stateText = brace < 0 ? null : token.Substring(brace);
		if (definitionId.Length == 0)
		{
			return Refuse($"No item id was given — name the game's own item id, e.g. /give @p bandage. Usage: {Usage}", out error);
		}

		if (definitionId.IndexOfAny(['[', ']', '}']) >= 0)
		{
			return Refuse($"the state attaches to the item id with '{{', e.g. /give @p <itemId>{{<key>:<value>}} — '{definitionId}' is not an item id. Usage: {Usage}", out error);
		}

		if (IsItemTargetKeyword(definitionId))
		{
			// 'id', 'slot' and 'cursor' name an item on the read/write verbs. A give names the
			// body that RECEIVES the item and then the definition, so the word is answered
			// rather than read as a definition id the game would fail to find.
			return Refuse($"'{definitionId}' names an item, not a definition — /give names the body that receives the item, then the item id, e.g. /give @p bandage slot 2. Usage: {Usage}", out error);
		}

		if (!TryParseState(stateText, out _, out var canonical, out error))
		{
			return false;
		}

		var slot = -1;
		if (next < tokens.Count && IsKeyword(tokens[next], SlotKeyword))
		{
			if (next + 1 >= tokens.Count
				|| !int.TryParse(tokens[next + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out slot)
				|| slot < 0)
			{
				return Refuse($"slot names a body slot index (0 or more). Usage: {Usage}", out error);
			}

			next += 2;
		}

		if (next < tokens.Count)
		{
			return Refuse($"Usage: {Usage}", out error);
		}

		request = new ItemGiveRequest(selector, definitionId, slot, canonical);
		return true;
	}

	/// <summary>
	/// Parses one state argument — the text attached to the item id here, or the text a
	/// member's request carried, which the host re-parses. Null or empty means "the
	/// definition's own captured state"; anything else must be a compound.
	/// <para>
	/// The rule itself is <see cref="ItemStateText"/>, shared with <c>/summon</c>: one
	/// parse, one canonical form, one length bound across the family's creation verbs.
	/// </para>
	/// </summary>
	internal static bool TryParseState(string? text, out SnbtCompound? state, out string canonical, out string? error) =>
		ItemStateText.TryParse(text, out state, out canonical, out error);

	private static bool IsItemTargetKeyword(string token) =>
		IsKeyword(token, IdKeyword) || IsKeyword(token, SlotKeyword) || IsKeyword(token, CursorKeyword);

	private static bool IsKeyword(string token, string keyword) =>
		string.Equals(token, keyword, StringComparison.OrdinalIgnoreCase);

	private static bool Refuse(string message, out string? error)
	{
		error = message;
		return false;
	}
}
