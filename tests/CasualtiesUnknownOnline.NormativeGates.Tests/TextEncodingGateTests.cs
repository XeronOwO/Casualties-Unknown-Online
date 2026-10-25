using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// A rewrite that does not control its encoding turns punctuation into CJK glyphs: an em dash becomes
/// <c>U+9225</c> followed by <c>?</c>, a section sign becomes <c>U+6402</c>, a middle dot becomes <c>U+8DEF</c>,
/// and a file read as the wrong code page ends up with <c>U+FFFD</c>. The damage is invisible in a diff, survives
/// review, and has been introduced by scripted edits in this repository more than once — including into a source
/// comment and the census header — so the check is mechanical:
///
/// <list type="bullet">
/// <item>no text file carries a replacement character, anywhere;</item>
/// <item>no file in an English-only area carries one of the mangled glyphs those rewrites produce.</item>
/// </list>
///
/// <para>
/// The second rule names the four glyphs rather than "any CJK", because several areas legitimately quote Chinese:
/// the acceptance records quote a game string, the terminology table lists the Chinese form of a project word, and
/// the Chinese block is Chinese. A "<c>U+9225</c>" is never a quotation in this tree — it is a broken em dash — and
/// that is the property this gate pins.
/// </para>
/// </summary>
public sealed class TextEncodingGateTests
{
	/// <summary>Glyphs that only a mis-decoded rewrite produces; none of them belongs in this repository.</summary>
	private static readonly char[] MangledGlyphs = ['\u9225', '\u6402', '\u8DEF', '\u9382'];

	/// <summary>Areas that hold English (or, for the terminology table, English plus the recorded Chinese form).</summary>
	private static readonly string[] CheckedAreas =
	[
		"src/",
		"tests/",
		"tools/",
		"docs/design/",
		"docs/architecture/",
		"docs/development/",
		"docs/standard/",
		"docs/contracts/",
		"docs/decisions/",
		"docs/en/",
	];

	/// <summary>Directories that are not this repository's text: build output, git, local scratch, decompiled tree.</summary>
	private static readonly string[] ExcludedSegments =
	[
		"/.git/",
		"/obj/",
		"/bin/",
		"/.acceptance/",
		"/.agent-local/",
		"/reversing/",
	];

	/// <summary>Files whose glyphs are data, not prose: the pinyin table and the bilingual term table.</summary>
	private static readonly string[] DataFiles =
	[
		"src/CasualtiesUnknownOnline.PinyinSearch.Core/Search/pinyin_data.txt",
		"docs/standard/terminology.txt",
	];

	[Fact]
	public void NoTextFile_CarriesAReplacementCharacter()
	{
		var failures = TextFiles()
			.Where(file => File.ReadAllText(file, Encoding.UTF8).Contains('\uFFFD'))
			.Select(file => $"{Relative(file)}: carries U+FFFD")
			.ToList();

		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void NoCheckedArea_CarriesAMangledGlyph()
	{
		var failures = new List<string>();
		foreach (var file in TextFiles())
		{
			var relative = Relative(file);
			if (DataFiles.Contains(relative, StringComparer.Ordinal)
				|| !CheckedAreas.Any(area => relative.StartsWith(area, StringComparison.Ordinal)))
			{
				continue;
			}

			var text = File.ReadAllText(file, Encoding.UTF8);
			var glyph = text.FirstOrDefault(character => MangledGlyphs.Contains(character));
			if (glyph != default)
			{
				failures.Add($"{relative}: carries U+{(int)glyph:X4} — a rewrite lost its encoding");
			}
		}

		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void TheMatcher_SeesAMangledDash_AndAcceptsPlainEnglish()
	{
		Assert.True(CarriesMangledGlyph("a sentence " + '\u9225' + "? and more"));
		Assert.True(CarriesMangledGlyph("docs/AGENTS.md \u6402" + "6"));
		Assert.False(CarriesMangledGlyph("a sentence — and more"));
		Assert.False(CarriesMangledGlyph("the Chinese form 联机 is a quotation"));
	}

	private static bool CarriesMangledGlyph(string text) => text.Any(MangledGlyphs.Contains);

	private static string Relative(string path) =>
		Path.GetRelativePath(RepositoryPaths.Root, path).Replace('\\', '/');

	private static IEnumerable<string> TextFiles()
	{
		string[] extensions = [".md", ".cs", ".txt", ".json", ".ps1", ".csproj", ".slnx", ".yml"];
		return Directory
			.EnumerateFiles(RepositoryPaths.Root, "*", SearchOption.AllDirectories)
			.Where(path => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
			.Where(path => !ExcludedSegments.Any(segment => path.Replace('\\', '/').Contains(segment, StringComparison.Ordinal)))
			.OrderBy(path => path, StringComparer.Ordinal);
	}
}
