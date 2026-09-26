using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The last two player-facing IMGUI panels are controls of CUO's own surface (ticket
/// online-ui-art-and-controls-overhaul, S5): the quick panel and the in-world player context menu render
/// through the game's own row prefabs, on the canvas the launcher and the window already stand on, and the
/// machinery that existed only because they were IMGUI — the scoped rectangle blockers, the theme's panel
/// frame, the IMGUI member card — retired with them.
///
/// <para>
/// None of this is visible from a unit test: whether the panels read as the game, whether a click lands on
/// them at the game's UI scale, and whether the docked corner and the menu's own corner are where the
/// player expects are run observations. What is pinned here is the shape: each panel builds a model instead
/// of drawing, one panel view serves both, the shared row machinery and the Runtime's placement rule are
/// reused, and the shared close id holds across the seam — each with a mutation of the REAL source as its
/// negative sample.
/// </para>
/// </summary>
public sealed class OnlineUiPanelSurfacePinTests
{
	[Fact]
	public void TheQuickPanelBuildsAModelInsteadOfDrawing() =>
		Assert.True(
			BuildsAModel(Plugin("OnlineUiQuickPanel.cs"), "return OnlineUiPanelModel.Docked("),
			"the quick panel must build a model for the game's own controls instead of drawing: an IMGUI panel would be invisible to the game's EventSystem again, which is the whole reason the surface exists");

	[Fact]
	public void TheContextMenuBuildsAModelInsteadOfDrawing() =>
		Assert.True(
			BuildsAModel(Plugin("OnlineUiPlayerContextMenu.cs"), "return OnlineUiPanelModel.AtPoint("),
			"the in-world menu must build a model for the game's own controls instead of drawing, and its corner must travel as a value");

	[Fact]
	public void TheSurfaceCarriesOnePanelViewForBoth() =>
		Assert.True(
			CarriesBothPanels(Adapter("OnlineUiSurfaceHost.cs")),
			"one panel view serves both panels, built with the surface and polled on every pushed frame");

	[Fact]
	public void ThePanelsKeepTheWindowsRowMachinery() =>
		Assert.True(
			UsesTheSharedRowMachinery(AdapterPanelView()),
			"a panel must render through the same control view and wrapping rule the pages use, or the migration grows a second row renderer that drifts from the first");

	[Fact]
	public void EachPanelPollsItsOwnRectAndReportsTheCallersFact() =>
		Assert.True(
			PollsItsOwnRect(AdapterPanelView()),
			"each panel's pointer fact must be polled from its own rectangle — uGUI's enter/exit callbacks fire on movement, and both world input paths judge the click by where the pointer is");

	[Fact]
	public void TheSurfaceCoversTheGamesCanvas() =>
		Assert.True(
			CoversTheGameCanvas(Adapter("OnlineUiSurfaceHost.cs")),
			"CUO's canvas must cover the game's canvas: a nested canvas is not resized by Unity, so without this every anchored control — the launcher, the window, the docked quick panel — is placed against an arbitrary rect instead of the screen");

	[Fact]
	public void ThePointAnchoredPanelIsPlacedByTheRuntimesRule() =>
		Assert.True(
			PlacedByTheRuntimeRule(AdapterPanelView()),
			"the context menu's corner must come from the Runtime's clamp, with the panel's own laid-out height — a placement rule kept in the view is a rule no test can reach");

	[Fact]
	public void ThePanelsIdsAreNamespacedAndTheCloseIsShared() =>
		Assert.True(
			NamespacesItsIds(Plugin("OnlineUiQuickPanel.cs"), Plugin("OnlineUiPlayerContextMenu.cs"))
				&& SharesThePanelsCloseId(Plugin("OnlineUiQuickPanel.cs"), AdapterPanelView()),
			"the three surfaces share one action table, so a panel's ids must be namespaced and its close control's id must be the one the plugin registered");

	[Fact]
	public void ThePanelRectsAreTheImguiRects() =>
		Assert.True(
			KeepsItsRect(
				Plugin("OnlineUiQuickPanel.cs"),
				Plugin("OnlineUiPlayerContextMenu.cs"),
				AdapterPanelView()),
			"the panels must keep the rects the player last saw them at — the quick panel's width and its bottom-right dock, the menu's width — so the migration is a change of renderer and not of placement (the S5 review's F9: nothing pinned these, so a changed constant kept every pin green)");

	[Fact]
	public void ThePanelsOwnIdentityHolds() =>
		Assert.True(
			ThePanelsHaveTheirOwnIdsAndSwallowThePointer(AdapterPanelView(), RuntimeControlIds()),
			"the quick panel's close control must be its OWN id (not the window's), and a panel's frame must swallow the pointer — otherwise a click inside the panel falls through to the world behind it");

	public static TheoryData<string, string, string, string, string> Mutations => new()
	{
		{ nameof(TheQuickPanelBuildsAModelInsteadOfDrawing), "plugin/OnlineUiQuickPanel.cs", "return OnlineUiPanelModel.Docked(", "GUILayout.Label(ctx.T(\"quick.title\"));\n\t\t\treturn null;\n\t\t}\n\n\t\tprivate static OnlineUiPanelModel? Drawn(", "a quick panel drawn in IMGUI again" },
		{ nameof(TheContextMenuBuildsAModelInsteadOfDrawing), "plugin/OnlineUiPlayerContextMenu.cs", "return OnlineUiPanelModel.AtPoint(", "GUI.Label(new Rect(0f, 0f, 1f, 1f), \"menu\");\n\t\treturn null;\n\t\t//", "a context menu drawn in IMGUI again" },
		{ nameof(TheSurfaceCarriesOnePanelViewForBoth), "adapter/OnlineUiSurfaceHost.cs", "QuickPanelName,", "RootName,", "a second panel that is never built" },
		{ nameof(TheSurfaceCarriesOnePanelViewForBoth), "adapter/OnlineUiSurfaceHost.cs", "ApplyPanel(_contextMenu, frame.ContextMenu);", "ApplyPanel(_quickPanel, frame.QuickPanel);", "a menu that is never applied" },
		{ nameof(ThePanelsKeepTheWindowsRowMachinery), "adapter/OnlineUiPanelView.cs", "var lines = OnlineUiRowLayout.LineOf(widths, ContentWidth, LineSpacing);", "var lines = new int[widths.Length];", "a panel that wraps its rows by no rule" },
		{ nameof(ThePanelsKeepTheWindowsRowMachinery), "adapter/OnlineUiPanelView.cs", "_rows.Add(new OnlineUiWindowRowView(LineSpacing));", "_rows.Add(default!);", "a panel that builds its own rows instead of the shared row view" },
		{ nameof(EachPanelPollsItsOwnRectAndReportsTheCallersFact), "adapter/OnlineUiPanelView.cs", "&& RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, camera);", "&& true;", "a pointer fact that never asks the pointer" },
		{ nameof(EachPanelPollsItsOwnRectAndReportsTheCallersFact), "adapter/OnlineUiPanelView.cs", "intents.Enqueue(new OnlineUiIntent(hovered ? _hoverEntered : _hoverLeft));", "intents.Enqueue(new OnlineUiIntent(OnlineUiIntentKind.LauncherHoverEntered));", "a panel that reports someone else's fact" },
		{ nameof(TheSurfaceCoversTheGamesCanvas), "adapter/OnlineUiSurfaceHost.cs", "canvasRect.anchorMax = Vector2.one;", "canvasRect.anchorMax = Vector2.zero;", "a canvas that does not cover the game's" },
		{ nameof(ThePointAnchoredPanelIsPlacedByTheRuntimesRule), "adapter/OnlineUiPanelView.cs", "var corner = OnlineUiPanelPlacement.ForPointer(", "var corner = new OnlineUiPanelCorner(_point.x, _point.y);\n\t\t_ = OnlineUiPanelPlacement.ForPointer(", "a corner that skips the Runtime's clamp" },
		{ nameof(ThePointAnchoredPanelIsPlacedByTheRuntimesRule), "adapter/OnlineUiPanelView.cs", "LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);", "// the height is read before the layout exists", "a clamp that runs before the panel has a height" },
		{ nameof(ThePointAnchoredPanelIsPlacedByTheRuntimesRule), "adapter/OnlineUiPanelView.cs", "\t\t\tbounds.width,", "\t\t\tScreen.width,", "a clamp that mixes the canvas's units with screen pixels (the review's F1: correct only at canvas scale 1)" },
		{ nameof(ThePointAnchoredPanelIsPlacedByTheRuntimesRule), "adapter/OnlineUiPanelView.cs", "if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, _point, camera, out var pointer))", "var pointer = _point; if (false)", "a pointer that is never converted out of screen pixels" },
		{ nameof(ThePanelsIdsAreNamespacedAndTheCloseIsShared), "plugin/OnlineUiQuickPanel.cs", "private const string IdPrefix = \"quick.\";", "private const string IdPrefix = \"\";", "a panel whose ids collide with the window's" },
		{ nameof(ThePanelsIdsAreNamespacedAndTheCloseIsShared), "plugin/OnlineUiQuickPanel.cs", "actions[OnlineUiControlIds.QuickPanelClose] = _ => Close();", "actions[\"quick.dismiss\"] = _ => Close();", "a close control whose two halves disagree about its id" },
		{ nameof(ThePanelRectsAreTheImguiRects), "plugin/OnlineUiQuickPanel.cs", "internal const float Width = 340f;", "internal const float Width = 300f;", "a quick panel that moved or shrank" },
		{ nameof(ThePanelRectsAreTheImguiRects), "plugin/OnlineUiPlayerContextMenu.cs", "internal const float Width = 240f;", "internal const float Width = 200f;", "a menu that moved or shrank" },
		{ nameof(ThePanelRectsAreTheImguiRects), "adapter/OnlineUiPanelView.cs", "internal const float DockMargin = 16f;", "internal const float DockMargin = 0f;", "a docked panel that stopped keeping its margin" },
		{ nameof(ThePanelsOwnIdentityHolds), "runtime/OnlineUiControlIds.cs", "public const string QuickPanelClose = \"quick.close\";", "public const string QuickPanelClose = \"window.close\";", "a panel close control that answers the window's id" },
		{ nameof(ThePanelsOwnIdentityHolds), "adapter/OnlineUiPanelView.cs", "panel.raycastTarget = true;", "panel.raycastTarget = false;", "a panel that lets clicks fall through to the world" },
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
		nameof(TheQuickPanelBuildsAModelInsteadOfDrawing) => broken => BuildsAModel(broken, "return OnlineUiPanelModel.Docked("),
		nameof(TheContextMenuBuildsAModelInsteadOfDrawing) => broken => BuildsAModel(broken, "return OnlineUiPanelModel.AtPoint("),
		nameof(TheSurfaceCarriesOnePanelViewForBoth) => CarriesBothPanels,
		nameof(ThePanelsKeepTheWindowsRowMachinery) => UsesTheSharedRowMachinery,
		nameof(EachPanelPollsItsOwnRectAndReportsTheCallersFact) => PollsItsOwnRect,
		nameof(TheSurfaceCoversTheGamesCanvas) => CoversTheGameCanvas,
		nameof(ThePointAnchoredPanelIsPlacedByTheRuntimesRule) => PlacedByTheRuntimeRule,
		nameof(ThePanelsIdsAreNamespacedAndTheCloseIsShared) => broken => NamespacesItsIds(broken, Plugin("OnlineUiPlayerContextMenu.cs"))
			&& SharesThePanelsCloseId(broken, AdapterPanelView()),
		nameof(ThePanelRectsAreTheImguiRects) => broken => KeepsItsRect(
			broken.Contains("class OnlineUiQuickPanel", StringComparison.Ordinal) ? broken : Plugin("OnlineUiQuickPanel.cs"),
			broken.Contains("class OnlineUiPlayerContextMenu", StringComparison.Ordinal) ? broken : Plugin("OnlineUiPlayerContextMenu.cs"),
			broken.Contains("class OnlineUiPanelView", StringComparison.Ordinal) ? broken : AdapterPanelView()),
		nameof(ThePanelsOwnIdentityHolds) => broken => broken.Contains("class OnlineUiControlIds", StringComparison.Ordinal)
			? ThePanelsHaveTheirOwnIdsAndSwallowThePointer(AdapterPanelView(), broken)
			: ThePanelsHaveTheirOwnIdsAndSwallowThePointer(broken, RuntimeControlIds()),
		_ => throw new InvalidOperationException($"no matcher is registered for the pin `{pin}`"),
	};

	/// <summary>
	/// A panel plugin builds a model and draws nothing. The draw fingerprints are the IMGUI calls
	/// themselves, so a panel regrown in IMGUI fails here wherever it is drawn from.
	/// </summary>
	private static bool BuildsAModel(string pluginSource, string factoryCall)
	{
		var flat = Flatten(pluginSource);

		return !DrawsImgui(flat) && flat.Contains(factoryCall, StringComparison.Ordinal);
	}

	/// <summary>One panel view for two panels, built with the surface and driven on every pushed frame.</summary>
	private static bool CarriesBothPanels(string surfaceSource)
	{
		var flat = Flatten(surfaceSource);

		return flat.Contains("_quickPanel = OnlineUiPanelView.Create( QuickPanelName,", StringComparison.Ordinal)
			&& flat.Contains("_contextMenu = OnlineUiPanelView.Create( ContextMenuName,", StringComparison.Ordinal)
			&& flat.Contains("ApplyPanel(_quickPanel, frame.QuickPanel); _quickPanel?.PollPointer(_intents);", StringComparison.Ordinal)
			&& flat.Contains("ApplyPanel(_contextMenu, frame.ContextMenu); _contextMenu?.PollPointer(_intents);", StringComparison.Ordinal);
	}

	/// <summary>One panel view for two panels: the same control view, the same row view, the same wrapping
	/// rule the window's pages use.</summary>
	private static bool UsesTheSharedRowMachinery(string panelViewSource)
	{
		var flat = Flatten(panelViewSource);

		return flat.Contains("_rows.Add(new OnlineUiWindowRowView(LineSpacing));", StringComparison.Ordinal)
			&& flat.Contains(
				"var lines = OnlineUiRowLayout.LineOf(widths, ContentWidth, LineSpacing);",
				StringComparison.Ordinal)
			&& flat.Contains("OnlineUiControlView.Create(element, parent, _typography, _report);", StringComparison.Ordinal);
	}

	/// <summary>The poll is the window's rule: the pointer against the panel's own rect, a FLIP reported with
	/// the two kinds the caller handed this view.</summary>
	private static bool PollsItsOwnRect(string panelViewSource)
	{
		var flat = Flatten(panelViewSource);

		return flat.Contains("var hovered = _root.activeInHierarchy", StringComparison.Ordinal)
			&& flat.Contains(
				"&& RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, camera);",
				StringComparison.Ordinal)
			&& flat.Contains("intents.Enqueue(new OnlineUiIntent(hovered ? _hoverEntered : _hoverLeft));", StringComparison.Ordinal)
			&& flat.Contains("if (hovered == _hovered) { return; }", StringComparison.Ordinal);
	}

	/// <summary>CUO's canvas stretches over the game's, which is what makes "the bottom-right corner" mean the
	/// player's bottom-right corner.</summary>
	private static bool CoversTheGameCanvas(string surfaceSource)
	{
		var flat = Flatten(surfaceSource);

		return flat.Contains("var canvasRect = (RectTransform)root.transform;", StringComparison.Ordinal)
			&& flat.Contains("canvasRect.anchorMin = Vector2.zero;", StringComparison.Ordinal)
			&& flat.Contains("canvasRect.anchorMax = Vector2.one;", StringComparison.Ordinal)
			&& flat.Contains("canvasRect.offsetMin = Vector2.zero;", StringComparison.Ordinal)
			&& flat.Contains("canvasRect.offsetMax = Vector2.zero;", StringComparison.Ordinal);
	}

	/// <summary>
	/// The menu's corner comes from the Runtime's rule, the panel is laid out first so the rule has a real
	/// height to clamp against, and the rule is asked in the CANVAS's units — the pointer converted into the
	/// parent rect once, the panel's own size, and the parent's bounds — never in screen pixels (the review's
	/// F1: pixels and canvas units only agree at a canvas scale of 1).
	/// </summary>
	private static bool PlacedByTheRuntimeRule(string panelViewSource)
	{
		var flat = Flatten(panelViewSource);

		return flat.Contains("LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);", StringComparison.Ordinal)
			&& flat.Contains(
				"if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, _point, camera, out var pointer))",
				StringComparison.Ordinal)
			&& flat.Contains("var corner = OnlineUiPanelPlacement.ForPointer(", StringComparison.Ordinal)
			&& flat.Contains("_rect.rect.width, _rect.rect.height, bounds.width, bounds.height);", StringComparison.Ordinal)
			&& flat.Contains("_rect.localPosition = new Vector3(bounds.xMin + corner.X, bounds.yMin + corner.Top, 0f);", StringComparison.Ordinal);
	}

	/// <summary>The panels keep the rects the player last saw them at.</summary>
	private static bool KeepsItsRect(string quickPanelSource, string menuSource, string panelViewSource) =>
		Flatten(quickPanelSource).Contains("internal const float Width = 340f;", StringComparison.Ordinal)
		&& Flatten(menuSource).Contains("internal const float Width = 240f;", StringComparison.Ordinal)
		&& Flatten(panelViewSource).Contains("internal const float DockMargin = 16f;", StringComparison.Ordinal);

	/// <summary>The quick panel's close control carries its OWN id (the window keeps its own), and a panel's
	/// frame is a raycast target so a click inside it never reaches the world behind it.</summary>
	private static bool ThePanelsHaveTheirOwnIdsAndSwallowThePointer(string panelViewSource, string controlIdsSource) =>
		Flatten(controlIdsSource).Contains("public const string WindowClose = \"window.close\";", StringComparison.Ordinal)
		&& Flatten(controlIdsSource).Contains("public const string QuickPanelClose = \"quick.close\";", StringComparison.Ordinal)
		&& Flatten(panelViewSource).Contains("panel.raycastTarget = true;", StringComparison.Ordinal);

	/// <summary>Each panel namespaces its ids, so the shared action table cannot confuse one surface's click
	/// with another's.</summary>
	private static bool NamespacesItsIds(string quickPanelSource, string menuSource) =>
		Flatten(quickPanelSource).Contains("private const string IdPrefix = \"quick.\";", StringComparison.Ordinal)
		&& Flatten(menuSource).Contains("private const string IdPrefix = \"menu.\";", StringComparison.Ordinal);

	/// <summary>The quick panel's close control is chrome the surface builds from the model's id, and the
	/// plugin registers that exact id — the window's contract, one panel over.</summary>
	private static bool SharesThePanelsCloseId(string quickPanelSource, string panelViewSource) =>
		Flatten(quickPanelSource).Contains("actions[OnlineUiControlIds.QuickPanelClose] = _ => Close();", StringComparison.Ordinal)
		&& Flatten(quickPanelSource).Contains("OnlineUiControlIds.QuickPanelClose, Width, page.Rows);", StringComparison.Ordinal)
		&& Flatten(panelViewSource).Contains("OnlineUiElementModel.Button(closeId, CloseCaption, CloseWidth)", StringComparison.Ordinal);

	/// <summary>The IMGUI fingerprints: a layout call, a fixed-rect draw, or a theme style. Named after what
	/// a panel does, not after one spelling of it.</summary>
	private static bool DrawsImgui(string code) =>
		code.Contains("GUILayout.", StringComparison.Ordinal)
		|| code.Contains("GUI.", StringComparison.Ordinal)
		|| code.Contains("GUIStyle", StringComparison.Ordinal);

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
			"runtime" => ReadNormalised(Path.Combine(RuntimeDirectory, name)),
			_ => throw new InvalidOperationException($"unknown tree `{tree}` in `{file}`"),
		};
	}

	private static string Plugin(string fileName) => ReadNormalised(Path.Combine(PluginDirectory, fileName));

	private static string Adapter(string fileName) => ReadNormalised(Path.Combine(GameAdapterDirectory, "OnlineUi", fileName));

	private static string AdapterPanelView() => Adapter("OnlineUiPanelView.cs");

	/// <summary>The Runtime's own file: the panel's close id lives there because both halves address it.</summary>
	private static string RuntimeControlIds() => ReadNormalised(Path.Combine(RuntimeDirectory, "OnlineUiControlIds.cs"));

	/// <summary>Comments cut and whitespace collapsed, so indentation and line endings cannot decide a pin.</summary>
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

	private static string ReadNormalised(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

	private static string GameAdapterDirectory =>
		Path.Combine(FindRepositoryRoot(), "src", "CasualtiesUnknownOnline.GameAdapter");

	private static string PluginDirectory =>
		Path.Combine(FindRepositoryRoot(), "src", "CasualtiesUnknownOnline.Plugin");

	private static string RuntimeDirectory =>
		Path.Combine(FindRepositoryRoot(), "src", "CasualtiesUnknownOnline.Runtime", "OnlineUi");

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
