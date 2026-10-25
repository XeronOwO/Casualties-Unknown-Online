using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Every game fact the corpus cites must resolve: a `reversing/&lt;assembly&gt;/…/&lt;Type&gt;.cs:&lt;line&gt;` citation has to
/// name a file that exists and a line the file has. The decompiled tree is gitignored, so this gate SKIPS itself on a
/// machine that has no such tree — that is not a licence to cite from memory, it is the difference between "cannot be
/// checked here" and "checked and wrong".
///
/// <para>
/// The tree is never edited, which is why a line number is allowed there at all: it is the only stable coordinate a reader
/// can follow, and this gate is what keeps every one of them honest.
/// </para>
/// </summary>
public sealed class NativeCitationGateTests
{
	private static readonly Regex Citation = new(
		@"reversing/(?<path>[A-Za-z0-9_.\-/]+\.cs):(?<line>\d+)",
		RegexOptions.Compiled);

	[Fact]
	public void EveryGameCitationInTheDesignCorpus_Resolves()
	{
		var root = Path.Combine(RepositoryPaths.Root, "reversing");
		if (!Directory.Exists(root))
		{
			return;
		}

		var failures = new List<string>();
		foreach (var page in Directory.EnumerateFiles(Path.Combine(RepositoryPaths.Root, "docs", "design"), "*.md", SearchOption.AllDirectories))
		{
			var text = File.ReadAllText(page);
			foreach (Match match in Citation.Matches(text))
			{
				var relative = match.Groups["path"].Value;
				var line = int.Parse(match.Groups["line"].Value);
				var file = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
				if (!File.Exists(file))
				{
					failures.Add($"{Relative(page)}: `reversing/{relative}` does not exist");
					continue;
				}

				var count = File.ReadAllLines(file).Length;
				if (line < 1 || line > count)
				{
					failures.Add($"{Relative(page)}: `reversing/{relative}:{line}` is past the file's {count} lines");
				}
			}
		}

		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void TheMatcher_FindsACitation_AndIgnoresProse()
	{
		var match = Citation.Match("as `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:2475-2481` shows");

		Assert.Equal("2475", match.Groups["line"].Value);
		Assert.DoesNotMatch(@"reversing/", "a sentence with no citation");
	}

	private static string Relative(string path) =>
		Path.GetRelativePath(RepositoryPaths.Root, path).Replace('\\', '/');
}
