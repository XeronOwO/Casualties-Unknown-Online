using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.GameAdapter;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The schema-driven half of the state completion: given a partial SNBT compound (the text
/// from the opening <c>{</c>) and the root shape, it reports the prefix up to the segment being
/// typed and the fragments that segment can become.
///
/// <para>
/// The scan is deliberately small: it tracks the open compounds/lists and the schema node at
/// each, treats quoted strings as opaque (so a brace inside a string cannot push a frame), and
/// stops at anything the schema does not describe. A position with no choice — a number, a
/// field the item does not carry — yields no fragments rather than an invented suggestion.
/// </para>
/// </summary>
internal static class SnbtCompletion
{
	internal static bool TryFragments(string text, ItemStateCompoundShape root, out string prefix, out IReadOnlyList<SnbtCompletionFragment> fragments)
	{
		prefix = "";
		fragments = [];
		if (text.Length == 0 || text[0] != '{')
		{
			return false;
		}

		var frames = new Stack<Frame>();
		var segmentStart = 0;
		var quote = '\0';
		var escaped = false;
		var valueJustClosed = false;
		for (var i = 0; i < text.Length; i++)
		{
			var c = text[i];
			if (quote != '\0')
			{
				if (escaped)
				{
					escaped = false;
				}
				else if (c == '\\')
				{
					escaped = true;
				}
				else if (c == quote)
				{
					quote = '\0';
				}

				continue;
			}

			if (c is '"' or '\'')
			{
				quote = c;
				continue;
			}

			switch (c)
			{
				case '{':
					if (PendingShape(frames, root) is not ItemStateCompoundShape compound)
					{
						return false;
					}

					frames.Push(new Frame(compound));
					segmentStart = i + 1;
					valueJustClosed = false;
					break;

				case '[':
					if (PendingShape(frames, root) is not ItemStateListShape list)
					{
						return false;
					}

					frames.Push(Frame.ForList(list.Element));
					segmentStart = i + 1;
					valueJustClosed = false;
					break;

				case ':':
					if (frames.Count == 0 || !frames.Peek().IsCompound)
					{
						return false;
					}

					var key = Unquote(text.Substring(segmentStart, i - segmentStart).Trim());
					var keyShape = frames.Peek().Compound!.Keys.FirstOrDefault(candidate => string.Equals(candidate.Name, key, StringComparison.Ordinal));
					if (keyShape is null)
					{
						return false;
					}

					frames.Peek().Seen.Add(keyShape.Name);
					frames.Peek().Pending = keyShape.Shape;
					segmentStart = i + 1;
					valueJustClosed = false;
					break;

				case ',':
					if (frames.Count == 0)
					{
						return false;
					}

					frames.Peek().Pending = null;
					segmentStart = i + 1;
					valueJustClosed = false;
					break;

				case '}' or ']':
					if (frames.Count == 0)
					{
						return false;
					}

					frames.Pop();
					if (frames.Count > 0)
					{
						frames.Peek().Pending = null;
					}

					segmentStart = i + 1;
					valueJustClosed = true;
					break;
			}
		}

		if (frames.Count == 0)
		{
			return false;
		}

		var frame = frames.Peek();
		var segment = text.Substring(segmentStart);
		var partial = Unquote(segment.TrimStart());
		prefix = text.Substring(0, segmentStart);

		if (frame.IsCompound)
		{
			if (frame.Pending is null)
			{
				AddKeys(frame, partial, valueJustClosed, ref fragments);
				return true;
			}

			AddValues(frame.Pending, partial, ref fragments);
			return true;
		}

		AddValues(frame.Element, partial, ref fragments);
		return true;
	}

	private static ItemStateShape? PendingShape(Stack<Frame> frames, ItemStateCompoundShape root) =>
		frames.Count == 0
			? root
			: frames.Peek().IsCompound
				? frames.Peek().Pending
				: frames.Peek().Element;

	private static void AddKeys(Frame frame, string partial, bool needsComma, ref IReadOnlyList<SnbtCompletionFragment> fragments)
	{
		var result = new List<SnbtCompletionFragment>();
		foreach (var key in frame.Compound!.Keys)
		{
			if (frame.Seen.Contains(key.Name) || !StartsWith(key.Name, partial))
			{
				continue;
			}

			result.Add(new SnbtCompletionFragment((needsComma ? "," : "") + QuoteKey(key.Name) + ":", key.Description));
		}

		fragments = result;
	}

	private static void AddValues(ItemStateShape? shape, string partial, ref IReadOnlyList<SnbtCompletionFragment> fragments)
	{
		var result = new List<SnbtCompletionFragment>();
		switch (shape)
		{
			case ItemStateCompoundShape when partial.Length == 0:
				result.Add(new SnbtCompletionFragment("{", "open the compound"));
				break;
			case ItemStateListShape when partial.Length == 0:
				result.Add(new SnbtCompletionFragment("[", "open the list"));
				break;
			case ItemStateScalarShape scalar:
				AddScalar(scalar, partial, result);
				break;
		}

		fragments = result;
	}

	private static void AddScalar(ItemStateScalarShape scalar, string partial, List<SnbtCompletionFragment> result)
	{
		if (scalar.Kind == ItemStateValueKind.Bool)
		{
			AddChoice("true", partial, result);
			AddChoice("false", partial, result);
			return;
		}

		if (scalar.Kind != ItemStateValueKind.String || scalar.Choices is null)
		{
			return;
		}

		foreach (var choice in scalar.Choices)
		{
			if (StartsWith(choice, partial))
			{
				var quoted = "\"" + choice.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
				result.Add(new SnbtCompletionFragment(quoted, choice));
			}
		}
	}

	private static void AddChoice(string fragment, string partial, List<SnbtCompletionFragment> result)
	{
		if (StartsWith(fragment, partial))
		{
			result.Add(new SnbtCompletionFragment(fragment, fragment));
		}
	}

	private static bool StartsWith(string candidate, string partial) =>
		partial.Length == 0 || candidate.StartsWith(partial, StringComparison.OrdinalIgnoreCase);

	private static string QuoteKey(string key) => SnbtWriter.KeyNeedsQuotes(key) ? "\"" + key + "\"" : key;

	private static string Unquote(string text) =>
		text.Length > 0 && text[0] is '"' or '\''
			? text.Substring(1).TrimEnd(text[0])
			: text;

	private sealed class Frame(ItemStateCompoundShape? compound)
	{
		internal ItemStateCompoundShape? Compound { get; } = compound;

		internal ItemStateShape? Element { get; private init; }

		internal HashSet<string> Seen { get; } = [with(StringComparer.Ordinal)];

		internal ItemStateShape? Pending { get; set; }

		internal bool IsCompound => Compound is not null;

		internal static Frame ForList(ItemStateShape element) => new((ItemStateCompoundShape?)null) { Element = element };
	}
}
