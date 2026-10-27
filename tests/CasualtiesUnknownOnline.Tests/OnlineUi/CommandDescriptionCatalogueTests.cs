using System;
using System.Collections.Generic;
using System.IO;
using CasualtiesUnknownOnline.Runtime.Localization;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The other half of the rule that lets a command's description key be CONCATENATED (<c>console.cmd.&lt;name&gt;.desc</c>) instead of listed in a
/// switch: the catalogue has to hold an entry for EVERY registered command, in both languages. The switch this replaces was a table that had to be
/// edited twice for each new command (it existed twice, once in the console service and once in the overlay, and the two could drift); the census
/// only sees the key SPACE, so this test is what makes the space complete.
///
/// <para>
/// Reach: it reads the two shipped catalogues and the command names the registry declares at compile time. It cannot see a command a MOD registers
/// (that command's description falls back to its own English sentence by design) and it cannot judge a translation's wording.
/// </para>
/// </summary>
public class CommandDescriptionCatalogueTests
{
	[Fact]
	public void EveryRegisteredCommandHasItsDescriptionInBothLanguages()
	{
		var missing = new List<string>();
		foreach (var name in CommandNames())
		{
			var key = $"console.cmd.{name}.desc";
			if (!LocalizationCatalog.English.ContainsKey(key))
			{
				missing.Add($"en:{key}");
			}

			if (!LocalizationCatalog.Chinese.ContainsKey(key))
			{
				missing.Add($"zh:{key}");
			}
		}

		Assert.True(
			missing.Count == 0,
			"a command without its catalogue entry falls back to English on a Chinese client — the exact defect the concatenated key is allowed by: "
			+ string.Join(", ", missing));
	}

	/// <summary>
	/// The command names the registry declares, read from the source of the attribute that declares them: the same source of truth the registry
	/// composes itself from, so a command added anywhere in the tree is covered without a list to update here.
	/// </summary>
	private static IEnumerable<string> CommandNames()
	{
		var root = RepositoryRoot();
		foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
		{
			if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				|| file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			{
				continue;
			}

			foreach (var line in File.ReadAllLines(file))
			{
				var marker = line.IndexOf("[ConsoleCommand(\"", StringComparison.Ordinal);
				if (marker < 0)
				{
					continue;
				}

				var start = marker + "[ConsoleCommand(\"".Length;
				var end = line.IndexOf('"', start);
				if (end > start)
				{
					yield return line.Substring(start, end - start);
				}
			}
		}
	}

	private static string RepositoryRoot()
	{
		var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CasualtiesUnknownOnline.slnx")))
		{
			directory = directory.Parent;
		}

		Assert.NotNull(directory);
		return directory!.FullName;
	}
}
