using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CasualtiesUnknownOnline.Runtime.Localization;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The pin for the Online UI window's console-page removal. The user ruled (2026-09-21) that the
/// window's console page is deleted: the in-game <c>/</c> overlay is the same command and chat
/// surface with more capability (completion, history, suggestions, the notification lines), so the
/// page was a second place to keep in sync and nothing else. The ticket is
/// <c>docs/backlog/review/remove-the-online-ui-console-page.md</c>.
///
/// It reads the page shell, the page enum, the window state and the localisation catalogue as text
/// (the both-language half also reads the compiled catalogue) and lives in its own class on purpose:
/// a re-added page must fail here for its own reason, not as a missing-type compile error in some
/// other test.
///
/// What the matchers prove: the page enum is exactly the six surviving pages, in order; the tab row
/// BUILDS exactly one tab per page with a <c>tab.&lt;page&gt;</c> label, in the same order, and the whole
/// window file builds exactly six tabs; the page dispatch is exactly one case per page, each calling that
/// page's builder; the console drawer file is deleted, its type is referenced nowhere in the plugin
/// source, and no plugin file but the overlay names <c>ctx.Commands</c>; the window state no longer
/// carries the page's input field; and the catalogue's <c>tab.</c>/<c>console.</c> keys are exactly the
/// keys some source file READS through <c>T(...)</c>, in both languages — an orphan key, a dangling
/// reference, and a key that survives only inside a comment or a log literal all fail, and the overlay's
/// own key staying read is part of that census.
///
/// The tab row and the page dispatch moved from IMGUI drawing to model building in S2b of the art and
/// controls overhaul, and the anchors moved with them: the contract is the same one — one tab per
/// surviving page, one case per page — expressed where the window is built now.
///
/// What the matchers cannot see: a rendering. "No Console tab in the window" and "<c>/</c> still opens
/// the overlay with completion, history and suggestions" are the user's real-session rows; no test in
/// this tree can draw a frame.
/// </summary>
public sealed class OnlineUiConsolePageRemovalPinTests
{
	[Fact]
	public void ThePageEnumIsExactlyTheSixSurvivingPagesInOrder() =>
		Assert.True(
			PageEnumHolds(ReadPageEnumSource()),
			$"the Online UI page enum is not the six surviving pages: {PageEnum(ReadPageEnumSource())}");

	[Fact]
	public void TheTabRowIsExactlyOneTabPerPageInPageOrder() =>
		Assert.True(
			TabRowHolds(ReadWindowSource()),
			$"the Online UI tab row is not exactly one tab per surviving page, in page order, each labelled tab.<page>: {Flatten(ExtractMember(ReadWindowSource(), TabRowAnchor))}");

	[Fact]
	public void ThePageSwitchIsExactlyOneCasePerPage() =>
		Assert.True(
			PageSwitchHolds(ReadWindowSource()),
			$"the Online UI page switch is not exactly one case per surviving page: {Flatten(SwitchBlock(ReadWindowSource()))}");

	[Fact]
	public void TheConsolePageLeavesNoTraceInThePluginSource()
	{
		Assert.False(
			File.Exists(ConsoleDrawerPath()),
			"OnlineUiConsoleDrawer.cs must be deleted with the page, not kept as a dead drawer");
		Assert.True(
			NoConsoleDrawerReference(ReadPluginSource()),
			"the console page's drawer type is still referenced from the plugin source");
	}

	[Fact]
	public void TheConsoleIsRenderedByTheOverlayAlone()
	{
		var files = PluginFilesReadingTheCommandBuffer();
		Assert.True(
			files.Count == 1 && files[0] == "CommandConsoleOverlay.cs",
			$"the in-game command buffer must be read by the overlay alone; these plugin files read it: {string.Join(", ", files)}");
	}

	[Fact]
	public void TheWindowStateNoLongerCarriesTheConsoleInputField() =>
		Assert.True(
			WindowStateHolds(ReadWindowStateSource()),
			"OnlineUiWindowState must not carry the console page's input field");

	[Fact]
	public void TheCatalogueDeclaresExactlyTheTabAndConsoleKeysTheSourceReferences()
	{
		var declared = DeclaredUiKeys(ReadCatalogueSource());
		var referenced = ReferencedUiKeys(ReadReferencedSource());

		Assert.True(
			declared.SequenceEqual(ExpectedUiKeys),
			$"the catalogue's tab./console. keys are not the surviving set: {string.Join(", ", declared)}");
		Assert.True(
			referenced.SequenceEqual(ExpectedUiKeys),
			$"the source references a different tab./console. key set than the catalogue declares: {string.Join(", ", referenced)}");
	}

	[Fact]
	public void BothLanguagesCarryEverySurvivingKey()
	{
		foreach (var key in ExpectedUiKeys)
		{
			Assert.True(LocalizationCatalog.English.ContainsKey(key), $"the English catalogue lost {key}");
			Assert.True(LocalizationCatalog.Chinese.ContainsKey(key), $"the Chinese catalogue lost {key}");
		}
	}

	// --- the matchers' negative samples: each rejected shape is a re-added half of the page this
	// cycle deletes, and every sample proves it really differs from the live text, so a text drift
	// cannot silently turn a sample into a tautology ---

	[Fact]
	public void ThePinRejectsAReAddedConsoleEnumMember()
	{
		var source = ReadPageEnumSource();
		var mutated = Mutate(source, "Preferences,", "Console, Preferences,");
		Assert.False(PageEnumHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsAReAddedConsoleTabCall()
	{
		// the tab row back in front of Preferences, in the shape the model build spells it
		var mutated = Mutate(
			ReadWindowSource(),
			"""BuildTab(page, OnlineUiPage.Preferences, "tab.preferences", page.T("tab.preferences"));""",
			"""BuildTab(page, OnlineUiPage.Console, "tab.console", page.T("tab.console")); BuildTab(page, OnlineUiPage.Preferences, "tab.preferences", page.T("tab.preferences"));""");
		Assert.False(TabRowHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsATabRowMissingAPage()
	{
		// the census is a ceiling too: a dropped tab leaves the remaining ones intact
		var mutated = Mutate(ReadWindowSource(), """BuildTab(page, OnlineUiPage.Preferences, "tab.preferences", page.T("tab.preferences"));""", string.Empty);
		Assert.False(TabRowHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsAReAddedConsoleSwitchCase()
	{
		// the switch would then dispatch a page the enum no longer declares
		var mutated = Mutate(
			ReadWindowSource(),
			"case OnlineUiPage.Preferences:",
			"case OnlineUiPage.Console: OnlineUiConsoleDrawer.Draw(ctx); break; case OnlineUiPage.Preferences:");
		Assert.False(PageSwitchHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsAConsoleDrawerStillReferenced()
	{
		var mutated = Mutate(
			ReadWindowSource(),
			"OnlineUiHomeDrawer.Build(ctx, page);",
			"OnlineUiConsoleDrawer.Build(ctx, page);");
		Assert.False(NoConsoleDrawerReference(mutated));
	}

	[Fact]
	public void ThePinRejectsAKeyThatSurvivesOnlyInABlockComment()
	{
		// the census counts READS: deleting the overlay's read and hiding the key text in a block
		// comment (or a log literal, below) must not satisfy it
		var mutated = Mutate(
			ReadReferencedSource(),
			"""ctx.T("console.overlay.empty")""",
			"""/* ctx.T("console.overlay.empty") */""");
		Assert.False(CatalogueAndReferencesAgree(ReadCatalogueSource(), mutated));
	}

	[Fact]
	public void ThePinRejectsAKeyThatSurvivesOnlyInALogLiteral()
	{
		var mutated = Mutate(
			ReadReferencedSource(),
			"""ctx.T("console.overlay.empty")""",
			"""Logger.Info("console.overlay.empty");""");
		Assert.False(CatalogueAndReferencesAgree(ReadCatalogueSource(), mutated));
	}

	[Fact]
	public void ThePinRejectsASeventhTabDrawnOutsideTheTabRow()
	{
		// the tab row's own body stays byte-perfect; the extra tab is built by the page pass
		var mutated = Mutate(
			ReadWindowSource(),
			"BuildTabs(page);",
			"BuildTabs(page);\n\t\tBuildTab(page, OnlineUiPage.Home, \"tab.home\", page.T(\"tab.home\"));");
		Assert.False(TabRowHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsAnInlineConsolePanel()
	{
		// no drawer type name and no tab./console. key: only the command-buffer census can see it
		var mutated = Mutate(
			ReadWindowSource(),
			"OnlineUiPreferencesDrawer.Build(ctx, page);",
			"OnlineUiPreferencesDrawer.Build(ctx, page); Logger.Info(ctx.Commands.Lines.Count);");
		Assert.False(LeavesTheCommandBufferAlone(mutated));
	}

	[Fact]
	public void ThePinToleratesASessionReferenceInTheWindowState()
	{
		// the field match is word-bounded: the console input SESSION is not the deleted buffer
		var mutated = Mutate(
			ReadWindowStateSource(),
			"internal string ProfileNameInput = \"\";",
			"internal ConsoleInputSession Session = null!;\n\n\tinternal string ProfileNameInput = \"\";");
		Assert.True(WindowStateHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsAReAddedConsoleInputField()
	{
		var mutated = Mutate(
			ReadWindowStateSource(),
			"internal string LobbyIdInput = \"\";",
			"internal string LobbyIdInput = \"\";\n\n\tinternal string ConsoleInput = \"\";");
		Assert.False(WindowStateHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsAReAddedConsolePageKey()
	{
		// an orphan key: declared with nothing referencing it (this is how tab.console and the four
		// console.* page keys read at HEAD once the drawer is gone)
		var mutated = Mutate(
			ReadCatalogueSource(),
			"[\"tab.preferences\"] = \"Preferences\",",
			"[\"tab.preferences\"] = \"Preferences\",\n\t\t[\"tab.console\"] = \"Console\",");
		Assert.False(CatalogueAndReferencesAgree(mutated, ReadReferencedSource()));
	}

	[Fact]
	public void ThePinRejectsAReferenceWithoutAKey()
	{
		// the dangling direction: a source reference whose key the catalogue does not declare
		var mutated = Mutate(
			ReadWindowSource(),
			"ctx.T(\"window.title\")",
			"ctx.T(\"window.title\") + ctx.T(\"console.hint\")");
		Assert.False(CatalogueAndReferencesAgree(ReadCatalogueSource(), ReadReferencedSource() + mutated));
	}

	private const string TabRowAnchor = "private void BuildTabs(OnlineUiPageBuilder page)";
	private const string PageSwitchAnchor = "switch (_state.Page)";
	private const string ConsoleDrawerType = "OnlineUiConsoleDrawer";
	private const string CommandBufferAnchor = "ctx.Commands";
	private const string TabCallAnchor = "BuildTab(";
	private const string TabRowCallAnchor = "BuildTabs(page);";
	private const string UiKeyPattern = "(?:tab|console)\\.[a-zA-Z_.]+";

	private static readonly Regex DeclaredKeyPattern = new($@"\[""({UiKeyPattern})""\]", RegexOptions.Compiled);
	private static readonly Regex ReferencedKeyPattern = new($@"\bT\(""({UiKeyPattern})""\)", RegexOptions.Compiled);
	private static readonly Regex ConsoleInputFieldPattern = new(@"\bConsoleInput\b", RegexOptions.Compiled);

	/// <summary>
	/// Every <c>tab.</c>/<c>console.</c> key the tree still needs: one per surviving page plus the
	/// in-game overlay's own empty-buffer line. A key outside this set is an orphan, a missing key is
	/// a dangling reference, and the in-game console's remaining keys are the overlay's.
	/// </summary>
	private static readonly string[] ExpectedUiKeys =
	[
		"console.overlay.empty",
		"tab.admin",
		"tab.home",
		"tab.network",
		"tab.players",
		"tab.preferences",
		"tab.worlds",
	];

	/// <summary>The surviving page set, in enum order: the tab row and the switch both follow it.</summary>
	private const string ExpectedPageBody = "Home, Players, Network, Admin, Worlds, Preferences,";

	private const string ExpectedTabRow = """private void BuildTabs(OnlineUiPageBuilder page) { BuildTab(page, OnlineUiPage.Home, "tab.home", page.T("tab.home")); BuildTab(page, OnlineUiPage.Players, "tab.players", page.T("tab.players")); BuildTab(page, OnlineUiPage.Network, "tab.network", page.T("tab.network")); BuildTab(page, OnlineUiPage.Admin, "tab.admin", page.T("tab.admin")); BuildTab(page, OnlineUiPage.Worlds, "tab.worlds", page.T("tab.worlds")); BuildTab(page, OnlineUiPage.Preferences, "tab.preferences", page.T("tab.preferences")); }""";

	private const string ExpectedPageSwitch = """switch (_state.Page) { case OnlineUiPage.Home: OnlineUiHomeDrawer.Build(ctx, page); break; case OnlineUiPage.Players: OnlineUiPlayersDrawer.Build(ctx, page); break; case OnlineUiPage.Network: OnlineUiNetworkDrawer.Build(ctx, page); break; case OnlineUiPage.Admin: OnlineUiAdminDrawer.Build(ctx, page); break; case OnlineUiPage.Worlds: OnlineUiWorldsDrawer.Build(ctx, page); break; case OnlineUiPage.Preferences: OnlineUiPreferencesDrawer.Build(ctx, page); break; }""";

	private static bool PageEnumHolds(string source) => PageEnum(source) == ExpectedPageBody;

	private static string PageEnum(string source) => BlockBetween(Flatten(source), '{', '}').Trim();

	private static bool TabRowHolds(string source) =>
		Flatten(ExtractMember(source, TabRowAnchor)) == ExpectedTabRow
		// The whole file, not just this member: a seventh tab drawn anywhere else — the window's
		// content pass included — would render a page the rest of the pin has already deleted.
		&& CountOf(Flatten(source), TabCallAnchor) == 7
		&& CountOf(Flatten(source), TabRowCallAnchor) == 1;

	private static bool PageSwitchHolds(string source) => Flatten(SwitchBlock(source)) == ExpectedPageSwitch;

	private static bool NoConsoleDrawerReference(string source) =>
		!Flatten(source).Contains(ConsoleDrawerType, StringComparison.Ordinal);

	/// <summary>
	/// The page's own input buffer must be gone. The match is word-bounded on purpose: a legitimate
	/// <c>ConsoleInputSession</c> reference in this file is not the deleted buffer, and a plain
	/// substring test failed such a reference spuriously.
	/// </summary>
	private static bool WindowStateHolds(string source) => !ConsoleInputFieldPattern.IsMatch(Flatten(source));

	private static bool CatalogueAndReferencesAgree(string catalogueSource, string referencedSource) =>
		DeclaredUiKeys(catalogueSource).SequenceEqual(ExpectedUiKeys)
		&& ReferencedUiKeys(referencedSource).SequenceEqual(ExpectedUiKeys);

	/// <summary>The keys the catalogue declares, both languages folded into one set, sorted.</summary>
	private static string[] DeclaredUiKeys(string catalogueSource) =>
		DeclaredKeyPattern.Matches(catalogueSource)
			.Cast<Match>()
			.Select(match => match.Groups[1].Value)
			.Distinct()
			.OrderBy(key => key, StringComparer.Ordinal)
			.ToArray();

	/// <summary>
	/// The keys some source file actually names, comments dropped so a comment naming a key can
	/// neither satisfy nor break the census. The catalogue itself is excluded: it declares them.
	/// </summary>
	private static string[] ReferencedUiKeys(string referencedSource) =>
		ReferencedKeyPattern.Matches(Flatten(referencedSource))
			.Cast<Match>()
			.Select(match => match.Groups[1].Value)
			.Distinct()
			.OrderBy(key => key, StringComparer.Ordinal)
			.ToArray();

	private static string BlockBetween(string text, char open, char close)
	{
		var start = text.IndexOf(open);
		Assert.True(start >= 0, $"the block opener `{open}` was not found");
		var end = text.IndexOf(close, start + 1);
		Assert.True(end > start, $"the block closer `{close}` was not found");
		return text.Substring(start + 1, end - start - 1);
	}

	/// <summary>The page switch's own block: the anchor through its closing brace at two tabs.</summary>
	private static string SwitchBlock(string source)
	{
		var start = source.IndexOf(PageSwitchAnchor, StringComparison.Ordinal);
		Assert.True(start >= 0, $"the page switch `{PageSwitchAnchor}` was not found in OnlineUiWindow.cs");
		var end = source.IndexOf("\n\t\t}", start, StringComparison.Ordinal);
		Assert.True(end > start, "the page switch has no closing brace at two tabs");
		return source.Substring(start, end - start + "\n\t\t}".Length);
	}

	/// <summary>The member's declaration line and its body, up to the next member (a line that starts at
	/// one tab with a declaration or with its doc comment).</summary>
	private static string ExtractMember(string source, string marker)
	{
		var start = source.IndexOf(marker, StringComparison.Ordinal);
		Assert.True(start >= 0, $"{marker} not found");
		var lines = source.Substring(start).Split('\n');
		var kept = new List<string> { lines[0] };
		for (var i = 1; i < lines.Length; i++)
		{
			var line = lines[i];
			if (line.StartsWith("\tprivate ", StringComparison.Ordinal)
				|| line.StartsWith("\tinternal ", StringComparison.Ordinal)
				|| line.StartsWith("\t/// ", StringComparison.Ordinal))
			{
				break;
			}

			kept.Add(line);
		}

		return string.Join("\n", kept);
	}

	private static string Mutate(string body, string from, string to)
	{
		Assert.True(body.Contains(from, StringComparison.Ordinal), $"the mutation anchor `{from}` is not in the live body — the sample would silently no-op");
		var mutated = body.Replace(from, to);
		Assert.NotEqual(body, mutated);
		return mutated;
	}

	private static int CountOf(string text, string needle)
	{
		var count = 0;
		for (var index = text.IndexOf(needle, StringComparison.Ordinal);
			index >= 0;
			index = text.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
		{
			count++;
		}

		return count;
	}

	private static bool LeavesTheCommandBufferAlone(string source) =>
		!Flatten(source).Contains(CommandBufferAnchor, StringComparison.Ordinal);

	/// <summary>The plugin files whose code reads the in-game command buffer — the overlay alone, today.</summary>
	private static List<string> PluginFilesReadingTheCommandBuffer() =>
		Directory.GetFiles(PluginDirectory(), "*.cs", SearchOption.AllDirectories)
			.Where(path => !IsBuildOutput(path))
			.Where(path => !LeavesTheCommandBufferAlone(File.ReadAllText(path)))
			.Select(path => Path.GetFileName(path)!)
			.OrderBy(name => name, StringComparer.Ordinal)
			.ToList();

	/// <summary>
	/// Flattens text to one whitespace-normalised line with its comments removed — line comments AND
	/// block comments, so a key hidden in either can neither satisfy a pin nor break one — while string
	/// and character literals survive verbatim.
	/// </summary>
	private static string Flatten(string text)
	{
		var kept = new StringBuilder(text.Length);
		for (var i = 0; i < text.Length; i++)
		{
			var current = text[i];

			if (current == '"' || current == '\'')
			{
				kept.Append(current);
				for (i++; i < text.Length; i++)
				{
					kept.Append(text[i]);
					if (text[i] == '\\')
					{
						i++;
						if (i < text.Length)
						{
							kept.Append(text[i]);
						}

						continue;
					}

					if (text[i] == current)
					{
						break;
					}
				}

				continue;
			}

			if (current == '/' && i + 1 < text.Length && text[i + 1] == '/')
			{
				while (i < text.Length && text[i] != '\n')
				{
					i++;
				}

				kept.Append(' ');
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

		return string.Join(" ", kept.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
	}

	private static string ReadPageEnumSource() => File.ReadAllText(Path.Combine(PluginDirectory(), "OnlineUiPage.cs"));

	private static string ReadWindowSource() => File.ReadAllText(Path.Combine(PluginDirectory(), "OnlineUiWindow.cs"));

	private static string ReadWindowStateSource() => File.ReadAllText(Path.Combine(PluginDirectory(), "OnlineUiWindowState.cs"));

	private static string ReadCatalogueSource() => File.ReadAllText(Path.Combine(SourceRoot(), "CasualtiesUnknownOnline.Runtime", "Localization", "LocalizationCatalog.cs"));

	private static string ConsoleDrawerPath() => Path.Combine(PluginDirectory(), "OnlineUiConsoleDrawer.cs");

	private static string ReadPluginSource() => string.Join(
		"\n",
		Directory.GetFiles(PluginDirectory(), "*.cs", SearchOption.AllDirectories)
			.Where(path => !IsBuildOutput(path))
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(File.ReadAllText));

	/// <summary>Every source file but the catalogue that declares the keys, and without build output.</summary>
	private static string ReadReferencedSource() => string.Join(
		"\n",
		Directory.GetFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories)
			.Where(path => !path.EndsWith("LocalizationCatalog.cs", StringComparison.Ordinal))
			.Where(path => !IsBuildOutput(path))
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(File.ReadAllText));

	private static bool IsBuildOutput(string path) =>
		path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
		|| path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

	private static string SourceRoot() => Path.Combine(FindRepositoryRoot(), "src");

	private static string PluginDirectory() => Path.Combine(SourceRoot(), "CasualtiesUnknownOnline.Plugin");

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
