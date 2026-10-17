using System;
using System.Collections.Generic;
using System.Globalization;
using CasualtiesUnknownOnline.Runtime.GameAdapter;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The <c>/summon</c> grammar, as tokens after the verb — Minecraft's own summon template, mapped onto the game's
/// identifiers:
///
/// <code>
/// /summon &lt;prefabId&gt; [&lt;position&gt;] [&lt;snbt&gt;]        an entity or an animal
/// /summon item &lt;itemId&gt; [&lt;position&gt;] [&lt;snbt&gt;]  a loose item entity
/// </code>
///
/// <para>
/// The TYPE WORD comes first, the shape Minecraft's command has: <c>item</c> names a loose item entity (the id is
/// then a definition from the game's own item table, and the state document is the SAME one <c>/give</c> and
/// <c>/data</c> speak), and anything else is a world-entity prefab. The two forms are kept apart rather than
/// guessed from the table, so an operator always knows which family they asked for and the adapter can refuse a
/// mismatched word by name. The position argument is the native console's own (<c>cursor</c>, <c>player</c>,
/// <c>random</c> or <c>&lt;x&gt;,&lt;y&gt;</c>; the pointer when omitted), and the state is the item's own
/// argument — exactly the document the give ladder's drop carries, which is what makes a summoned item entity and
/// a dropped one the same fact.
/// </para>
///
/// <para>
/// With the position omitted and only one trailing token, a token that starts with <c>{</c> is the state and
/// anything else is the position. Whether the game knows the prefab is the creation seam's answer, never this
/// parser's guess.
/// </para>
/// </summary>
internal static class WorldSummonParser
{
	internal const string Usage = "/summon [item] <prefabId> [<position>] [<snbt-state>]";

	/// <summary>The type word that names a loose item entity — the only type word this command has; everything else is a prefab id.</summary>
	internal const string ItemKeyword = "item";

	/// <summary>The position vocabulary, quoted by refusals so an operator reads the accepted words rather than guessing.</summary>
	internal const string PositionHelp = "cursor | player | random | <x>,<y>";

	/// <summary>Parses the tokens after the verb, or reports why they are not a summon.</summary>
	internal static bool TryParse(IReadOnlyList<string> tokens, out WorldSummonRequest request, out string? error)
	{
		request = new WorldSummonRequest("", WorldSummonKind.Entity, WorldSummonPosition.Default, "");
		error = null;

		if (tokens.Count == 0)
		{
			return Refuse($"Usage: {Usage}", out error);
		}

		var kind = WorldSummonKind.Entity;
		var next = 0;
		if (IsKeyword(tokens[0], ItemKeyword))
		{
			kind = WorldSummonKind.Item;
			next = 1;
			if (tokens.Count == 1)
			{
				return Refuse($"{ItemKeyword} names a loose item entity and needs the item definition: /summon item <itemId> [<position>] [<snbt-state>]. Usage: {Usage}", out error);
			}
		}

		var prefabId = tokens[next++];
		if (prefabId.Length == 0)
		{
			return Refuse($"No prefab id was given — name the game's own item id or prefab id, e.g. /summon bandage. Usage: {Usage}", out error);
		}

		if (prefabId.IndexOfAny(['{', '}', '[', ']']) >= 0)
		{
			// The state is its own argument here; the attached form is the shape a reader of /give would try.
			return Refuse($"The state is its own argument, not attached to the id: /summon {prefabId} [<position>] {{<key>:<value>}} — e.g. /summon item bandage player {{Condition:0.5f}}. Usage: {Usage}", out error);
		}

		var position = WorldSummonPosition.Default;
		var stateText = "";
		if (next < tokens.Count && !IsStateToken(tokens[next]))
		{
			if (!TryParsePosition(tokens[next], out position, out error))
			{
				return false;
			}

			next++;
		}

		if (next < tokens.Count)
		{
			if (!IsStateToken(tokens[next]))
			{
				return Refuse($"'{tokens[next]}' is not the state — the state is a compound, e.g. {{Condition:0.5f}}. Usage: {Usage}", out error);
			}

			if (!ItemStateText.TryParse(tokens[next], out _, out stateText, out error))
			{
				return false;
			}

			next++;
		}

		if (next < tokens.Count)
		{
			return Refuse($"Usage: {Usage}", out error);
		}

		request = new WorldSummonRequest(prefabId, kind, position, stateText);
		return true;
	}

	/// <summary>One position token: a native keyword (case-insensitive), or a literal <c>&lt;x&gt;,&lt;y&gt;</c> pair of invariant floats.</summary>
	private static bool TryParsePosition(string token, out WorldSummonPosition position, out string? error)
	{
		position = WorldSummonPosition.Default;
		error = null;

		if (IsKeyword(token, "cursor"))
		{
			position = new WorldSummonPosition(WorldSummonAnchor.Cursor, 0f, 0f);
			return true;
		}

		if (IsKeyword(token, "player"))
		{
			position = new WorldSummonPosition(WorldSummonAnchor.Player, 0f, 0f);
			return true;
		}

		if (IsKeyword(token, "random"))
		{
			position = new WorldSummonPosition(WorldSummonAnchor.Random, 0f, 0f);
			return true;
		}

		var comma = token.IndexOf(',');
		if (comma > 0
			&& float.TryParse(token.Substring(0, comma), NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
			&& float.TryParse(token.Substring(comma + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
		{
			position = new WorldSummonPosition(null, x, y);
			return true;
		}

		error = $"'{token}' is not a position — use {PositionHelp}. Usage: {Usage}";
		return false;
	}

	private static bool IsStateToken(string token) => token.StartsWith("{", StringComparison.Ordinal);

	private static bool IsKeyword(string token, string keyword) =>
		string.Equals(token, keyword, StringComparison.OrdinalIgnoreCase);

	private static bool Refuse(string message, out string? error)
	{
		error = message;
		return false;
	}
}
