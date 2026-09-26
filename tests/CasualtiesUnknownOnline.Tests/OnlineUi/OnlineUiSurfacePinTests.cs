using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The Online UI's native surface and its launcher (ticket online-ui-art-and-controls-overhaul, S2a). The
/// launcher moved off IMGUI onto the game's own control, which means the path from the idle rule to the
/// pixels now crosses a boundary no unit test in this tree can run: the plugin asks the Runtime rule and
/// pushes the answer, and the adapter puts it on a Unity object graph. Both halves are pinned here as
/// source facts, with the rules that must not regress spelled out — the launcher is the GAME's own
/// prefab, at the rect the IMGUI launcher occupied, faded on the whole surface, hovered through a polled
/// pointer fact, clickable into an intent, and it plays the game's own click.
///
/// <para>
/// Every pin carries a mutation of the REAL source as its negative sample, so none of them can pass
/// vacuously, and the anchors are matched after comment-stripping and whitespace-flattening, so
/// indentation, line endings and a comment that merely names an API cannot decide a result.
/// </para>
/// </summary>
public sealed class OnlineUiSurfacePinTests
{
	[Fact]
	public void ThePluginDrivesTheSurfaceWithTheRulesAnswer()
	{
		var host = PluginSource("OnlineUiHost.cs");

		Assert.True(
			DrivesTheSurfaceWithTheRule(host),
			"the plugin must ask the idle rule with the runtime clock and push its answer: a hard-coded opacity, a discarded answer or a hand-built caption is the defect this pin exists for");
	}

	[Fact]
	public void ThePinRejectsAPluginThatHardCodesTheOpacity()
	{
		var host = PluginSource("OnlineUiHost.cs");
		var broken = host.Replace("_launcherFade.Evaluate(_time.NowMs, _launcherHovered)", "1f");

		Assert.True(host != broken, "the opacity mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(DrivesTheSurfaceWithTheRule(broken));
	}

	[Fact]
	public void ThePinRejectsAPluginThatBuildsTheCaptionItself()
	{
		var host = PluginSource("OnlineUiHost.cs");
		var broken = host.Replace("OnlineUiLauncherText.Label(caption, open)", "caption");

		Assert.True(host != broken, "the caption mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(DrivesTheSurfaceWithTheRule(broken));
	}

	/// <summary>The window rides in the same frame: a push that carries no window model shows no window at
	/// all, which is the regression the S2b half of this pin exists for.</summary>
	[Fact]
	public void ThePinRejectsAPluginThatPushesNoWindow()
	{
		var host = PluginSource("OnlineUiHost.cs");
		var broken = host.Replace("_onlineUi.Window.Build(ctx)", "null");

		Assert.True(host != broken, "the window mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(DrivesTheSurfaceWithTheRule(broken));
	}

	[Fact]
	public void TheLaunchersClickKeepsTheWindowsOpeningRule()
	{
		var overlay = PluginSource("OnlineUiOverlay.cs");

		Assert.True(
			ToggleWindowKeepsTheLaunchersRule(overlay),
			"the launcher's click must toggle the window and open it on the Players page while a session runs — the rule the IMGUI launcher applied");
	}

	[Fact]
	public void ThePinRejectsAnOpeningRuleThatLandsOnTheJoinForm()
	{
		var overlay = PluginSource("OnlineUiOverlay.cs");
		var broken = overlay.Replace(" && role != SessionRole.None", string.Empty);

		Assert.True(overlay != broken, "the opening-rule mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(ToggleWindowKeepsTheLaunchersRule(broken));
	}

	[Fact]
	public void TheSurfaceHangsUnderTheGamesOwnCanvasAndRebuildsWhenItGoesAway()
	{
		var host = AdapterSource("OnlineUiSurfaceHost.cs");

		Assert.True(
			ParentsTheLiveSurfaceUnderTheGameCanvas(host),
			"CUO's live canvas must be a child of the game's own canvas (that is where the game's UI scale and sorting come from), carry a raycaster so the game's EventSystem can hit it, and rebuild itself when the canvas it hung on is gone");
	}

	[Fact]
	public void ThePinRejectsASurfaceWithoutARaycaster()
	{
		var host = AdapterSource("OnlineUiSurfaceHost.cs");
		var broken = host.Replace("root.AddComponent<GraphicRaycaster>();\n", string.Empty);

		Assert.True(host != broken, "the raycaster mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(ParentsTheLiveSurfaceUnderTheGameCanvas(broken));
	}

	/// <summary>The other half of the same pin: a surface that is never rebuilt once its canvas is gone
	/// dereferences destroyed objects on every frame. The pin requires the alive check; this is what proves
	/// removing it is caught.</summary>
	[Fact]
	public void ThePinRejectsASurfaceThatNeverRebuilds()
	{
		var host = AdapterSource("OnlineUiSurfaceHost.cs");
		var broken = host.Replace(
			"if (_root != null && _root.activeInHierarchy && _launcher != null)",
			"if (_root != null)");

		Assert.True(host != broken, "the rebuild mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(ParentsTheLiveSurfaceUnderTheGameCanvas(broken));
	}

	[Fact]
	public void TheLiveSurfacePrefersTheInRunCanvas()
	{
		var host = AdapterSource("OnlineUiSurfaceHost.cs");

		Assert.True(
			TheInRunCanvasComesFirst(host),
			"a live surface must hang on the player camera's canvas while a run is loaded — that canvas's scale IS PlayerCamera.uiScale — and fall back to the pre-run/menu canvas before a run exists");
	}

	[Fact]
	public void ThePinRejectsTheProbesCanvasOrder()
	{
		var host = AdapterSource("OnlineUiSurfaceHost.cs");
		var broken = host.Replace("PlayerCamera.main.mainCanvas", "PreRunScript.instance.mainCanvas");

		Assert.True(host != broken, "the canvas-order mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(TheInRunCanvasComesFirst(broken));
	}

	[Fact]
	public void TheLauncherIsTheGamesOwnControlAtTheLaunchersRect()
	{
		var view = AdapterSource("OnlineUiLauncherView.cs");

		Assert.True(
			TheLauncherIsTheGamesRowAtTheLaunchersRect(view),
			"the launcher must be the game's own button-row prefab, anchored top-right at the rect the IMGUI launcher occupied (a right and top margin of 12, 158 by 34)");
	}

	[Fact]
	public void ThePinRejectsALauncherThatMovedOrShrank()
	{
		var view = AdapterSource("OnlineUiLauncherView.cs");
		var broken = view.Replace("internal const float Width = 158f;", "internal const float Width = 120f;");

		Assert.True(view != broken, "the rect mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(TheLauncherIsTheGamesRowAtTheLaunchersRect(broken));
	}

	[Fact]
	public void ThePinRejectsAPlainButtonInsteadOfTheGamesRow()
	{
		var view = AdapterSource("OnlineUiLauncherView.cs");
		var broken = view.Replace(
			"var prefab = Resources.Load<GameObject>(PrefabPath);",
			"var prefab = (GameObject?)null;");

		Assert.True(view != broken, "the prefab mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(TheLauncherIsTheGamesRowAtTheLaunchersRect(broken));
	}

	[Fact]
	public void TheFadesOpacityReachesTheWholeLauncher()
	{
		var view = AdapterSource("OnlineUiLauncherView.cs");

		Assert.True(
			TheOpacityLandsOnTheWholeSurface(view),
			"the fade's alpha must land on the launcher's CanvasGroup, so the frame AND the caption fade together — the IMGUI half-fade is the defect this pin exists for");
	}

	[Fact]
	public void ThePinRejectsAnOpacityThatOnlyReachesHalfTheControl()
	{
		var view = AdapterSource("OnlineUiLauncherView.cs");
		var broken = view.Replace("_group.alpha = clamped;", "_group.alpha = 1f;");

		Assert.True(view != broken, "the opacity mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(TheOpacityLandsOnTheWholeSurface(broken));
	}

	[Fact]
	public void TheHoverFactIsPolledFromTheLaunchersRect()
	{
		var view = AdapterSource("OnlineUiLauncherView.cs");

		Assert.True(
			PollsThePointerAgainstTheRect(view),
			"hover must be polled from the launcher's rect and the current pointer — uGUI's enter/exit callbacks fire on pointer movement, so a launcher that appears under a stationary pointer would never report the hover that keeps it opaque");
	}

	[Fact]
	public void ThePinRejectsAHoverThatNeverAsksThePointer()
	{
		var view = AdapterSource("OnlineUiLauncherView.cs");
		var broken = view.Replace(
			"var hovered = RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, camera);",
			"var hovered = _hovered;");

		Assert.True(view != broken, "the hover mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(PollsThePointerAgainstTheRect(broken));
	}

	[Fact]
	public void AClickQueuesTheIntentAndPlaysTheGamesOwnClick()
	{
		var host = AdapterSource("OnlineUiSurfaceHost.cs");

		Assert.True(
			QueuesTheClickAndPlaysTheGamesSound(host),
			"a click must queue the toggle intent BEFORE anything else can fail, and play the game's own UI click so the launcher sounds like the rest of the interface");
	}

	[Fact]
	public void ThePinRejectsASilentLauncher()
	{
		var host = AdapterSource("OnlineUiSurfaceHost.cs");
		var broken = host.Replace("PlayerCamera.PlayUISound(ClickSound);\n", string.Empty);

		Assert.True(host != broken, "the sound mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(QueuesTheClickAndPlaysTheGamesSound(broken));
	}

	[Fact]
	public void TheSurfaceCreatesAnEventSystemOnlyWhenTheSceneLacksAnEnabledOne()
	{
		var host = AdapterSource("OnlineUiSurfaceHost.cs");

		Assert.True(
			CreatesAnEventSystemOnlyWhenTheSceneLacksAnEnabledOne(host),
			"CUO must never duplicate the game's EventSystem: it creates one only when the scene has none ENABLED, and its own copy is a child of CUO's surface");
	}

	[Fact]
	public void ThePinRejectsASecondEventSystem()
	{
		var host = AdapterSource("OnlineUiSurfaceHost.cs");
		var broken = host.Replace("if (EventSystem.current != null)", "if (false)");

		Assert.True(host != broken, "the EventSystem-guard mutation's anchor text is gone — re-anchor this mutation before trusting it");
		Assert.False(CreatesAnEventSystemOnlyWhenTheSceneLacksAnEnabledOne(broken));
	}

	[Fact]
	public void TheLauncherIsNoLongerDrawnInImgui()
	{
		var offenders = PluginSources()
			.Where(file => DrawsALauncherInImgui(file.Source))
			.Select(file => file.Path)
			.ToArray();

		Assert.True(
			offenders.Length == 0,
			$"these files still draw a launcher in IMGUI — a second launcher is worse than the first one: {string.Join(", ", offenders)}");

		// The window shell and its local state no longer own the launcher at all: the fade's state lives
		// with the frame push, and the caption is built from the Runtime's rule.
		Assert.False(
			StripComments(PluginSource("OnlineUiWindow.cs")).Contains("Launcher", StringComparison.Ordinal),
			"OnlineUiWindow must not carry launcher state any more");
		Assert.False(
			StripComments(PluginSource("OnlineUiWindowState.cs")).Contains("Launcher", StringComparison.Ordinal),
			"OnlineUiWindowState must not carry the launcher's fade or caption any more");
	}

	[Fact]
	public void TheImguiLauncherMatcher_FlagsTheRemovedDrawAndIgnoresAMention()
	{
		var theme = PluginSource("OnlineUiTheme.cs");

		Assert.False(DrawsALauncherInImgui(theme), "the theme no longer has a launcher draw");
		Assert.True(
			DrawsALauncherInImgui(theme + "\n\t\tvar style = OnlineUiTheme.Launcher(alpha);\n"),
			"a launcher draw regrown in the theme is the eviction this pin exists for");
		Assert.True(
			DrawsALauncherInImgui("var rect = new Rect(Screen.width - 170f, 12f, 158f, 34f);"),
			"the IMGUI rect is the same launcher, wherever it comes back");
		Assert.False(
			DrawsALauncherInImgui("/// <summary>The launcher is the game's own control now.</summary>\nvar launcher = 1;"),
			"a comment that merely mentions the launcher is not a draw");
	}

	/// <summary>
	/// The plugin's half: drain what the player did before pushing what to show, ask the Runtime rule with
	/// the runtime clock and the hover fact, build the caption from the Runtime's own rule, and hand the
	/// rule's answer AND the window's model to the surface. Every intent kind has its own case — the
	/// launcher's three, the window's own pointer fact, and the control interactions the window family
	/// reports (ticket online-ui-art-and-controls-overhaul, S2b) — and the click's case is the window's
	/// opening rule.
	/// </summary>
	private static bool DrivesTheSurfaceWithTheRule(string hostSource)
	{
		var flat = Flatten(hostSource);

		return flat.Contains("DrainSurfaceIntents(); PushSurfaceFrame(ctx);", StringComparison.Ordinal)
			&& flat.Contains(
				"_surface.Push(new OnlineUiFrame(label, _launcherFade.Evaluate(_time.NowMs, _launcherHovered), _onlineUi.Window.Build(ctx)));",
				StringComparison.Ordinal)
			&& flat.Contains("label = _launcherLabel = OnlineUiLauncherText.Label(caption, open);", StringComparison.Ordinal)
			&& flat.Contains(
				"case OnlineUiIntentKind.LauncherToggled: _onlineUi.ToggleWindow(_session.Role); break;",
				StringComparison.Ordinal)
			&& flat.Contains(
				"case OnlineUiIntentKind.LauncherHoverEntered: _launcherHovered = true; break;",
				StringComparison.Ordinal)
			&& flat.Contains(
				"case OnlineUiIntentKind.LauncherHoverLeft: _launcherHovered = false; break;",
				StringComparison.Ordinal)
			&& flat.Contains(
				"case OnlineUiIntentKind.WindowHoverEntered: _onlineUi.SetPointerOverWindow(true); break;",
				StringComparison.Ordinal)
			&& flat.Contains(
				"case OnlineUiIntentKind.WindowHoverLeft: _onlineUi.SetPointerOverWindow(false); break;",
				StringComparison.Ordinal);
	}

	/// <summary>The window's opening rule: a toggle, and the Home→Players landing while a session runs.</summary>
	private static bool ToggleWindowKeepsTheLaunchersRule(string overlaySource)
	{
		var body = Flatten(ExtractMember(overlaySource, "internal void ToggleWindow("));

		return body.Contains("_window.State.Visible = !_window.State.Visible;", StringComparison.Ordinal)
			&& body.Contains(
				"if (_window.State.Visible && _window.State.Page == OnlineUiPage.Home && role != SessionRole.None)",
				StringComparison.Ordinal)
			&& body.Contains("_window.State.Page = OnlineUiPage.Players;", StringComparison.Ordinal);
	}

	/// <summary>
	/// The live canvas: created under the game's canvas, sorting above the game's UI, with a raycaster so
	/// the EventSystem can hit it, and rebuilt when the canvas it hung on is gone (a scene change destroys
	/// CUO's canvas with the game's — a surface that dereferences destroyed objects would throw every
	/// frame).
	/// </summary>
	private static bool ParentsTheLiveSurfaceUnderTheGameCanvas(string hostSource)
	{
		var flat = Flatten(hostSource);

		return CountOf(hostSource, "root.transform.SetParent(parent, worldPositionStays: false);") == 1
			&& flat.Contains("var canvas = root.AddComponent<Canvas>();", StringComparison.Ordinal)
			&& flat.Contains("canvas.overrideSorting = true;", StringComparison.Ordinal)
			&& flat.Contains("canvas.sortingOrder = SortingOrder;", StringComparison.Ordinal)
			&& flat.Contains("root.AddComponent<GraphicRaycaster>();", StringComparison.Ordinal)
			&& flat.Contains("if (_root != null && _root.activeInHierarchy && _launcher != null) { return; }", StringComparison.Ordinal)
			&& !flat.Contains("throw ", StringComparison.Ordinal);
	}

	/// <summary>The in-run canvas is consulted first, the pre-run/menu canvas second — the order a LIVE
	/// surface needs (the probe reads the other way round: it only needs any canvas, and the menu one
	/// exists first).</summary>
	private static bool TheInRunCanvasComesFirst(string hostSource)
	{
		var camera = hostSource.IndexOf("PlayerCamera.main.mainCanvas", StringComparison.Ordinal);
		var preRun = hostSource.IndexOf("PreRunScript.instance.mainCanvas", StringComparison.Ordinal);
		return camera >= 0 && preRun > camera;
	}

	/// <summary>The game's own row, loaded by its path, stretched over the launcher's rect.</summary>
	private static bool TheLauncherIsTheGamesRowAtTheLaunchersRect(string viewSource)
	{
		var flat = Flatten(viewSource);

		return flat.Contains("internal const string PrefabPath = \"Special/GameSettingLanguage\";", StringComparison.Ordinal)
			&& flat.Contains("var prefab = Resources.Load<GameObject>(PrefabPath);", StringComparison.Ordinal)
			&& flat.Contains("rect.anchorMin = new Vector2(1f, 1f);", StringComparison.Ordinal)
			&& flat.Contains("rect.anchorMax = new Vector2(1f, 1f);", StringComparison.Ordinal)
			&& flat.Contains("rect.pivot = new Vector2(1f, 1f);", StringComparison.Ordinal)
			&& flat.Contains("rect.anchoredPosition = new Vector2(-RightMargin, -TopMargin);", StringComparison.Ordinal)
			&& flat.Contains("rect.sizeDelta = new Vector2(Width, Height);", StringComparison.Ordinal)
			&& flat.Contains("internal const float RightMargin = 12f;", StringComparison.Ordinal)
			&& flat.Contains("internal const float TopMargin = 12f;", StringComparison.Ordinal)
			&& flat.Contains("internal const float Width = 158f;", StringComparison.Ordinal)
			&& flat.Contains("internal const float Height = 34f;", StringComparison.Ordinal);
	}

	/// <summary>The opacity the Runtime derived lands on the launcher's group — one surface, both halves.</summary>
	private static bool TheOpacityLandsOnTheWholeSurface(string viewSource)
	{
		var flat = Flatten(viewSource);

		return flat.Contains("var group = root.AddComponent<CanvasGroup>();", StringComparison.Ordinal)
			&& flat.Contains("var clamped = Mathf.Clamp01(alpha);", StringComparison.Ordinal)
			&& flat.Contains("_group.alpha = clamped;", StringComparison.Ordinal);
	}

	/// <summary>The pointer is asked where it is, and a flip of the answer is what queues the hover fact.</summary>
	private static bool PollsThePointerAgainstTheRect(string viewSource)
	{
		var flat = Flatten(viewSource);

		return flat.Contains(
				"var hovered = RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, camera);",
				StringComparison.Ordinal)
			&& flat.Contains("if (hovered == _hovered) { return; }", StringComparison.Ordinal)
			&& flat.Contains("intents.Enqueue(new OnlineUiIntent(", StringComparison.Ordinal)
			&& flat.Contains(
				"hovered ? OnlineUiIntentKind.LauncherHoverEntered : OnlineUiIntentKind.LauncherHoverLeft",
				StringComparison.Ordinal);
	}

	/// <summary>The click's fact is queued before the sound plays (a click must never be lost to a side
	/// effect), and the sound is the game's own UI click.</summary>
	private static bool QueuesTheClickAndPlaysTheGamesSound(string hostSource)
	{
		var flat = Flatten(hostSource);
		var queued = flat.IndexOf("_intents.Enqueue(new OnlineUiIntent(OnlineUiIntentKind.LauncherToggled));", StringComparison.Ordinal);
		var sound = flat.IndexOf("PlayerCamera.PlayUISound(ClickSound);", StringComparison.Ordinal);

		return flat.Contains("internal const string ClickSound = \"miniClick\";", StringComparison.Ordinal)
			&& queued >= 0
			&& sound > queued;
	}

	/// <summary>
	/// The scene's own EventSystem wins. <c>EventSystem.current</c> reports the ENABLED one, so the fact
	/// this pins is "the scene lacks an enabled EventSystem", not "the scene has none at all" — and the
	/// game itself relies on one being enabled wherever its own UI works:
	/// <c>UIUtil.GetEventSystemRaycastResults</c> dereferences <c>EventSystem.current</c> without a guard
	/// and runs from the pointer-over-UI path every frame, so a scene where CUO's copy would be the only
	/// one is a scene where the game's own pointer handling is already broken.
	/// </summary>
	private static bool CreatesAnEventSystemOnlyWhenTheSceneLacksAnEnabledOne(string hostSource)
	{
		var flat = Flatten(hostSource);

		return flat.Contains("if (EventSystem.current != null) { return; }", StringComparison.Ordinal)
			&& CountOf(flat, "new GameObject(\"CUO Online UI Event System\"") == 1;
	}

	/// <summary>
	/// The IMGUI launcher's fingerprints: the theme's own launcher style (or a draw through the alpha
	/// overload the launcher used) and the launcher's GUI-space rect. COVERAGE LIMIT, recorded rather than
	/// implied: three fingerprints over the PLUGIN tree only, so a launcher regrown at another rect — or
	/// drawn through a different call that computes its rect — would not be seen here; what makes the
	/// ordinary "draw it again in the same place" regression fail is the rect literal. The adapter side
	/// needs no such scan: the surface IS the launcher there, and its own pins require it.
	/// </summary>
	private static bool DrawsALauncherInImgui(string source)
	{
		var code = StripComments(source);
		return code.Contains("OnlineUiTheme.Launcher(", StringComparison.Ordinal)
			|| code.Contains("OnlineUiTheme.DrawBackground(rect, alpha)", StringComparison.Ordinal)
			|| code.Contains("Screen.width - 170f", StringComparison.Ordinal);
	}

	/// <summary>Every <c>.cs</c> file of the plugin, comments cut — the scan surface of the "no second
	/// launcher" rule.</summary>
	private static IReadOnlyList<(string Path, string Source)> PluginSources() =>
	[
		.. Directory.GetFiles(PluginDirectory, "*.cs", SearchOption.AllDirectories)
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(path => (Path: path, Source: StripComments(ReadNormalised(path)))),
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

	/// <summary>Cut line and block comments, so a comment that NAMES a control is not read as a draw of
	/// it, while a path inside a string literal survives.</summary>
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

	/// <summary>Comments cut and whitespace collapsed, so indentation and line endings cannot decide a pin.</summary>
	private static string Flatten(string source) =>
		string.Join(" ", StripComments(source).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

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

	/// <summary>
	/// A source file read with its line endings normalised to <c>\n</c>: the working tree is checked out
	/// with CRLF (<c>core.autocrlf=true</c>) and <c>dotnet format</c> rewrites it that way, so a pin that
	/// spelled a multi-line anchor with <c>\n</c> would pass on a fresh file and fail on a formatted one.
	/// </summary>
	private static string ReadNormalised(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

	private static string AdapterSource(string fileName) =>
		ReadNormalised(Path.Combine(GameAdapterDirectory, "OnlineUi", fileName));

	private static string PluginSource(string fileName) => ReadNormalised(Path.Combine(PluginDirectory, fileName));

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
