using System;
using System.Collections.Generic;
using System.Globalization;
using CasualtiesUnknownOnline.Runtime.Data;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The item-data verbs' grammar, after the verb word: the family's ONE target vocabulary
/// (<see cref="ItemDataQueryParser"/>) followed by the verb's own arguments. It is a static
/// seam so the read verb and the write verbs parse a target the same way, and so a malformed
/// line is refused before the session or the cheat gate is asked anything.
///
/// <para>
/// The state argument is exactly one command-line token: the console's tokenizer keeps a
/// braced group together (quotes and escapes included), so a state written with spaces is
/// still one token, and a second token after it is a usage error rather than a value silently
/// ignored.
/// </para>
/// </summary>
internal static class ItemDataVerbParser
{
	internal const string Usage =
		"/data get <target> [<path>] | /data merge <target> <snbt> | /data modify <target> <path> <set|merge|append|prepend|insert <n>> <snbt> | /data remove <target> <path> — target: [<selector>] slot <n> | cursor | id <n>";

	internal const string SetOperation = "set";

	internal const string MergeOperation = "merge";

	internal const string AppendOperation = "append";

	internal const string PrependOperation = "prepend";

	internal const string InsertOperation = "insert";

	internal const string ModifyOperation = "modify";

	internal const string RemoveOperation = "remove";

	/// <summary><c>get</c>: a target and an OPTIONAL path that ends the line.</summary>
	internal static bool TryParseGet(IReadOnlyList<string> tokens, out ItemDataGetRequest request, out string? error)
	{
		request = new ItemDataGetRequest(EmptyTarget, NbtPath.Root);
		if (!TryParseTargetAndPath(tokens, requirePath: false, endAfterPath: true, out var target, out var path, out _, out error))
		{
			return false;
		}

		request = new ItemDataGetRequest(target, path);
		return true;
	}

	/// <summary><c>merge</c>: a target and one compound; there is no path — the merge is into the state's root.</summary>
	internal static bool TryParseMerge(IReadOnlyList<string> tokens, out ItemDataMergeRequest request, out string? error)
	{
		request = new ItemDataMergeRequest(EmptyTarget, SnbtCompound.Empty);
		if (!ItemDataQueryParser.TryParseTarget(tokens, 0, out var target, out var next, out error))
		{
			return false;
		}

		if (!TryParseOne(Tail(tokens, next), out var value, out error))
		{
			return false;
		}

		if (value is not SnbtCompound compound)
		{
			error = $"merge takes a compound, e.g. {{<key>:<value>}} — got {value.Kind}. Usage: {Usage}";
			return false;
		}

		request = new ItemDataMergeRequest(target, compound);
		return true;
	}

	/// <summary>
	/// <c>modify</c>: a target, a REQUIRED path, the operation (with <c>insert</c>'s index) and
	/// the value. <c>value</c> is not spelled — there is no second source in CUO — so the state
	/// follows the operation directly.
	/// </summary>
	internal static bool TryParseModify(IReadOnlyList<string> tokens, out ItemDataModifyRequest request, out string? error)
	{
		request = new ItemDataModifyRequest(EmptyTarget, NbtPath.Root, SetOperation, 0, SnbtCompound.Empty);
		if (!TryParseTargetAndPath(tokens, requirePath: true, endAfterPath: false, out var target, out var path, out var next, out error))
		{
			return false;
		}

		if (next >= tokens.Count)
		{
			error = $"Usage: {Usage}";
			return false;
		}

		var operation = tokens[next++].ToLowerInvariant();
		var index = -1;
		if (operation == InsertOperation)
		{
			if (next >= tokens.Count
				|| !int.TryParse(tokens[next], NumberStyles.Integer, CultureInfo.InvariantCulture, out index)
				|| index < 0)
			{
				error = $"insert needs a list index (0 or more) before the value. Usage: {Usage}";
				return false;
			}

			next++;
		}
		else if (operation is not (SetOperation or MergeOperation or AppendOperation or PrependOperation))
		{
			error = $"'{operation}' is not a modify operation — the operations are set, merge, append, prepend and insert <n>. Usage: {Usage}";
			return false;
		}

		if (!TryParseOne(Tail(tokens, next), out var value, out error))
		{
			return false;
		}

		request = new ItemDataModifyRequest(target, path, operation, index, value);
		return true;
	}

	/// <summary><c>remove</c>: a target and ONE required path, and nothing after it.</summary>
	internal static bool TryParseRemove(IReadOnlyList<string> tokens, out ItemDataRemoveRequest request, out string? error)
	{
		request = new ItemDataRemoveRequest(EmptyTarget, NbtPath.Root);
		if (!TryParseTargetAndPath(tokens, requirePath: true, endAfterPath: true, out var target, out var path, out _, out error))
		{
			return false;
		}

		request = new ItemDataRemoveRequest(target, path);
		return true;
	}

	private static bool TryParseTargetAndPath(
		IReadOnlyList<string> tokens,
		bool requirePath,
		bool endAfterPath,
		out ItemDataQuery target,
		out NbtPath path,
		out int next,
		out string? error)
	{
		target = EmptyTarget;
		path = NbtPath.Root;
		error = null;
		if (!ItemDataQueryParser.TryParseTarget(tokens, 0, out target, out next, out error))
		{
			return false;
		}

		if (next >= tokens.Count)
		{
			if (!requirePath)
			{
				return true;
			}

			error = $"This verb needs a path into the item's state. Usage: {Usage}";
			return false;
		}

		if (!NbtPathParser.TryParse(tokens[next], out path, out var pathError))
		{
			error = $"The path is not an NBT path: {pathError}";
			return false;
		}

		next++;
		if (endAfterPath && next < tokens.Count)
		{
			error = $"Usage: {Usage}";
			return false;
		}

		return true;
	}

	private static bool TryParseOne(IReadOnlyList<string> tokens, out SnbtValue value, out string? error)
	{
		value = SnbtCompound.Empty;
		error = null;
		if (tokens.Count == 0)
		{
			error = $"A state is missing. Usage: {Usage}";
			return false;
		}

		if (tokens.Count > 1)
		{
			error = $"Usage: {Usage}";
			return false;
		}

		if (!SnbtParser.TryParse(tokens[0], out value, out var parseError))
		{
			error = $"The state is not SNBT: {parseError}";
			return false;
		}

		return true;
	}

	private static List<string> Tail(IReadOnlyList<string> tokens, int start)
	{
		var tail = new List<string>(Math.Max(tokens.Count - start, 0));
		for (var i = start; i < tokens.Count; i++)
		{
			tail.Add(tokens[i]);
		}

		return tail;
	}

	private static ItemDataQuery EmptyTarget => new(ItemDataQueryKind.Bodies, null, -1, 0);
}
