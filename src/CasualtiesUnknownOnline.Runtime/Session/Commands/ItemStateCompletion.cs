using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.GameAdapter;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The console completion for the item state language: it parses the SNBT prefix the operator
/// has typed, walks the ADAPTER's schema to the position being edited, and offers what that
/// position can carry — top-level keys, component type names, component fields, booleans and
/// the game's liquid ids.
///
/// <para>
/// It is a contextual source, not an argument-kind one: which schema applies depends on the
/// command, the target and the definition already typed, so <see cref="CommandConsoleService"/>
/// asks this before the per-kind fallback. Every suggestion is the WHOLE token the console
/// should hold after accepting it (the console replaces one token per suggestion), inserted
/// verbatim — the state's own quoted strings must not be re-quoted by the token inserter.
/// </para>
///
/// <para>
/// Where no schema can be named — a target still being typed, a selector naming several bodies,
/// a definition the build does not know — it answers nothing rather than guessing, and the
/// ordinary argument completion takes over.
/// </para>
/// </summary>
internal sealed class ItemStateCompletion(
	IKernelItemFacts items,
	IItemStateLanguage language,
	ItemDataTargets targets)
{
	private readonly IKernelItemFacts _items = items;
	private readonly IItemStateLanguage _language = language;
	private readonly ItemDataTargets _targets = targets;

	/// <summary>Suggestions for the token under the caret, or false when this is not a state the item family can complete.</summary>
	internal bool TrySuggest(
		string command,
		IReadOnlyList<CommandLineTokenizer.Token> tokens,
		CommandLineTokenizer.Token current,
		out IReadOnlyList<CommandSuggestion> suggestions)
	{
		suggestions = [];
		if (current.Length == 0)
		{
			return false;
		}

		var text = current.Unquoted;
		if (text.IndexOf('{') < 0)
		{
			return false;
		}

		if (string.Equals(command, "give", StringComparison.OrdinalIgnoreCase))
		{
			return TryGive(tokens, current, text, out suggestions);
		}

		if (!string.Equals(command, "data", StringComparison.OrdinalIgnoreCase) || tokens.Count < 2)
		{
			return false;
		}

		return tokens[1].Unquoted.ToLowerInvariant() switch
		{
			"merge" => TryDataState(tokens, current, text, 2, out suggestions),
			"modify" => TryDataState(tokens, current, text, 2, out suggestions),
			_ => false,
		};
	}

	private bool TryGive(IReadOnlyList<CommandLineTokenizer.Token> tokens, CommandLineTokenizer.Token current, string text, out IReadOnlyList<CommandSuggestion> suggestions)
	{
		suggestions = [];
		var itemArgument = tokens.Count > 1 && tokens[1].Unquoted.StartsWith("@", StringComparison.Ordinal) ? 2 : 1;
		if (itemArgument >= tokens.Count || tokens[itemArgument].Start != current.Start)
		{
			return false;
		}

		var brace = text.IndexOf('{');
		var definitionId = text.Substring(0, brace);
		var schema = _language.DescribeDefinitionSchema(definitionId);
		if (schema is null)
		{
			return false;
		}

		return SnbtCompletion.TryFragments(text.Substring(brace), schema.Root, out var prefix, out var fragments)
			&& ToSuggestions(definitionId + prefix, fragments, out suggestions);
	}

	/// <summary>
	/// A <c>/data merge</c> or <c>/data modify</c> state argument: the target tokens precede it,
	/// and the adapter schema comes from the item the target names. The path/operation between
	/// the target and the state is not interpreted — the state is the last token and carries its
	/// own prefix.
	/// </summary>
	private bool TryDataState(
		IReadOnlyList<CommandLineTokenizer.Token> tokens,
		CommandLineTokenizer.Token current,
		string text,
		int argumentStart,
		out IReadOnlyList<CommandSuggestion> suggestions)
	{
		suggestions = [];
		if (tokens[tokens.Count - 1].Start != current.Start)
		{
			return false;
		}

		var args = new List<string>(tokens.Count - argumentStart);
		for (var i = argumentStart; i < tokens.Count; i++)
		{
			args.Add(tokens[i].Unquoted);
		}

		if (!ItemDataQueryParser.TryParseTarget(args, 0, out var target, out var next, out _) || next == 0)
		{
			return false;
		}

		var schema = SchemaFor(target);
		if (schema is null)
		{
			return false;
		}

		// The state token's schema depends on WHERE it lands: merge edits the root, while
		// modify's value takes the shape at the path (and the list's ELEMENT shape for the
		// list operations). Walking the root there would offer keys the adapter refuses.
		var root = ShapeFor(tokens[1].Unquoted, args, next, schema.Root) as ItemStateCompoundShape;
		if (root is null)
		{
			return false;
		}

		var brace = text.IndexOf('{');
		return SnbtCompletion.TryFragments(text.Substring(brace), root, out var prefix, out var fragments)
			&& ToSuggestions(prefix, fragments, out suggestions);
	}

	/// <summary>The shape the state token is being typed against: the root for merge, the path's shape (or its list element) for modify; null when no choice applies.</summary>
	private static ItemStateShape? ShapeFor(string verb, IReadOnlyList<string> args, int next, ItemStateCompoundShape schemaRoot)
	{
		if (string.Equals(verb, "merge", StringComparison.OrdinalIgnoreCase))
		{
			return schemaRoot;
		}

		if (!string.Equals(verb, "modify", StringComparison.OrdinalIgnoreCase)
			|| next + 1 >= args.Count
			|| !NbtPathParser.TryParse(args[next], out var path, out _))
		{
			return null;
		}

		var shape = ShapeAt(schemaRoot, path);
		var operation = args[next + 1].ToLowerInvariant();
		if (operation is "append" or "prepend" or "insert")
		{
			shape = (shape as ItemStateListShape)?.Element;
		}

		return shape;
	}

	/// <summary>Walks the schema to the shape a path addresses; null when the path leaves the schema.</summary>
	private static ItemStateShape? ShapeAt(ItemStateShape? shape, NbtPath path)
	{
		foreach (var segment in path.Segments)
		{
			if (shape is null)
			{
				return null;
			}

			shape = segment switch
			{
				NbtPathKey key when shape is ItemStateCompoundShape compound =>
					compound.Keys.FirstOrDefault(candidate => string.Equals(candidate.Name, key.Key, StringComparison.Ordinal))?.Shape,
				NbtPathIndex when shape is ItemStateListShape list => list.Element,
				_ => null,
			};
		}

		return shape;
	}

	private ItemStateSchema? SchemaFor(ItemDataQuery target)
	{
		switch (target.Kind)
		{
			case ItemDataQueryKind.InstanceId:
				return _items.FindItem(target.InstanceId) is { } row ? _language.DescribeSchema(row.Data) : null;

			case ItemDataQueryKind.Cursor:
				return _targets.TryResolveCursor(out var cursor, out _) && _items.FindItem(cursor) is { } cursorRow
					? _language.DescribeSchema(cursorRow.Data)
					: null;

			case ItemDataQueryKind.Slot:
				var owners = _targets.ResolveOwners(target.Selector, out _);
				if (owners.Count != 1)
				{
					return null;
				}

				var all = new List<ItemState>(_items.QueryItems().Values);
				return ItemDataTargets.SlotItem(all, owners[0], target.Slot) is { } slotItem
					? _language.DescribeSchema(slotItem.Data)
					: null;

			default:
				return null;
		}
	}

	private static bool ToSuggestions(string prefix, IReadOnlyList<SnbtCompletionFragment> fragments, out IReadOnlyList<CommandSuggestion> suggestions)
	{
		suggestions = [];
		if (fragments.Count == 0)
		{
			return false;
		}

		var result = new List<CommandSuggestion>(fragments.Count);
		foreach (var fragment in fragments)
		{
			result.Add(new CommandSuggestion(prefix + fragment.Text, fragment.Description, Verbatim: true));
		}

		suggestions = result;
		return true;
	}
}
