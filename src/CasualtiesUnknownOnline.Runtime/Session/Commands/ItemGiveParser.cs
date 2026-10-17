using System;
using System.Collections.Generic;
using System.Globalization;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The <c>/give</c> grammar, as tokens after the verb:
/// <c>[&lt;selector&gt;] &lt;itemId&gt; [slot &lt;n&gt;] [&lt;property&gt; ...]</c>.
///
/// <para>
/// The selector is the read half's BODY vocabulary — a player selector is <c>@</c> plus
/// a type, and the resolver answers an unknown one by name at execution — because a
/// give names who RECEIVES the item rather than which item it edits. The definition is
/// the game's own item id (<c>bandage</c>, <c>largebattery</c>); whether the build knows
/// it is the creation port's answer, never this parser's guess.
/// </para>
///
/// <para>
/// The destination slot follows the definition on purpose. The read grammar spells
/// "<c>selector slot n</c>" for the item IN that slot, so letting the target parser
/// consume the selector would read <c>/give @p slot 2 bandage</c> as a slot target and
/// silently drop the operator's "slot 2" — a shape whose intent is unambiguous gets a
/// named refusal instead (the word is a destination operand only after the definition).
/// </para>
///
/// <para>
/// The properties are the write verb's own grammar, delegated to
/// <see cref="ItemDataWriteParser.TryParseProperties"/> so the words, their value
/// shapes and every refusal exist once. They are OPTIONAL: <c>/give @p bandage</c>
/// creates the definition with its prefab's own state.
/// </para>
/// </summary>
internal static class ItemGiveParser
{
	internal const string Usage = "/give [<selector>] <itemId> [slot <n>] — optional: condition <n> | favourited <bool> | liquid <liquidId> <ml> | component <TypeName> <field> <value>";

	/// <summary>The wire request's own bound: the property list is re-parsed on the host, so the token count it accepts is stated rather than unbounded.</summary>
	internal const int MaxPropertyTokens = 64;

	private const string SlotKeyword = "slot";

	private const string IdKeyword = "id";

	private const string CursorKeyword = "cursor";

	/// <summary>Parses the tokens after the verb, or reports why they are not a give.</summary>
	internal static bool TryParse(IReadOnlyList<string> tokens, out ItemGiveRequest request, out string? error)
	{
		request = new ItemGiveRequest(null, "", -1, []);
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

		var definitionId = tokens[next++];
		if (definitionId.Length == 0)
		{
			return Refuse($"Usage: {Usage}", out error);
		}

		if (IsItemTargetKeyword(definitionId))
		{
			// 'id', 'slot' and 'cursor' name an item on the read/write verbs. A give
			// names the body that RECEIVES the item and then the definition, so the word
			// is answered rather than read as a definition id the game would then fail
			// to find — or, worse, as a target that swallowed the slot the operator
			// meant.
			return Refuse($"'{definitionId}' names an item, not a definition — /give names the body that receives the item, then the item id, e.g. /give @p bandage slot 2. Usage: {Usage}", out error);
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

		var propertyTokens = new List<string>(Math.Max(tokens.Count - next, 0));
		for (var i = next; i < tokens.Count; i++)
		{
			propertyTokens.Add(tokens[i]);
		}

		request = new ItemGiveRequest(selector, definitionId, slot, propertyTokens);
		return true;
	}

	/// <summary>
	/// The property list on its own — the host re-parses the tokens a member sent,
	/// and the member parses them before sending, so both sides share one entry.
	/// The writes carry a body target that the value step never reads; only the
	/// property and its value are used when the item is created.
	/// </summary>
	internal static bool TryParseProperties(IReadOnlyList<string> tokens, out IReadOnlyList<ItemDataWrite> writes, out string? error) =>
		ItemDataWriteParser.TryParseProperties(tokens, 0, new ItemDataQuery(ItemDataQueryKind.Bodies, null, -1, 0), Usage, out writes, out error);

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
