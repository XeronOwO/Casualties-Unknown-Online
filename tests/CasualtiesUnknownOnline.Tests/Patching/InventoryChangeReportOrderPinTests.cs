using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
/// <para>
/// The craft half is the same rule at its hardest point (ticket <c>craft-carried-fact-retirement</c>): a
/// craft consumes/reports inside <c>Origin.Craft</c> and commits its ONE report after the native call, so
/// its snapshot entry point withholds until the report is out, and both apply paths must retire the carried
/// row the report destroyed — otherwise the post-craft snapshot is the first thing that announces it.
/// </para>
/// </summary>
public class InventoryChangeReportOrderPinTests
{
	/// <summary>The one entry point the order rule protects; a postfix must call it exactly once, last.</summary>
	private const string SnapshotCall = "PatchBridge.Impl?.OnInventoryChanged()";

	/// <summary>The local a guard can be hoisted into (<c>var inCraft = CallContext.IsWithin(…)</c>) — the matcher follows it, so the guard's OWN block is the one that must exit.</summary>
	private static readonly Regex GuardLocalRegex = new(@"var\s+(\w+)\s*=\s*CallContext\.IsWithin\(CallContext\.Origin\.Craft\)", RegexOptions.Compiled);

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

	/// <summary>
	/// The craft half of the order rule, at the caller that inverted it: the wear postfix runs inside
	/// <c>Origin.Craft</c> when a craft's product is worn, and the craft postfix commits the ONE report only after
	/// that native call — so this postfix must not ask for the snapshot there. It exits instead of reaching
	/// <c>OnInventoryChanged</c>, and <c>RecipeTryMakePatch</c>'s postfix sends the one re-report that follows the
	/// report (pinned below). Without this, a peer reads the consumed material and the product as moves without
	/// an event (ticket <c>craft-carried-fact-retirement</c>).
	/// </summary>
	[Fact]
	public void TheWearPostfix_WithholdsItsSnapshotInsideACraft()
	{
		var bodyPatches = StripComments(ReadAdapterPatch("BodyPatches.cs"));
		var wear = ClassBody(bodyPatches, "WearWearablePatch", "DropWearablePatch");

		var violation = FindCraftWithholdViolation(wear, SnapshotCall);
		Assert.True(violation is null, $"WearWearablePatch: {violation}");
	}

	/// <summary>
	/// The craft postfixes close the scope (the report commit's own <c>finally</c> disposes it) and only then
	/// ask for the snapshot that follows the report. A re-order here would make the wear postfix's withhold
	/// swallow the craft's ONLY immediate re-report — the peers would then wait for the 1 Hz snapshot again,
	/// which is the delay this pair exists to remove.
	/// </summary>
	[Theory]
	[InlineData("RecipeTryMakePatch", "BodyCombinePatch", "PatchBridge.Impl?.OnCraftEnd(__state)")]
	[InlineData("BodyCombinePatch", "LiquidTransferFinishPatch", "PatchBridge.Impl?.OnCombineEnd(__state)")]
	public void TheCraftPostfixes_CommitTheReportBeforeAskingForTheSnapshot(string patch, string nextPatch, string commitCall)
	{
		var source = StripComments(ReadAdapterPatch("CraftingPatches.cs"));
		var block = ClassBody(source, patch, nextPatch);

		var reportAt = block.IndexOf(commitCall, StringComparison.Ordinal);
		Assert.True(reportAt >= 0, $"{patch}: `{commitCall}` is missing — the terminal state never reaches the coordinator");

		var snapshotAt = block.IndexOf(SnapshotCall, StringComparison.Ordinal);
		Assert.True(snapshotAt > reportAt, $"{patch}: the snapshot must follow `{commitCall}` (the scope closes there); it is asked for before the report is committed");
		Assert.Equal(1, Count(block, SnapshotCall));
	}

	/// <summary>
	/// The by-pass half of the same pin: no patch file may reach the character domain directly. The bridge is
	/// the patch layer's one entry point, so a postfix that called <c>ReportInventoryChanged</c> itself would
	/// re-acquire exactly the snapshot the craft withhold withholds, and the pin above would stay green.
	/// </summary>
	[Fact]
	public void ThePatchLayer_ReachesTheCharacterSnapshotOnlyThroughTheBridge()
	{
		var patchDirectory = Path.Combine(FindRepositoryRoot(), "src", "CasualtiesUnknownOnline.GameAdapter", "Patches");
		var byPasses = Directory.EnumerateFiles(patchDirectory, "*.cs", SearchOption.AllDirectories)
			.Where(file => StripComments(File.ReadAllText(file)).Contains("ReportInventoryChanged", StringComparison.Ordinal))
			.Select(file => Path.GetFileName(file))
			.ToList();

		Assert.True(
			byPasses.Count == 0,
			$"the patch layer must ask the bridge (`OnInventoryChanged`), never the character domain directly: {string.Join(", ", byPasses)}");
	}

	/// <summary>The craft-withhold matcher's own positive and negative samples: the guard is an `if` whose branch exits before the one snapshot call. A mere mention of the query, an exit that belongs to some other condition, a snapshot taken before the guard's exit and a body with two snapshots are all violations.</summary>
	[Theory]
	[InlineData("if (CallContext.IsWithin(CallContext.Origin.Craft)) { return; }\nPatchBridge.Impl?.OnInventoryChanged();", true)]
	[InlineData("var inCraft = CallContext.IsWithin(CallContext.Origin.Craft);\nif (inCraft) { return; }\nPatchBridge.Impl?.OnInventoryChanged();", true)]
	[InlineData("PatchBridge.Impl?.OnInventoryChanged();\nif (CallContext.IsWithin(CallContext.Origin.Craft)) { return; }", false)]
	[InlineData("PatchBridge.Impl?.OnInventoryChanged();", false)]
	[InlineData("// if (CallContext.IsWithin(CallContext.Origin.Craft)) { return; }\nPatchBridge.Impl?.OnInventoryChanged();", false)]
	[InlineData("if (CallContext.IsWithin(CallContext.Origin.Craft)) { return; }\nPatchBridge.Impl?.OnInventoryChanged();\nPatchBridge.Impl?.OnInventoryChanged();", false)]
	[InlineData("var inCraft = CallContext.IsWithin(CallContext.Origin.Craft);\nif (other) { return; }\nPatchBridge.Impl?.OnInventoryChanged();", false)]
	[InlineData("var inCraft = CallContext.IsWithin(CallContext.Origin.Craft);\nPatchBridge.Impl?.OnInventoryChanged();\nif (inCraft) { return; }", false)]
	public void TheCraftWithholdCheck_ReadsTheGuardBeforeTheOneSnapshot(string body, bool withholds) =>
		Assert.Equal(withholds, FindCraftWithholdViolation(body, SnapshotCall) is null);

	/// <summary>
	/// The retirement's transport into the fact table: the item domain publishes <c>CarriedItemRetired</c> with the
	/// owner the host attributed the id to, and the session binding routes it to the fact table's TARGETED
	/// retirement (never the drop path's every-owner removal). Without the subscription the apply half is a
	/// signal into the void — the post-craft snapshot would still warn.
	/// </summary>
	[Fact]
	public void TheSessionBinding_RoutesTheRetirementIntoTheCloneFactTable()
	{
		var binding = StripComments(ReadAdapterFile("GameAdapterSessionBinding.cs"));

		Assert.Contains("CarriedItemRetired += OnCarriedItemRetired", binding, StringComparison.Ordinal);
		Assert.Contains("CarriedItemRetired -= OnCarriedItemRetired", binding, StringComparison.Ordinal);

		var handler = MemberBody(binding, "private void OnCarriedItemRetired(ulong owner, ulong itemId)");
		Assert.Contains("RetireCarriedItem(", handler, StringComparison.Ordinal);
	}

	/// <summary>
	/// Both halves of the apply retire the carried row the report destroyed: the host's own apply of a member's
	/// report, and every relay receiver's positional apply. The relay cannot classify for itself (its tables are
	/// empty), so the host stamps the routed kind — WorldDestroy for a scene copy, CarriedRetire for the sender's
	/// fact row — and an unattributable destroy stays None (no action anywhere).
	/// </summary>
	[Fact]
	public void BothCraftApplyPaths_RetireTheCarriedRowTheReportDestroyed()
	{
		var craft = StripComments(ReadRuntimeFile(Path.Combine("Session", "Items", "CraftSyncService.cs")));

		var guestApply = MemberBody(craft, "private void ApplyGuestReport(ulong sender, CraftReportMsg msg)");
		Assert.Contains("_carriedRows.IsOwnedBy(", guestApply, StringComparison.Ordinal);
		Assert.Contains("RetireCarriedLocal(sender, id)", guestApply, StringComparison.Ordinal);
		Assert.Contains("CraftApplyKind.CarriedRetire", guestApply, StringComparison.Ordinal);
		Assert.Contains("CraftApplyKind.WorldDestroy", guestApply, StringComparison.Ordinal);

		var relayApply = MemberBody(craft, "private void ApplyRelayed(CraftReportMsg msg)");
		Assert.Contains("CraftApplyKind.CarriedRetire", relayApply, StringComparison.Ordinal);
		Assert.Contains("RetireCarriedLocal(msg.OwnerSteamId, id)", relayApply, StringComparison.Ordinal);
		Assert.Contains("RemoveWorldItemLocal(", relayApply, StringComparison.Ordinal);
	}

	/// <summary>
	/// The host's OWN craft: its world destroys are stamped for the guests' scene copies (a deleted stamp leaves a
	/// destroyed floor item standing on every viewer), its carried destroys are stamped for the guests' fact rows,
	/// and the host's own authority row is retired (the in-craft destroy claim keeps the ordinary report off it).
	/// </summary>
	[Fact]
	public void TheHostOwnCraft_StampsBothDestroyRoutesAndRetiresItsKernelRow()
	{
		var craft = StripComments(ReadRuntimeFile(Path.Combine("Session", "Items", "CraftSyncService.cs")));
		var receive = MemberBody(craft, "public void FireCraftReportReceived(ulong sender, CraftReportMsg msg)");

		var ownBranchAt = receive.IndexOf("_carriedRows.RetireOwnRow(", StringComparison.Ordinal);
		var guestPathAt = receive.IndexOf("ApplyGuestReport(", StringComparison.Ordinal);
		Assert.True(ownBranchAt >= 0, "the host's own craft branch never retires the destroyed carried row from its kernel");
		Assert.True(ownBranchAt < guestPathAt, "the kernel retirement belongs to the host's own branch, before the guest-report path");
		Assert.Contains("CraftApplyKind.WorldDestroy", receive, StringComparison.Ordinal);
		Assert.Contains("CraftApplyKind.CarriedRetire", receive, StringComparison.Ordinal);

		var retirement = StripComments(ReadRuntimeFile(Path.Combine("Session", "Items", "CraftCarriedRowRetirement.cs")));
		var retire = MemberBody(retirement, "internal void RetireOwnRow(ulong itemId)");
		Assert.Contains("TryDestroy(", retire, StringComparison.Ordinal);

		var owned = MemberBody(retirement, "internal bool IsOwnedBy(ulong itemId, ulong owner)");
		Assert.Contains("ItemLocationKind.Carried or ItemLocationKind.Contained", owned, StringComparison.Ordinal);
		Assert.Contains("Owner.Value == owner", owned, StringComparison.Ordinal);
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

	/// <summary>
	/// Null when the craft scope's guard exits before the one snapshot call; otherwise the reason, named. The
	/// guard is an `if` whose condition is the scope query — used directly, or through the local assigned from
	/// it — and its OWN block must contain the exit: a defensive return belonging to another condition cannot
	/// stand in for it, and a snapshot placed before that exit is still a violation.
	/// </summary>
	private static string? FindCraftWithholdViolation(string body, string snapshotCall)
	{
		var code = StripComments(body);
		var snapshotCount = Count(code, snapshotCall);
		if (snapshotCount != 1)
		{
			return $"expected exactly one `{snapshotCall}`, found {snapshotCount}";
		}

		var query = "CallContext.IsWithin(CallContext.Origin.Craft)";
		var queryAt = code.IndexOf(query, StringComparison.Ordinal);
		if (queryAt < 0)
		{
			return $"no `{query}` withhold here — an in-craft snapshot would overtake the craft report that explains it";
		}

		var assignment = GuardLocalRegex.Match(code.Substring(0, queryAt + query.Length));
		var condition = assignment.Success ? $"if ({assignment.Groups[1].Value})" : $"if ({query})";
		var ifAt = code.IndexOf(condition, StringComparison.Ordinal);
		if (ifAt < 0)
		{
			return $"the craft scope is named but never guards an `if` (`{condition}`) — a guard that cannot exit withholds nothing";
		}

		var openAt = code.IndexOf('{', ifAt);
		var closeAt = openAt < 0 ? -1 : MatchingBrace(code, openAt);
		if (closeAt < 0)
		{
			return "the craft guard has no closed block";
		}

		if (!code.Substring(openAt, closeAt - openAt).Contains("return;", StringComparison.Ordinal))
		{
			return "the craft guard's own branch does not exit — the snapshot would be reached anyway";
		}

		var snapshotAt = code.IndexOf(snapshotCall, StringComparison.Ordinal);
		if (snapshotAt < closeAt)
		{
			return "the snapshot call is not after the craft guard's exit — the guard's branch falls through to it";
		}

		return null;
	}

	/// <summary>The closing brace that matches the one at <paramref name="openAt"/>, or -1 when the block never closes.</summary>
	private static int MatchingBrace(string code, int openAt)
	{
		var depth = 0;
		for (var i = openAt; i < code.Length; i++)
		{
			if (code[i] == '{')
			{
				depth++;
			}
			else if (code[i] == '}' && --depth == 0)
			{
				return i;
			}
		}

		return -1;
	}

	/// <summary>The body of one member: braces balanced from its opening brace, or the expression body up to its semicolon. String and char literals are skipped so a log template's braces cannot end the body early.</summary>
	private static string MemberBody(string source, string signature)
	{
		var start = source.IndexOf(signature, StringComparison.Ordinal);
		Assert.True(start >= 0, $"the member `{signature}` was not found in the source");

		var position = start + signature.Length;
		while (position < source.Length && char.IsWhiteSpace(source[position]))
		{
			position++;
		}

		if (position + 1 < source.Length && source[position] == '=' && source[position + 1] == '>')
		{
			var end = source.IndexOf(';', position);
			Assert.True(end > position, $"the expression body of `{signature}` has no terminator");
			return source.Substring(position, end - position);
		}

		Assert.True(position < source.Length && source[position] == '{', $"the member `{signature}` has no body");
		var depth = 0;
		var inString = false;
		var inChar = false;
		for (var i = position; i < source.Length; i++)
		{
			var current = source[i];
			if (inString)
			{
				if (current == '\\')
				{
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
				if (current == '\\')
				{
					i++;
				}
				else if (current == '\'')
				{
					inChar = false;
				}

				continue;
			}

			switch (current)
			{
				case '"':
					inString = true;
					break;
				case '\'':
					inChar = true;
					break;
				case '{':
					depth++;
					break;
				case '}':
					depth--;
					if (depth == 0)
					{
						return source.Substring(position, i - position + 1);
					}

					break;
			}
		}

		Assert.Fail($"the body of `{signature}` does not close");
		return string.Empty;
	}

	/// <summary>A non-patch adapter source, read by its path relative to the adapter project.</summary>
	private static string ReadAdapterFile(string relativePath) =>
		ReadProjectFile("CasualtiesUnknownOnline.GameAdapter", relativePath);

	/// <summary>A Runtime source, read by its path relative to the runtime project (the apply half lives there).</summary>
	private static string ReadRuntimeFile(string relativePath) =>
		ReadProjectFile("CasualtiesUnknownOnline.Runtime", relativePath);

	private static string ReadProjectFile(string project, string relativePath) =>
		File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", project, relativePath));

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
