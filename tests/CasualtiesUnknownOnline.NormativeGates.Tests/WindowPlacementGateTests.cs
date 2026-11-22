using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// No code in this repository may place, raise, pin or restyle a window. 2026-11-22: a "keep the game window at the
/// bottom" change was written as <c>SetWindowPos</c> calls in a loop, first in the launcher and then inside the client;
/// its window match was a title substring, the title of the window running the session contains the repository name,
/// so the person's own window lost its taskbar entry, and the repair then made it always-on-top. A written rule did not
/// stop any of that — the calls did. The only window API this tree may use is a capture (<c>PrintWindow</c>), which
/// reads pixels and changes nothing.
/// </summary>
public sealed class WindowPlacementGateTests
{
	// Placement and styling calls. Captures (PrintWindow / BitBlt) and input posting are not in this list: they are
	// read-only for the window or belong to the OS-input channel with its own authorisation.
	private static readonly string[] ForbiddenApi =
	[
		"SetWindowPos",
		"SetForegroundWindow",
		"BringWindowToTop",
		"SetWindowLong",
		"GetWindowLong",
		"SetWindowPlacement",
		"WS_EX_NOACTIVATE",
		"WS_EX_TOPMOST",
		"HWND_TOPMOST",
		"HWND_BOTTOM",
	];

	private static readonly string[] ScannedDirectories = ["src", "tools", "tests", "scripts"];

	[Fact]
	public void NoWindowPlacementCall_LivesInTheTree()
	{
		var hits = new List<string>();
		foreach (var root in ScannedDirectories.Where(Directory.Exists))
		{
			foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
			{
				if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
					|| file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
				{
					continue;
				}

				var lines = File.ReadAllLines(file);
				for (var index = 0; index < lines.Length; index++)
				{
					foreach (var api in ForbiddenApi)
					{
						if (lines[index].Contains(api, StringComparison.Ordinal))
						{
							hits.Add($"{file}:{index + 1}: {api}");
						}
					}
				}
			}
		}

		Assert.True(
			hits.Count == 0,
			"window placement or styling calls are not allowed in this tree (a window that must stay behind is created "
			+ "without activation, and a person's windows are never touched): " + string.Join("; ", hits));
	}
}
