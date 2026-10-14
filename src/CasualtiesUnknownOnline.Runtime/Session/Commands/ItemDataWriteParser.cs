using System;
using System.Collections.Generic;
using System.Globalization;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The item-data write grammar, as tokens after the <c>set</c> verb: one of the
/// read half's target shapes followed by the property and its value —
/// <c>condition &lt;n&gt;</c>, <c>favourited &lt;bool&gt;</c>,
/// <c>liquid &lt;liquidId&gt; &lt;ml&gt;</c> or
/// <c>component &lt;TypeName&gt; &lt;field&gt; &lt;value&gt;</c>.
///
/// <para>
/// The target is parsed by <see cref="ItemDataQueryParser"/> — the SAME grammar the
/// read verb uses — so both verbs admit one selector vocabulary, and the property is
/// a closed keyword set: an unknown word is refused with the usage line rather than
/// guessed at, so a typo cannot write a property the operator did not name. The
/// property NAMES are the ones the read half prints, so an operator sets what the
/// verb just showed.
/// </para>
/// </summary>
internal static class ItemDataWriteParser
{
	internal const string Usage = "/data set [<selector>] slot <n> | id <n> — condition <n> | favourited <bool> | liquid <liquidId> <ml> | component <TypeName> <field> <value>";

	private const string ConditionKeyword = "condition";

	private const string FavouritedKeyword = "favourited";

	private const string LiquidKeyword = "liquid";

	private const string ComponentKeyword = "component";

	/// <summary>Parses the tokens that follow the verb, or reports why they are not a write.</summary>
	internal static bool TryParse(IReadOnlyList<string> tokens, out ItemDataWrite write, out string? error)
	{
		write = new ItemDataWrite(new ItemDataQuery(ItemDataQueryKind.Bodies, null, -1, 0), ItemDataProperty.Condition, 0f, false, "", "", "");
		error = null;

		if (!ItemDataQueryParser.TryParseTarget(tokens, 0, out var target, out var next, out error))
		{
			return false;
		}

		if (next >= tokens.Count)
		{
			return Refuse($"Usage: {Usage}", out write, out error);
		}

		var property = tokens[next];
		var rest = tokens.Count - next - 1;
		if (IsKeyword(property, ConditionKeyword))
		{
			return rest == 1 && TryParseNumber(tokens[next + 1], out var condition)
				? Accept(new ItemDataWrite(target, ItemDataProperty.Condition, condition, false, "", "", ""), out write, out error)
				: RefuseNumber(tokens, next, rest, out write, out error);
		}

		if (IsKeyword(property, FavouritedKeyword))
		{
			return rest == 1 && TryParseFlag(tokens[next + 1], out var favourited)
				? Accept(new ItemDataWrite(target, ItemDataProperty.Favourited, 0f, favourited, "", "", ""), out write, out error)
				: RefuseFlag(tokens, next, rest, out write, out error);
		}

		if (IsKeyword(property, LiquidKeyword))
		{
			return rest == 2 && TryParseNumber(tokens[next + 2], out var millilitres)
				? Accept(new ItemDataWrite(target, ItemDataProperty.Liquid, millilitres, false, tokens[next + 1], "", ""), out write, out error)
				: RefuseNumber(tokens, next, rest, out write, out error);
		}

		if (IsKeyword(property, ComponentKeyword))
		{
			return rest == 3
				? Accept(new ItemDataWrite(target, ItemDataProperty.Component, 0f, false, tokens[next + 2], tokens[next + 1], tokens[next + 3]), out write, out error)
				: Refuse($"Usage: {Usage}", out write, out error);
		}

		return Refuse($"'{property}' is not a property to write — write condition, favourited, liquid or component. Usage: {Usage}", out write, out error);
	}

	/// <summary>
	/// A number is an invariant-culture float, and it must be FINITE: the parser
	/// accepts the words "NaN" and "Infinity", and a condition or a millilitre
	/// count that is neither would travel into the record as poison.
	/// </summary>
	private static bool TryParseNumber(string text, out float number) =>
		float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
		&& !float.IsNaN(number)
		&& !float.IsInfinity(number);

	/// <summary>What the operator reads for a flag is what they may write back: the read half prints "yes"/"no", a component field prints "true"/"false".</summary>
	private static bool TryParseFlag(string text, out bool flag)
	{
		if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(text, "on", StringComparison.OrdinalIgnoreCase))
		{
			flag = true;
			return true;
		}

		if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(text, "no", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(text, "off", StringComparison.OrdinalIgnoreCase))
		{
			flag = false;
			return true;
		}

		flag = false;
		return false;
	}

	private static bool RefuseNumber(IReadOnlyList<string> tokens, int next, int rest, out ItemDataWrite write, out string? error)
	{
		if (rest >= 1 && !TryParseNumber(tokens[next + 1], out _))
		{
			return Refuse($"'{tokens[next + 1]}' is not a number. Usage: {Usage}", out write, out error);
		}

		if (rest >= 2 && !TryParseNumber(tokens[next + 2], out _))
		{
			return Refuse($"'{tokens[next + 2]}' is not a number of millilitres. Usage: {Usage}", out write, out error);
		}

		return Refuse($"Usage: {Usage}", out write, out error);
	}

	private static bool RefuseFlag(IReadOnlyList<string> tokens, int next, int rest, out ItemDataWrite write, out string? error) =>
		rest >= 1
			? Refuse($"'{tokens[next + 1]}' is not a yes/no value — write true, false, yes or no. Usage: {Usage}", out write, out error)
			: Refuse($"Usage: {Usage}", out write, out error);

	private static bool IsKeyword(string token, string keyword) =>
		string.Equals(token, keyword, StringComparison.OrdinalIgnoreCase);

	private static bool Accept(ItemDataWrite parsed, out ItemDataWrite write, out string? error)
	{
		write = parsed;
		error = null;
		return true;
	}

	private static bool Refuse(string message, out ItemDataWrite write, out string? error)
	{
		write = new ItemDataWrite(new ItemDataQuery(ItemDataQueryKind.Bodies, null, -1, 0), ItemDataProperty.Condition, 0f, false, "", "", "");
		error = message;
		return false;
	}
}
