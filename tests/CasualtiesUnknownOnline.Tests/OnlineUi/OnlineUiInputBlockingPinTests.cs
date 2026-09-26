using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The Online UI's input blocking after the migration (ticket online-ui-art-and-controls-overhaul, S4 for
/// the retirement pass, S5 for the two panels). Three facts hold the family together and none is visible
/// from a unit test: CUO's own guard must never lay a blocker over CUO's own surface, because that surface
/// is uGUI and blocks its own pixels (a blocker above it swallows every click on the launcher, the window
/// and the panels); both world input paths — the middle-click ping and the in-world right-click menu — must
/// ask ONE pointer census, which knows every surface; and every surface's fact must come from that
/// surface's own poll on the game's canvas, because S5 retired the rectangle list the two IMGUI panels
/// needed.
///
/// <para>
/// Every pin carries a mutation of the REAL source as its negative sample — the rows of
/// <see cref="Mutations"/> — so none of them can pass vacuously, and every anchor is matched after
/// comment-stripping and whitespace flattening, so indentation, line endings and a comment that merely
/// names an API cannot decide a result.
/// </para>
///
/// <para>
/// What these pins cannot see: whether a click really reaches the game's own controls behind the surface,
/// whether the pointer really lands on the launcher's or a panel's pixels at the game's UI scale, and
/// whether the console's mutual exclusion reads right in play — those are run observations. The pins hold
/// the shape the adapter and the plugin were built to.
/// </para>
/// </summary>
public sealed class OnlineUiInputBlockingPinTests
{
	[Fact]
	public void TheGuardNeverBlocksCuosOwnSurface() =>
		Assert.True(
			GuardsItsOwnSurface(AdapterRoot("OnlineMenuInputGuard.cs")),
			"CUO's own guard must leave CUO's own surface alone in every sweep it makes: its blockers exist for surfaces the game's EventSystem cannot see, and one over CUO's own canvas makes the launcher, the window and the panels unclickable");

	[Fact]
	public void TheSurfaceMarksItsOwnCanvas() =>
		Assert.True(
			MarksItsOwnCanvas(Adapter("OnlineUiSurfaceHost.cs")),
			"the live surface must mark the root it builds, or the guard has no way to tell CUO's canvas from the game's");

	[Fact]
	public void TheLauncherEntersThePointerCensus() =>
		Assert.True(
			FeedsTheLauncherFact(Plugin("OnlineUiHost.cs")),
			"the launcher's pointer fact must reach the census: without it a middle-click over the launcher pings the world and a right-click there opens the in-world menu (S2a's recorded limit)");

	[Fact]
	public void ThePanelsEnterThePointerCensus() =>
		Assert.True(
			FeedsThePanelFacts(Plugin("OnlineUiHost.cs")),
			"both panels' pointer facts must reach the census the same way the launcher's and the window's do: they are uGUI controls now, so the surface's poll is the only fact there is");

	[Fact]
	public void ThePanelsPollTheirOwnRect() =>
		Assert.True(
			PollsBothPanelRects(Adapter("OnlineUiSurfaceHost.cs")),
			"each panel must be polled on every frame the surface pushes, or a panel opened or closed under the pointer never reports the flip the census reads");

	[Fact]
	public void TheRetiredRectangleApiIsGoneFromTheSource() =>
		Assert.True(
			NoScopedRectangleApi(SourceSources()),
			"the scoped rectangle API (SetOnlineUiScopedBlocks / OnlineUiBlockRect / OnlineScopedRaycastFilter) had exactly two consumers — the IMGUI panels — and retired with them: a rectangle list regrown anywhere means a surface is being blocked from outside again");

	[Fact]
	public void BothWorldInputPathsAskTheCensus() =>
		Assert.True(
			BothPathsAskTheCensus(Plugin("OnlineUiOverlay.cs"), Plugin("OnlineUiPlayerContextMenu.cs")),
			"the world middle-click and the world right-click must ask the same rule: two hand-kept lists of surfaces are how the launcher came to be on neither");

	[Fact]
	public void TheConsoleKeepsTheLauncherFromOpeningASecondSurface() =>
		Assert.True(
			TheConsoleOwnsTheInput(Plugin("OnlineUiOverlay.cs")),
			"the command console owns the input while it is open, so a launcher click must not open the window behind it — the blocker that used to swallow that click is gone by design");

	[Fact]
	public void ThePointerFactsDieWithTheSurface() =>
		Assert.True(
			RetractsThePointerFacts(Adapter("OnlineUiSurfaceHost.cs")),
			"the views report a FLIP and a rebuilt one starts un-hovered, so the facts must be retracted where the views die: a stale one is global and blocks every world ping and every in-world right-click");

	public static TheoryData<string, string, string, string, string> Mutations => new()
	{
		{ nameof(TheGuardNeverBlocksCuosOwnSurface), "adapter-root/OnlineMenuInputGuard.cs", "&& !OnlineUiSurfaceMarker.IsInside(canvas);", "&& true;", "a guard that blocks CUO's own canvas again" },
		{ nameof(TheGuardNeverBlocksCuosOwnSurface), "adapter-root/OnlineMenuInputGuard.cs", "private int CreateRaycastBlockers()\n\t{\n\t\tvar created = 0;\n\t\tforeach (var canvas in Object.FindObjectsOfType<Canvas>())\n\t\t{\n\t\t\tif (!IsBlockable(canvas))", "private int CreateRaycastBlockers()\n\t{\n\t\tvar created = 0;\n\t\tforeach (var canvas in Object.FindObjectsOfType<Canvas>())\n\t\t{\n\t\t\tif (canvas == null)", "the sweep that stops asking the ownership rule" },
		{ nameof(TheGuardNeverBlocksCuosOwnSurface), "adapter-root/OnlineMenuInputGuard.cs", "|| OnlineUiSurfaceMarker.IsInside(button)", "|| false", "a modal sweep that disables a control CUO's own surface put there" },
		{ nameof(TheSurfaceMarksItsOwnCanvas), "adapter/OnlineUiSurfaceHost.cs", "root.AddComponent<OnlineUiSurfaceMarker>();", "// (no marker)", "a surface the guard cannot recognise as CUO's own" },
		{ nameof(TheLauncherEntersThePointerCensus), "plugin/OnlineUiHost.cs", "_onlineUi.SetPointerOverLauncher(true);", "// the launcher's pointer fact is dropped", "a launcher hover that never reaches the census" },
		{ nameof(ThePanelsEnterThePointerCensus), "plugin/OnlineUiHost.cs", "_onlineUi.SetPointerOverQuickPanel(true);", "// the quick panel's pointer fact is dropped", "a quick panel hover that never reaches the census" },
		{ nameof(ThePanelsEnterThePointerCensus), "plugin/OnlineUiHost.cs", "_onlineUi.SetPointerOverContextMenu(true);", "// the context menu's pointer fact is dropped", "a context menu hover that never reaches the census" },
		{ nameof(ThePanelsPollTheirOwnRect), "adapter/OnlineUiSurfaceHost.cs", "_quickPanel?.PollPointer(_intents);", "// the quick panel's pointer is never polled", "a panel whose pointer fact never reaches the plugin" },
		{ nameof(ThePanelsPollTheirOwnRect), "adapter/OnlineUiSurfaceHost.cs", "_contextMenu?.PollPointer(_intents);", "// the context menu's pointer is never polled", "a menu whose pointer fact never reaches the plugin" },
		{ nameof(TheRetiredRectangleApiIsGoneFromTheSource), "adapter-root/OnlineMenuInputGuard.cs", "private static bool IsBlockable(Canvas canvas)", "internal void SetOnlineUiScopedBlocks() { }\n\n\tprivate static bool IsBlockable(Canvas canvas)", "the retired rectangle-list API regrown on the guard" },
		{ nameof(BothWorldInputPathsAskTheCensus), "plugin/OnlineUiOverlay.cs", "return _pointerCensus.BlocksWorldPing();", "return IsCommandConsoleOpen || IsWindowVisible;", "a ping path that only knows the modal flag again" },
		{ nameof(BothWorldInputPathsAskTheCensus), "plugin/OnlineUiOverlay.cs", "return _pointerCensus.BlocksWorldMenu();", "return _contextMenu.IsOpen;", "a right-click path that keeps its own list of surfaces" },
		{ nameof(TheConsoleKeepsTheLauncherFromOpeningASecondSurface), "plugin/OnlineUiOverlay.cs", "Plugin.Logger.LogInfo(\"Online UI launcher click ignored: the command console owns the input.\");", "// the launcher opens the window behind the console", "a launcher that opens a second surface behind the console" },
		{ nameof(TheConsoleKeepsTheLauncherFromOpeningASecondSurface), "plugin/OnlineUiOverlay.cs", "if (_commandOverlay.IsOpen)\n\t\t{\n\t\t\tPlugin.Logger.LogInfo(\"Online UI launcher click ignored: the command console owns the input.\");\n\t\t\treturn;\n\t\t}\n\n\t\t_window.State.Visible = !_window.State.Visible;", "_window.State.Visible = !_window.State.Visible;\n\n\t\tif (_commandOverlay.IsOpen)\n\t\t{\n\t\t\tPlugin.Logger.LogInfo(\"Online UI launcher click ignored: the command console owns the input.\");\n\t\t\treturn;\n\t\t}", "a refusal that runs after the window has already opened" },
		{ nameof(ThePointerFactsDieWithTheSurface), "adapter/OnlineUiSurfaceHost.cs", "_intents.Enqueue(new OnlineUiIntent(OnlineUiIntentKind.LauncherHoverLeft));", "// the launcher's stale pointer fact is kept", "a surface that leaves the census holding a fact its view no longer reports" },
		{ nameof(ThePointerFactsDieWithTheSurface), "adapter/OnlineUiSurfaceHost.cs", "_intents.Enqueue(new OnlineUiIntent(OnlineUiIntentKind.QuickPanelHoverLeft));", "// the quick panel's stale pointer fact is kept", "a surface that retracts three facts and forgets the fourth" },
	};

	/// <summary>
	/// Every pin of this class, against a broken source: the anchor must be in the real file (so a text
	/// drift cannot turn the sample into a tautology), the replacement must change it, and the pin's own
	/// matcher must then be false.
	/// </summary>
	[Theory]
	[MemberData(nameof(Mutations))]
	public void EveryPinRejectsItsMutation(string pin, string file, string anchor, string replacement, string why)
	{
		var source = Read(file);
		Assert.True(
			source.Contains(anchor, StringComparison.Ordinal),
			$"{pin}: the mutation anchor `{anchor}` is not in {file} — re-anchor this mutation before trusting it");

		var broken = source.Replace(anchor, replacement);
		Assert.NotEqual(source, broken);
		Assert.False(Matcher(pin)(broken), $"{pin}: {why}");
	}

	private static Func<string, bool> Matcher(string pin) => pin switch
	{
		nameof(TheGuardNeverBlocksCuosOwnSurface) => GuardsItsOwnSurface,
		nameof(TheSurfaceMarksItsOwnCanvas) => MarksItsOwnCanvas,
		nameof(TheLauncherEntersThePointerCensus) => FeedsTheLauncherFact,
		nameof(ThePanelsEnterThePointerCensus) => FeedsThePanelFacts,
		nameof(ThePanelsPollTheirOwnRect) => PollsBothPanelRects,
		nameof(TheRetiredRectangleApiIsGoneFromTheSource) => source => NoScopedRectangleApi([source]),
		nameof(BothWorldInputPathsAskTheCensus) => broken => BothPathsAskTheCensus(broken, Plugin("OnlineUiPlayerContextMenu.cs")),
		nameof(TheConsoleKeepsTheLauncherFromOpeningASecondSurface) => TheConsoleOwnsTheInput,
		nameof(ThePointerFactsDieWithTheSurface) => RetractsThePointerFacts,
		_ => throw new InvalidOperationException($"no matcher is registered for the pin `{pin}`"),
	};

	/// <summary>
	/// The guard's ownership rule: one predicate every screen-space sweep asks, and the modal sweep's own
	/// version of it for the game's custom buttons. The scoped rectangle sweep is gone with the panels (S5),
	/// so the full-screen blocker is the only canvas sweep left — the count is the pin's census of them.
	/// COVERAGE LIMIT, recorded rather than implied: the count sees the sweeps that exist — a THIRD sweep
	/// added later that never asks the predicate would not raise the count and would not be caught here.
	/// </summary>
	private static bool GuardsItsOwnSurface(string guardSource)
	{
		var flat = Flatten(guardSource);
		var blockable = Flatten(ExtractMember(guardSource, "private static bool IsBlockable("));

		return blockable.Contains("&& !OnlineUiSurfaceMarker.IsInside(canvas);", StringComparison.Ordinal)
			&& CountOf(flat, "if (!IsBlockable(canvas))") == 1
			&& flat.Contains("|| OnlineUiSurfaceMarker.IsInside(button)", StringComparison.Ordinal);
	}

	/// <summary>The marker is added to the root the surface builds, before anything is built under it.</summary>
	private static bool MarksItsOwnCanvas(string surfaceSource)
	{
		var flat = Flatten(surfaceSource);
		var marker = flat.IndexOf("root.AddComponent<OnlineUiSurfaceMarker>();", StringComparison.Ordinal);

		return marker >= 0
			&& marker < flat.IndexOf("OnlineUiLauncherView.TryCreate(root.transform", StringComparison.Ordinal)
			&& marker < flat.IndexOf("OnlineUiWindowView.Create(root.transform", StringComparison.Ordinal);
	}

	/// <summary>Both directions of the launcher's pointer flip reach the census, beside the fade's own
	/// input — one fact, two readers.</summary>
	private static bool FeedsTheLauncherFact(string hostSource)
	{
		var flat = Flatten(hostSource);

		return flat.Contains(
				"case OnlineUiIntentKind.LauncherHoverEntered: _launcherHovered = true; _onlineUi.SetPointerOverLauncher(true); break;",
				StringComparison.Ordinal)
			&& flat.Contains(
				"case OnlineUiIntentKind.LauncherHoverLeft: _launcherHovered = false; _onlineUi.SetPointerOverLauncher(false); break;",
				StringComparison.Ordinal);
	}

	/// <summary>Both panels' flips reach the census, in both directions each: four facts, four cases.</summary>
	private static bool FeedsThePanelFacts(string hostSource)
	{
		var flat = Flatten(hostSource);

		return flat.Contains(
				"case OnlineUiIntentKind.QuickPanelHoverEntered: _onlineUi.SetPointerOverQuickPanel(true); break;",
				StringComparison.Ordinal)
			&& flat.Contains(
				"case OnlineUiIntentKind.QuickPanelHoverLeft: _onlineUi.SetPointerOverQuickPanel(false); break;",
				StringComparison.Ordinal)
			&& flat.Contains(
				"case OnlineUiIntentKind.ContextMenuHoverEntered: _onlineUi.SetPointerOverContextMenu(true); break;",
				StringComparison.Ordinal)
			&& flat.Contains(
				"case OnlineUiIntentKind.ContextMenuHoverLeft: _onlineUi.SetPointerOverContextMenu(false); break;",
				StringComparison.Ordinal);
	}

	/// <summary>
	/// Both panels are applied and polled on every pushed frame, and each polls its own rectangle through
	/// the same call the window uses — the fact is the surface's, and the plugin only holds the last answer.
	/// </summary>
	private static bool PollsBothPanelRects(string surfaceSource)
	{
		var flat = Flatten(surfaceSource);

		return flat.Contains("ApplyPanel(_quickPanel, frame.QuickPanel); _quickPanel?.PollPointer(_intents);", StringComparison.Ordinal)
			&& flat.Contains("ApplyPanel(_contextMenu, frame.ContextMenu); _contextMenu?.PollPointer(_intents);", StringComparison.Ordinal)
			&& Flatten(Adapter("OnlineUiPanelView.cs"))
				.Contains("RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, camera)", StringComparison.Ordinal);
	}

	/// <summary>
	/// The rectangles the IMGUI panels needed are gone from the source: no scoped-block member, no GUI-space
	/// rectangle value, no raycast filter. The scan reads every <c>.cs</c> under <c>src/</c> with comments
	/// cut, so a mention in a doc comment is not an offence and a type regrown under a new name is the only
	/// thing this cannot see (a name-based scan is the shape that can be pinned).
	/// </summary>
	private static bool NoScopedRectangleApi(IReadOnlyList<string> sources) =>
		sources.All(source =>
			!source.Contains("SetOnlineUiScopedBlocks", StringComparison.Ordinal)
			&& !source.Contains("OnlineUiBlockRect", StringComparison.Ordinal)
			&& !source.Contains("OnlineScopedRaycastFilter", StringComparison.Ordinal));

	/// <summary>
	/// Both world paths ask the census and nothing else: the ping asks the Runtime's rule, the right-click
	/// path hands the menu the census's own answer, and the plugin keeps no second copy of any surface's
	/// pointer fact.
	/// </summary>
	private static bool BothPathsAskTheCensus(string overlaySource, string menuSource)
	{
		var flat = Flatten(overlaySource);

		return flat.Contains("return _pointerCensus.BlocksWorldPing();", StringComparison.Ordinal)
			&& flat.Contains(
				"_contextMenu.HandleInput(ctx, _pointerCensus.OverContextMenu, BlocksWorldMenu);",
				StringComparison.Ordinal)
			&& flat.Contains("return _pointerCensus.BlocksWorldMenu();", StringComparison.Ordinal)
			&& !flat.Contains("_pointerOverWindow", StringComparison.Ordinal)
			&& !flat.Contains("_contextMenu.Contains(", StringComparison.Ordinal)
			&& Flatten(menuSource).Contains("if (blockedByCuiSurface())", StringComparison.Ordinal);
	}

	/// <summary>
	/// The launcher's own opening rule refuses while the console owns the input, and says so at a level the
	/// plugin's default log configuration shows. The ORDER is part of the pin: a refusal that ran after the
	/// toggle would leave the window open behind the console with the line still in the file.
	/// </summary>
	private static bool TheConsoleOwnsTheInput(string overlaySource)
	{
		var body = Flatten(ExtractMember(overlaySource, "internal void ToggleWindow("));
		var refusal = body.IndexOf("if (_commandOverlay.IsOpen)", StringComparison.Ordinal);
		var toggle = body.IndexOf("_window.State.Visible = !_window.State.Visible;", StringComparison.Ordinal);

		return refusal >= 0
			&& toggle > refusal
			&& body.Contains("Plugin.Logger.LogInfo(", StringComparison.Ordinal);
	}

	/// <summary>
	/// Every pointer fact is retracted where the view that reported it dies — the launcher's, the window's
	/// and (S5) both panels'. The views report a FLIP only and a rebuilt one starts un-hovered, so a surface
	/// rebuilt while the pointer sat on one of them would otherwise leave the census holding "the pointer is
	/// over CUO's UI" for good — and that fact is global, so it would block every world ping and every
	/// in-world right-click.
	/// </summary>
	private static bool RetractsThePointerFacts(string surfaceSource)
	{
		var body = Flatten(ExtractMember(surfaceSource, "private void DestroySurface()"));

		return body.Contains("_intents.Enqueue(new OnlineUiIntent(OnlineUiIntentKind.LauncherHoverLeft));", StringComparison.Ordinal)
			&& body.Contains("_intents.Enqueue(new OnlineUiIntent(OnlineUiIntentKind.WindowHoverLeft));", StringComparison.Ordinal)
			&& body.Contains("_intents.Enqueue(new OnlineUiIntent(OnlineUiIntentKind.QuickPanelHoverLeft));", StringComparison.Ordinal)
			&& body.Contains("_intents.Enqueue(new OnlineUiIntent(OnlineUiIntentKind.ContextMenuHoverLeft));", StringComparison.Ordinal);
	}

	private static string Read(string file)
	{
		var separator = file.IndexOf('/');
		Assert.True(separator > 0, $"the mutation's file `{file}` must be written as `<tree>/<name>.cs`");
		var tree = file.Substring(0, separator);
		var name = file.Substring(separator + 1);
		return tree switch
		{
			"plugin" => Plugin(name),
			"adapter" => Adapter(name),
			"adapter-root" => AdapterRoot(name),
			_ => throw new InvalidOperationException($"unknown tree `{tree}` in `{file}`"),
		};
	}

	private static string Plugin(string fileName) => ReadNormalised(Path.Combine(PluginDirectory, fileName));

	private static string Adapter(string fileName) => ReadNormalised(Path.Combine(GameAdapterDirectory, "OnlineUi", fileName));

	/// <summary>The adapter's own root: the input guard and the surface host are not part of the
	/// <c>OnlineUi/</c> view folder, so the mutation rows name them separately.</summary>
	private static string AdapterRoot(string fileName) => ReadNormalised(Path.Combine(GameAdapterDirectory, fileName));

	/// <summary>Every <c>.cs</c> file under <c>src/</c>, comments cut — the scan surface of the "the retired
	/// rectangle API stays retired" rule.</summary>
	private static IReadOnlyList<string> SourceSources() =>
	[
		.. Directory.GetFiles(Path.Combine(FindRepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(path => StripComments(ReadNormalised(path))),
	];

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

	/// <summary>Comments cut and whitespace collapsed, so indentation and line endings cannot decide a
	/// pin; the file is read with its line endings normalised for the same reason (<c>dotnet format</c>
	/// rewrites the tree to CRLF).</summary>
	private static string Flatten(string source) =>
		string.Join(" ", StripComments(source).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

	private static string StripComments(string source)
	{
		var kept = new List<string>();
		var inBlock = false;
		foreach (var line in source.Split('\n'))
		{
			var text = line;
			if (inBlock)
			{
				var end = text.IndexOf("*/", StringComparison.Ordinal);
				if (end < 0)
				{
					kept.Add(string.Empty);
					continue;
				}

				text = text.Substring(end + 2);
				inBlock = false;
			}

			var block = text.IndexOf("/*", StringComparison.Ordinal);
			var lineComment = text.IndexOf("//", StringComparison.Ordinal);
			if (block >= 0 && (lineComment < 0 || block < lineComment))
			{
				kept.Add(text.Substring(0, block));
				if (text.IndexOf("*/", block, StringComparison.Ordinal) < 0)
				{
					inBlock = true;
				}

				continue;
			}

			kept.Add(lineComment < 0 ? text : text.Substring(0, lineComment));
		}

		return string.Join("\n", kept);
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

	private static string ReadNormalised(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

	private static string GameAdapterDirectory =>
		Path.Combine(FindRepositoryRoot(), "src", "CasualtiesUnknownOnline.GameAdapter");

	private static string PluginDirectory =>
		Path.Combine(FindRepositoryRoot(), "src", "CasualtiesUnknownOnline.Plugin");

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
