using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Text scanning shared by the census gates. <see cref="Flatten"/> replaces comments with a space
/// while keeping string and character literals — and the newlines around them — intact, because a
/// census counts READS: a key that survives only in a comment, a log literal or prose may neither
/// satisfy nor break it. <see cref="MemberCallArguments"/> resolves a member-access call to its
/// argument text, balanced, so <c>ctx.T(flag ? "a.b" : "c.d")</c> is one call site whose two
/// literals are both reads.
/// </summary>
internal static class SourceScan
{
	/// <summary>A catalogue key: a lowercase dotted identifier such as <c>home.steam_id</c>, or the
	/// single-segment <c>launcher</c>.</summary>
	internal static readonly Regex KeyShape = new(@"^[a-z][a-z0-9_]*(?:\.[a-z0-9_]+)*$", RegexOptions.Compiled);

	/// <summary>The fragment an interpolated key starts with, dot included: <c>prefs.color.</c>.</summary>
	internal static readonly Regex KeyPrefixShape = new(@"^[a-z][a-z0-9_]*(?:\.[a-z0-9_]+)*\.$", RegexOptions.Compiled);

	/// <summary>Every <c>*.cs</c> file under <paramref name="root"/>, build output excluded, ordinal order.</summary>
	internal static string[] SourceFiles(string root) =>
		[.. Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
			.Where(path => !IsBuildOutput(path))
			.OrderBy(path => path, StringComparer.Ordinal)];

	internal static bool IsBuildOutput(string path) =>
		path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
		|| path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

	internal static string ReadAll(IEnumerable<string> paths) =>
		string.Join("\n", paths.Select(File.ReadAllText));

	/// <summary>
	/// The text with line and block comments replaced by a space. String and character literals are
	/// copied verbatim, newlines are kept, so a method boundary stays findable after flattening.
	/// </summary>
	internal static string Flatten(string text)
	{
		var kept = new StringBuilder(text.Length);
		for (var i = 0; i < text.Length; i++)
		{
			var current = text[i];

			if (current == '"' || current == '\'')
			{
				var end = LiteralEnd(text, i);
				kept.Append(text, i, end - i);
				i = end - 1;
				continue;
			}

			if (current == '/' && i + 1 < text.Length && text[i + 1] == '/')
			{
				while (i < text.Length && text[i] != '\n')
				{
					i++;
				}

				kept.Append('\n');
				continue;
			}

			if (current == '/' && i + 1 < text.Length && text[i + 1] == '*')
			{
				for (i += 2; i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/'); i++)
				{
				}

				i++;
				kept.Append(' ');
				continue;
			}

			kept.Append(current);
		}

		return kept.ToString();
	}

	/// <summary>
	/// The argument text of every call to one of <paramref name="accessors"/>, written either as a
	/// member access (<c>ctx.T(…)</c>) or as a bare call (<c>T(…)</c>): the call's argument list,
	/// balanced, nested parentheses kept. A declaration's parameter list matches too
	/// (<c>string T(string key)</c>) and is returned like a call — harmless, because it carries no
	/// literal: the census counts what it can resolve, not what it can name.
	/// </summary>
	internal static List<string> MemberCallArguments(string text, params string[] accessors)
	{
		var pattern = new Regex(@"\.(?:" + string.Join("|", accessors.Select(Regex.Escape)) + @")\(|\b(?:" + string.Join("|", accessors.Select(Regex.Escape)) + @")\(", RegexOptions.Compiled);
		var found = new List<string>();
		foreach (Match match in pattern.Matches(text))
		{
			found.Add(ArgumentList(text, match.Index + match.Length - 1));
		}

		return found;
	}

	/// <summary>The balanced argument text of the call whose opening parenthesis is at
	/// <paramref name="openIndex"/>, parentheses inside string literals ignored.</summary>
	internal static string ArgumentList(string text, int openIndex)
	{
		var depth = 0;
		for (var i = openIndex; i < text.Length; i++)
		{
			var current = text[i];
			if (current == '"' || current == '\'')
			{
				i = LiteralEnd(text, i) - 1;
				continue;
			}

			if (current == '(')
			{
				depth++;
				continue;
			}

			if (current == ')')
			{
				depth--;
				if (depth == 0)
				{
					return text.Substring(openIndex + 1, i - openIndex - 1);
				}
			}
		}

		return text.Substring(openIndex + 1);
	}

	/// <summary>The index just past the literal that starts at <paramref name="start"/> (its closing
	/// quote), honoring backslash escapes and the verbatim <c>@"…"</c> doubling.</summary>
	internal static int LiteralEnd(string text, int start)
	{
		var quote = text[start];
		var verbatim = quote == '"' && start > 0 && text[start - 1] == '@';
		for (var i = start + 1; i < text.Length; i++)
		{
			if (verbatim && text[i] == '"')
			{
				if (i + 1 < text.Length && text[i + 1] == '"')
				{
					i++;
					continue;
				}

				return i + 1;
			}

			if (!verbatim && text[i] == '\\' && i + 1 < text.Length)
			{
				i++;
				continue;
			}

			if (text[i] == quote)
			{
				return i + 1;
			}
		}

		return text.Length;
	}
}
