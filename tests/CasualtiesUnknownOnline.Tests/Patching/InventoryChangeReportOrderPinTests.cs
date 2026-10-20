using System;
using System.IO;
using System.Text;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The report ORDER at the three inventory-gesture postfixes: the item fact first, the character snapshot
/// second. The rule belongs to <c>GameAdapterBridge.OnInventoryChanged</c>'s contract — a peer compares an
/// arriving snapshot against the facts it already holds, so a snapshot that overtakes its event reads as a
/// move without a report and fires <c>CloneFactTable</c>'s divergence pair (batch <c>20261025-b</c> saw one
/// pair per wear/swap, 1–3 ms before the event applied).
/// <para>
/// The runtime half is pinned by <see cref="CloneFactTableDivergenceMonitorTests"/> (an event applied before
/// the snapshot leaves the next snapshot quiet) and by the ticket's three-client acceptance row. This file is
/// the WIRING half, a source-shape pin, because the Unity adapter has no unit harness: re-ordering the calls
/// at <c>WearWearablePatch</c>, <c>SwapSlotsPatch</c> or <c>SwitchHandsPatch</c> would otherwise only show up
/// in a live batch as the warning pair, and a deletion cannot pass silently in the meantime.
/// </para>
/// </summary>
public class InventoryChangeReportOrderPinTests
{
	/// <summary>The one entry point the order rule protects; a postfix must call it exactly once, last.</summary>
	private const string SnapshotCall = "PatchBridge.Impl?.OnInventoryChanged()";

	[Fact]
	public void TheThreeGesturePostfixes_ReportTheItemFactBeforeTheSnapshot()
	{
		var bodyPatches = StripComments(ReadAdapterPatch("BodyPatches.cs"));
		var bodyItemPatches = StripComments(ReadAdapterPatch("BodyItemPatches.cs"));

		AssertOrder(
			"WearWearablePatch",
			ClassBody(bodyPatches, "WearWearablePatch", "DropWearablePatch"),
			"PatchBridge.Impl?.OnItemWorn(item)",
			"PatchBridge.Impl?.OnItemPickedUp(item)");

		AssertOrder(
			"SwapSlotsPatch",
			ClassBody(bodyItemPatches, "SwapSlotsPatch", "SwitchHandsPatch"),
			"PatchBridge.Impl?.OnSlotMoved(__instance, slot1, \"Swap\")",
			"PatchBridge.Impl?.OnSlotMoved(__instance, slot2, \"Swap\")");

		AssertOrder(
			"SwitchHandsPatch",
			ClassBody(bodyItemPatches, "SwitchHandsPatch", "PickUpItemPatch"),
			"PatchBridge.Impl?.OnSlotMoved(__instance, 0, \"Hands\")",
			"PatchBridge.Impl?.OnSlotMoved(__instance, 1, \"Hands\")");
	}

	/// <summary>
	/// The matcher's own positive/negative samples: the two synthetic postfix bodies say the caller order is
	/// what is compared, so the pin cannot pass on a source that merely CONTAINS both calls in either order.
	/// </summary>
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void TheOrderCheck_ReadsTheCallOrder(bool factsFirst)
	{
		const string factCall = "PatchBridge.Impl?.OnSlotMoved(__instance, 0, \"Hands\");";
		var body = factsFirst
			? $"\t\t\t{factCall}\n\t\t\t{SnapshotCall};"
			: $"\t\t\t{SnapshotCall};\n\t\t\t{factCall}";

		var violation = FindOrderViolation(body, factCall);

		if (factsFirst)
		{
			Assert.Null(violation);
		}
		else
		{
			Assert.Contains("reported after the snapshot", violation, StringComparison.Ordinal);
		}
	}

	/// <summary>
	/// A call spelled in a trailing comment or a block comment is not a call: with the real fact below the
	/// snapshot, the checker must still fail. Without comment removal the comment's copy would place the fact
	/// first and the pin would pass on a tree whose real call order is inverted.
	/// </summary>
	[Fact]
	public void TheOrderCheck_IgnoresCallTextThatOnlyLivesInComments()
	{
		const string factCall = "PatchBridge.Impl?.OnSlotMoved(__instance, 0, \"Hands\");";
		var body = $"\t\t\t{SnapshotCall};\n\t\t\t// trailing: {factCall}\n\t\t\t/* block: {factCall} */\n\t\t\t{factCall}";

		var violation = FindOrderViolation(body, factCall);

		Assert.NotNull(violation);
		Assert.Contains("reported after the snapshot", violation, StringComparison.Ordinal);
	}

	private static void AssertOrder(string patchName, string block, params string[] factCalls)
	{
		var violation = FindOrderViolation(block, factCalls);
		Assert.True(violation is null, $"{patchName}: {violation}");
	}

	/// <summary>
	/// Null when every fact call precedes the single snapshot call; otherwise the first broken link, named.
	/// Comments — whole-line, inline and block — are removed first, so call text that only lives in a
	/// comment cannot satisfy the pin; string and char literals are kept, so a quoted <c>//</c> inside a
	/// literal cannot truncate real code (the scanner is overkill for today's two files and keeps the pin
	/// honest as they grow).
	/// </summary>
	private static string? FindOrderViolation(string block, params string[] factCalls)
	{
		var code = StripComments(block);
		var snapshotCount = Count(code, SnapshotCall);
		if (snapshotCount != 1)
		{
			return $"expected exactly one `{SnapshotCall}`, found {snapshotCount}";
		}

		var snapshotAt = code.IndexOf(SnapshotCall, StringComparison.Ordinal);
		foreach (var factCall in factCalls)
		{
			var factAt = code.IndexOf(factCall, StringComparison.Ordinal);
			if (factAt < 0)
			{
				return $"`{factCall}` is missing";
			}

			if (factAt > snapshotAt)
			{
				return $"`{factCall}` is reported after the snapshot — the snapshot would overtake the item fact";
			}
		}

		return null;
	}

	/// <summary>The class body from its declaration up to the NEXT patch class, so only that postfix is read.</summary>
	private static string ClassBody(string source, string className, string nextClassName)
	{
		var start = source.IndexOf($"internal static class {className}", StringComparison.Ordinal);
		Assert.True(start >= 0, $"the class `{className}` was not found in the patch source");
		var end = source.IndexOf($"internal static class {nextClassName}", start, StringComparison.Ordinal);
		Assert.True(end > start, $"the class after `{className}` (`{nextClassName}`) was not found — the block boundary moved");
		return source.Substring(start, end - start);
	}

	private static int Count(string text, string needle)
	{
		var count = 0;
		for (var at = text.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
		{
			count++;
		}

		return count;
	}

	/// <summary>
	/// Removes <c>//</c> and <c>/* */</c> comments while keeping string and char literals, and folds CRLF to
	/// LF so the pin is line-ending agnostic. Structure (newlines) survives a removed block comment.
	/// </summary>
	private static string StripComments(string source)
	{
		var text = source.Replace("\r\n", "\n");
		var output = new StringBuilder(text.Length);
		var inString = false;
		var inChar = false;
		var inLineComment = false;
		var inBlockComment = false;
		for (var i = 0; i < text.Length; i++)
		{
			var current = text[i];
			var next = i + 1 < text.Length ? text[i + 1] : '\0';
			if (inLineComment)
			{
				if (current == '\n')
				{
					inLineComment = false;
					output.Append(current);
				}

				continue;
			}

			if (inBlockComment)
			{
				if (current == '*' && next == '/')
				{
					inBlockComment = false;
					i++;
				}
				else if (current == '\n')
				{
					output.Append(current);
				}

				continue;
			}

			if (inString)
			{
				output.Append(current);
				if (current == '\\' && next != '\0')
				{
					output.Append(next);
					i++;
				}
				else if (current == '"')
				{
					inString = false;
				}

				continue;
			}

			if (inChar)
			{
				output.Append(current);
				if (current == '\\' && next != '\0')
				{
					output.Append(next);
					i++;
				}
				else if (current == '\'')
				{
					inChar = false;
				}

				continue;
			}

			if (current == '"')
			{
				inString = true;
				output.Append(current);
				continue;
			}

			if (current == '\'')
			{
				inChar = true;
				output.Append(current);
				continue;
			}

			if (current == '/' && next == '/')
			{
				inLineComment = true;
				i++;
				continue;
			}

			if (current == '/' && next == '*')
			{
				inBlockComment = true;
				i++;
				continue;
			}

			output.Append(current);
		}

		return output.ToString();
	}

	private static string ReadAdapterPatch(string name) =>
		File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "CasualtiesUnknownOnline.GameAdapter", "Patches", name));

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CasualtiesUnknownOnline.slnx")))
		{
			directory = directory.Parent;
		}

		if (directory is null)
		{
			throw new InvalidOperationException("could not locate repository root (CasualtiesUnknownOnline.slnx)");
		}

		return directory.FullName;
	}
}
