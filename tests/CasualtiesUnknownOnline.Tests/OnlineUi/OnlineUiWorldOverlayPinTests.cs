using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The world-space overlays are labels of the game's own font on CUO's canvas (ticket
/// online-ui-art-and-controls-overhaul, S6): the nameplates, the off-screen arrows, the network readout and
/// the location pings. The two IMGUI surfaces that drew them — the overlay's own
/// <c>DrawNetworkHud</c>/<c>DrawNameplatesAndArrows</c> and the whole of <c>LocationPingOverlay</c> — are
/// gone with the migration, and the projection, the edge clamp and the label geometry are the surface's,
/// through the Runtime's own rules.
///
/// <para>
/// None of this is visible from a unit test: whether the markers land where a player expects, whether they
/// read as this game and whether the name is legible at the player's UI scale are game observations. What
/// is pinned here is the shape — the plugin builds models instead of drawing, one view places them, the
/// geometry and the glyph come from the Runtime, the whole rule is asked in the canvas's own units, the
/// overlay takes no input, and it draws behind CUO's own panels — each with a mutation of the REAL source
/// as its negative sample.
/// </para>
/// </summary>
public sealed class OnlineUiWorldOverlayPinTests
{
	[Fact]
	public void ThePluginBuildsTheWorldOverlaysInsteadOfDrawing() =>
		Assert.True(
			BuildsTheWorldOverlay(Plugin("OnlineUiOverlay.cs")),
			"the nameplates, the arrows, the readout and the pings must be models the surface renders: a world overlay drawn in IMGUI again would keep the skin's own font, which is the whole of what S6 removes");

	[Fact]
	public void TheWorldOverlayMatcher_FlagsTheRemovedDrawAndIgnoresAMention()
	{
		Assert.True(
			DrawsAWorldOverlayInImgui("GUI.Label(rect, name, style);\nvar box = NameplateLayout.AboveHead(x, y);"),
			"a nameplate drawn with GUI.Label through the Runtime's box is the eviction this pin exists for");
		Assert.True(
			DrawsAWorldOverlayInImgui("var p = OffScreenArrowGeometry.Place(x, y, Screen.width, Screen.height, 52f);\nGUI.Label(rect, \"\u25B2\", style);"),
			"an off-screen arrow drawn with the screen's own pixels is the same defect");
		Assert.False(
			DrawsAWorldOverlayInImgui("var marker = OnlineUiWorldMarker.Nameplate(1f, 2f, \"Ana\", \"3 m\", color);"),
			"a model that is handed to the surface is not a draw");
		Assert.False(
			DrawsAWorldOverlayInImgui("/// <summary>The nameplate is a label on the game's canvas now.</summary>\nvar width = NameplateLayout.Width;"),
			"a comment that merely mentions the nameplate is not a draw");
	}

	/// <summary>The tree scan, and the file that must not come back: the pings' IMGUI overlay. A marker
	/// regrown inside another plugin file is the same defect, which is why this asks every file rather than
	/// the one that used to hold it.</summary>
	[Fact]
	public void NoPluginFileDrawsAWorldOverlayInImgui()
	{
		var offenders = PluginSources()
			.Where(file => DrawsAWorldOverlayInImgui(file.Source))
			.Select(file => file.Path)
			.ToArray();

		Assert.True(
			offenders.Length == 0,
			$"these files draw a world-space overlay in IMGUI again: {string.Join(", ", offenders)}");

		Assert.False(
			File.Exists(Path.Combine(PluginDirectory, "LocationPingOverlay.cs")),
			"the IMGUI location-ping overlay retired with the markers it drew — the pings are labels of the game's own canvas now (S6)");
	}

	[Fact]
	public void TheSurfaceCarriesTheWorldOverlay() =>
		Assert.True(
			CarriesTheWorldOverlay(Adapter("OnlineUiSurfaceHost.cs")),
			"one view is built with the surface, applied on every pushed frame and dropped with the canvas: an overlay the frame does not reach is a world with no nameplates");

	[Fact]
	public void TheOverlaysLabelsComeFromTheGamesOwnFont() =>
		Assert.True(
			LabelsComeFromTheGame(Adapter("OnlineUiWorldOverlayView.cs")),
			"every label on the overlay — the names, the pings, the readout — must take the game's own font asset and its size ladder, which is the art ask S6 exists for");

	[Fact]
	public void ThePlacementIsTheRuntimesRuleInTheCanvasOwnUnits() =>
		Assert.True(
			PlacedByTheRuntimeRulesInCanvasUnits(Adapter("OnlineUiWorldOverlayView.cs")),
			"the placement must be the Runtime's rule (the edge clamp, the box above a head, the arrow's mark) asked in the canvas's OWN units: the surface is scaled by the game's UI scale, so a rule asked in screen pixels would only hold at a canvas scale of 1 (the S5 review's F1)");

	[Fact]
	public void TheOverlayTakesNoInput() =>
		Assert.True(
			TakesNoInput(Adapter("OnlineUiWorldOverlayView.cs")),
			"a marker is a label over the world and must never answer the pointer: the layer carries no graphic, every label refuses the raycast, the view builds no interactive component and reports nothing back — the mutation rows add a Button to the layer and a pointer poll to the pass, which the text-absence check this pin used to be would have missed");

	/// <summary>
	/// The other half of "no input": the two world gestures still ask the ONE pointer census, and the world
	/// overlay is not on that path. The census's own behaviour is <c>OnlineUiInputBlockingPinTests</c>'
	/// contract — this pin holds that S6 did not reroute the callers.
	/// </summary>
	[Fact]
	public void TheWorldInputPathsStillAskTheCensus() =>
		Assert.True(
			KeepsTheWorldInputPathsOnTheCensus(Plugin("OnlineUiOverlay.cs")),
			"the middle-click ping and the in-world right-click must still ask the pointer census through the same two members — a migrated overlay that quietly joined the input path would block a world click somewhere the player cannot see");

	/// <summary>
	/// The labels' fit and their marks: a fixed box, one line, no auto-sizing, and an ASCII stand-in for a
	/// mark the game's font cannot draw. The sizes come from the game's ladder and are a deliberate change
	/// from the old pixel constants — recorded in the stage's limits, so what is held here is the boxes the
	/// boxes must keep and the policy that keeps a name readable.
	/// </summary>
	[Fact]
	public void TheLabelsKeepTheirBoxesAndFallBackForAMissingMark() =>
		Assert.True(
			LabelsKeepTheirBoxes(Adapter("OnlineUiWorldOverlayView.cs")),
			"the label boxes are the IMGUI rects in canvas units (180x24 above a head, 160x20 under an arrow, 140x20 under a ping at 22 below it, 220x20 for the readout), the fit policy is stated as one line with overflow rather than a clip, and every mark goes through the font check that falls back to ASCII");

	[Fact]
	public void TheOverlayDrawsBehindCuosOwnPanels() =>
		Assert.True(
			DrawsBehindCuosOwnPanels(Adapter("OnlineUiWorldOverlayView.cs")),
			"the layer must put itself first among the canvas's children: a nameplate over the window the player has open is the wrong way round");

	/// <summary>The two states the IMGUI pass returned before drawing the world overlays in: the start gate
	/// owns the screen while it holds the player (its overlay covers the HUD), and the command console is a
	/// modal surface of its own. Pushing the models from Update instead must not lose either rule — the
	/// console half was a real regression in the first cut of this stage.</summary>
	[Fact]
	public void TheWorldOverlayHidesWhileTheGateOrTheConsoleOwnsTheScreen() =>
		Assert.True(
			HidesTheWorldWhileTheScreenIsOwned(Plugin("OnlineUiHost.cs")),
			"the frame must push an empty overlay while the start gate or the command console owns the screen — the IMGUI pass skipped the world overlays in both states, and a model that outlives that rule draws nameplates over a surface that is supposed to own the screen");

	[Fact]
	public void ThePingKeepsItsOwnMarkAndFade() =>
		Assert.True(
			KeepsThePingsOwnMarkAndFade(Plugin("OnlineUiOverlay.cs")),
			"a ping keeps the mark it always drew (`!` or `●`, spelled in the plugin because the mark is a translation-side choice) and fades out over its last second by folding the alpha into the colour the marker carries (the removed IMGUI overlay's own rule): a migration that dropped either would change what the player sees");

	/// <summary>
	/// The Runtime's own half of the marks: the four arrows and the dot are the glyphs the two removed
	/// switches spelled, held as codepoints because a behavioural test that compares each constant with the
	/// mapping it feeds would follow a typo instead of catching it.
	/// </summary>
	[Fact]
	public void TheRuntimeOwnsTheArrowMarks() =>
		Assert.True(
			HoldsTheArrowMarks(Read("runtime/OffScreenArrowText.cs")),
			"the arrows must keep their codepoints and their direction mapping — they are the marks the IMGUI overlay drew, and the game's own font is asked for them at render time");

	public static TheoryData<string, string, string, string, string> Mutations => new()
	{
		// The plugin's half: a model, not a draw.
		{ nameof(ThePluginBuildsTheWorldOverlaysInsteadOfDrawing), "plugin/OnlineUiOverlay.cs", "_worldMarkers.Add(OnlineUiWorldMarker.Nameplate(", "GUI.Label(new Rect(0f, 0f, 1f, 1f), \"Ana\");\n\t\t\t_worldMarkers.Add(OnlineUiWorldMarker.Nameplate(", "a nameplate drawn in IMGUI again" },
		{ nameof(ThePluginBuildsTheWorldOverlaysInsteadOfDrawing), "plugin/OnlineUiOverlay.cs", "return new OnlineUiWorldOverlay(BuildNetworkHud(ctx), _worldMarkers);", "return OnlineUiWorldOverlay.None;", "a world overlay the frame always pushes empty" },
		// The surface's half: built with the canvas, applied on every frame, dropped with it.
		{ nameof(TheSurfaceCarriesTheWorldOverlay), "adapter/OnlineUiSurfaceHost.cs", "_worldOverlay = OnlineUiWorldOverlayView.Create(root.transform, _log);", "_worldOverlay = null;", "a world overlay that is never built" },
		{ nameof(TheSurfaceCarriesTheWorldOverlay), "adapter/OnlineUiSurfaceHost.cs", "_worldOverlay?.Apply(frame.World);", "// the frame's world overlay is dropped", "a frame whose world overlay never reaches the surface" },
		{ nameof(TheSurfaceCarriesTheWorldOverlay), "adapter/OnlineUiSurfaceHost.cs", "_worldOverlay = null;", "// the world overlay outlives the surface it was built on", "a world overlay leaked past the surface that owns it" },
		// The art half: the game's typography, the boxes, and the fit and fallback policy.
		{ nameof(TheOverlaysLabelsComeFromTheGamesOwnFont), "adapter/OnlineUiWorldOverlayView.cs", "OnlineUiControlFactory.ReadRowTemplate(root.transform, out var typography, out _, out _, out _);", "var typography = new OnlineUiControlView.Typography(null, 12f);", "a typography the game never handed over" },
		{ nameof(TheOverlaysLabelsComeFromTheGamesOwnFont), "adapter/OnlineUiWorldOverlayView.cs", "item.Label(marker.OffScreenText, color, _typography.SizeOf(OnlineUiTextStyle.Muted));", "item.Label(marker.OffScreenText, color, 12f);", "a label size that is a constant instead of the game's own ladder" },
		{ nameof(TheLabelsKeepTheirBoxesAndFallBackForAMissingMark), "adapter/OnlineUiWorldOverlayView.cs", "text.overflowMode = TextOverflowModes.Overflow;", "text.overflowMode = TextOverflowModes.Ellipsis;", "a name the player cannot finish reading" },
		{ nameof(TheLabelsKeepTheirBoxesAndFallBackForAMissingMark), "adapter/OnlineUiWorldOverlayView.cs", "internal const float PingLabelOffsetY = 22f;", "internal const float PingLabelOffsetY = 0f;", "a ping label that sat on its own mark" },
		{ nameof(TheLabelsKeepTheirBoxesAndFallBackForAMissingMark), "adapter/OnlineUiWorldOverlayView.cs", "OffScreenArrowText.Up => \"^\",", "OffScreenArrowText.Up => \"\\u25B2\",", "a fallback that falls back to the glyph the font cannot draw" },
		// The geometry half: the Runtime's rules, asked in the canvas's own units.
		{ nameof(ThePlacementIsTheRuntimesRuleInTheCanvasOwnUnits), "adapter/OnlineUiWorldOverlayView.cs", "var placement = OffScreenArrowGeometry.Place(point.x, point.y, bounds.width, bounds.height, ScreenEdgeMargin);", "var placement = OffScreenArrowGeometry.Place(point.x, point.y, Screen.width, Screen.height, ScreenEdgeMargin);", "a rule asked in screen pixels beside canvas units (the S5 review's F1)" },
		{ nameof(ThePlacementIsTheRuntimesRuleInTheCanvasOwnUnits), "adapter/OnlineUiWorldOverlayView.cs", "if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, new Vector2(screen.x, screen.y), cameraOfCanvas, out var local))", "var local = new Vector2(screen.x, screen.y); var answered = true; if (!answered)", "a projected point that is never converted out of screen pixels" },
		{ nameof(ThePlacementIsTheRuntimesRuleInTheCanvasOwnUnits), "adapter/OnlineUiWorldOverlayView.cs", "var box = NameplateLayout.AboveHead(placement.X, placement.Y);", "var box = new NameplateRect(placement.X, placement.Y, 180f, 24f);", "a nameplate placed by a hand-rolled box" },
		{ nameof(ThePlacementIsTheRuntimesRuleInTheCanvasOwnUnits), "adapter/OnlineUiWorldOverlayView.cs", "item.Glyph(Drawable(OffScreenArrowText.Glyph(placement.Direction)), color, ArrowGlyphFontSize);", "item.Glyph(\"\\u25B2\", color, ArrowGlyphFontSize);", "an arrow that stopped asking the Runtime's own rule" },
		{ nameof(ThePlacementIsTheRuntimesRuleInTheCanvasOwnUnits), "adapter/OnlineUiWorldOverlayView.cs", "internal const float ScreenEdgeMargin = 52f;", "internal const float ScreenEdgeMargin = 0f;", "a marker flush against the screen's edge" },
		// The input half: nothing to hit, nothing reported, and the two world gestures still on the census.
		{ nameof(TheOverlayTakesNoInput), "adapter/OnlineUiWorldOverlayView.cs", "text.raycastTarget = false;", "text.raycastTarget = true;", "a marker that swallows a click meant for the world" },
		{ nameof(TheOverlayTakesNoInput), "adapter/OnlineUiWorldOverlayView.cs", "var root = new GameObject(LayerName, typeof(RectTransform));", "var root = new GameObject(LayerName, typeof(RectTransform), typeof(UnityEngine.UI.Button));", "an overlay layer that carries a control to click" },
		{ nameof(TheOverlayTakesNoInput), "adapter/OnlineUiWorldOverlayView.cs", "var camera = Camera.main;", "var pointer = Input.mousePosition; var camera = Camera.main;", "a marker pass that polls the pointer" },
		{ nameof(TheWorldInputPathsStillAskTheCensus), "plugin/OnlineUiOverlay.cs", "return _pointerCensus.BlocksWorldPing();", "return false;", "a middle-click ping that stops asking the census" },
		// The order, and the two states that hide the overlay.
		{ nameof(TheOverlayDrawsBehindCuosOwnPanels), "adapter/OnlineUiWorldOverlayView.cs", "root.transform.SetAsFirstSibling();", "root.transform.SetAsLastSibling();", "an overlay drawn over CUO's own panels" },
		{ nameof(TheWorldOverlayHidesWhileTheGateOrTheConsoleOwnsTheScreen), "plugin/OnlineUiHost.cs", "_gateState is { IsWaitingForReady: true }", "false", "a world overlay that shows through the start gate" },
		{ nameof(TheWorldOverlayHidesWhileTheGateOrTheConsoleOwnsTheScreen), "plugin/OnlineUiHost.cs", "|| _onlineUi.IsCommandConsoleOpen", "", "a world overlay that draws nameplates over the open command console" },
		// The ping's own mark and fade.
		{ nameof(ThePingKeepsItsOwnMarkAndFade), "plugin/OnlineUiOverlay.cs", "color.A * Mathf.Clamp01((float)remaining / PingFadeMs)", "color.A", "a ping that stops fading" },
		{ nameof(ThePingKeepsItsOwnMarkAndFade), "plugin/OnlineUiOverlay.cs", "private const float PingFadeMs = 1_000f;", "private const float PingFadeMs = 0f;", "a fade window that is not the ping's own" },
		{ nameof(ThePingKeepsItsOwnMarkAndFade), "plugin/OnlineUiOverlay.cs", "ping.Kind == LocationPingKind.Exclamation ? \"!\" : \"●\"", "ping.Kind == LocationPingKind.Exclamation ? \"!\" : \"o\"", "a ping that stopped drawing its own mark" },
		// The Runtime's own marks.
		{ nameof(TheRuntimeOwnsTheArrowMarks), "runtime/OffScreenArrowText.cs", "public const string Up = \"\\u25B2\";", "public const string Up = \"\\u25BC\";", "two directions that draw the same arrow" },
	};

	/// <summary>
	/// Every pin of this class, against a broken source: the anchor must be in the real file (so a text
	/// drift cannot turn the sample into a tautology), the replacement must change it, and the pin's own
	/// matcher must then be false. Every pin here reads ONE file, so the mutated source is the one the
	/// matcher is handed.
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
		nameof(ThePluginBuildsTheWorldOverlaysInsteadOfDrawing) => BuildsTheWorldOverlay,
		nameof(TheSurfaceCarriesTheWorldOverlay) => CarriesTheWorldOverlay,
		nameof(TheOverlaysLabelsComeFromTheGamesOwnFont) => LabelsComeFromTheGame,
		nameof(TheLabelsKeepTheirBoxesAndFallBackForAMissingMark) => LabelsKeepTheirBoxes,
		nameof(ThePlacementIsTheRuntimesRuleInTheCanvasOwnUnits) => PlacedByTheRuntimeRulesInCanvasUnits,
		nameof(TheOverlayTakesNoInput) => TakesNoInput,
		nameof(TheWorldInputPathsStillAskTheCensus) => KeepsTheWorldInputPathsOnTheCensus,
		nameof(TheOverlayDrawsBehindCuosOwnPanels) => DrawsBehindCuosOwnPanels,
		nameof(TheWorldOverlayHidesWhileTheGateOrTheConsoleOwnsTheScreen) => HidesTheWorldWhileTheScreenIsOwned,
		nameof(ThePingKeepsItsOwnMarkAndFade) => KeepsThePingsOwnMarkAndFade,
		nameof(TheRuntimeOwnsTheArrowMarks) => HoldsTheArrowMarks,
		_ => throw new InvalidOperationException($"no matcher is registered for the pin `{pin}`"),
	};

	/// <summary>The plugin's half: the world overlay is built as a model, and nothing in that file draws.
	/// The draw fingerprints are the IMGUI calls themselves, so a nameplate regrown in IMGUI fails here
	/// wherever in the file it is drawn from.</summary>
	private static bool BuildsTheWorldOverlay(string overlaySource)
	{
		var flat = Flatten(overlaySource);

		return !DrawsImgui(flat)
			&& flat.Contains("internal OnlineUiWorldOverlay BuildWorldOverlay(OnlineUiContext ctx)", StringComparison.Ordinal)
			&& flat.Contains("_worldMarkers.Add(OnlineUiWorldMarker.Nameplate(", StringComparison.Ordinal)
			&& flat.Contains("_worldMarkers.Add(OnlineUiWorldMarker.Ping(", StringComparison.Ordinal)
			&& flat.Contains("return new OnlineUiWorldOverlay(BuildNetworkHud(ctx), _worldMarkers);", StringComparison.Ordinal);
	}

	/// <summary>The surface's half: one view, built with the canvas, applied from the frame push, and
	/// dropped inside the surface's own teardown — the last of the three is asked of
	/// <c>DestroySurface</c>'s body, because the assignment alone would be satisfied by any stray local
	/// (the review's N18).</summary>
	private static bool CarriesTheWorldOverlay(string surfaceSource) =>
		Flatten(surfaceSource).Contains("_worldOverlay = OnlineUiWorldOverlayView.Create(root.transform, _log);", StringComparison.Ordinal)
			&& Flatten(ExtractMember(surfaceSource, "internal void Push(OnlineUiFrame frame)")).Contains("_worldOverlay?.Apply(frame.World);", StringComparison.Ordinal)
			&& Flatten(ExtractMember(surfaceSource, "private void DestroySurface()")).Contains("_worldOverlay = null;", StringComparison.Ordinal);

	/// <summary>The art half: the game's own row template hands over the font asset and the type ladder,
	/// and every label on the overlay is built from them — no label of this view sets a font or a size of
	/// its own.</summary>
	private static bool LabelsComeFromTheGame(string viewSource)
	{
		var flat = Flatten(viewSource);

		return flat.Contains("OnlineUiControlFactory.ReadRowTemplate(root.transform, out var typography, out _, out _, out _);", StringComparison.Ordinal)
			&& flat.Contains("text.font = typography.Font;", StringComparison.Ordinal)
			&& flat.Contains("label.fontSize = typography.SizeOf(OnlineUiTextStyle.Muted);", StringComparison.Ordinal)
			&& flat.Contains("item.Label(marker.OnScreenText, color, _typography.SizeOf(OnlineUiTextStyle.Default));", StringComparison.Ordinal)
			&& flat.Contains("item.Label(marker.OffScreenText, color, _typography.SizeOf(OnlineUiTextStyle.Muted));", StringComparison.Ordinal);
	}

	/// <summary>
	/// The geometry half: the projected world point is converted into the canvas ONCE, and the Runtime's
	/// rules are then asked with the canvas's own rect, margin and size. The screen's own pixel dimensions
	/// must not appear in the view at all — the surface is scaled by the game's UI scale.
	/// </summary>
	private static bool PlacedByTheRuntimeRulesInCanvasUnits(string viewSource)
	{
		var flat = Flatten(viewSource);

		return flat.Contains("internal const float ScreenEdgeMargin = 52f;", StringComparison.Ordinal)
			&& flat.Contains(
				"if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, new Vector2(screen.x, screen.y), cameraOfCanvas, out var local))",
				StringComparison.Ordinal)
			&& flat.Contains(
				"var placement = OffScreenArrowGeometry.Place(point.x, point.y, bounds.width, bounds.height, ScreenEdgeMargin);",
				StringComparison.Ordinal)
			&& flat.Contains("var box = NameplateLayout.AboveHead(placement.X, placement.Y);", StringComparison.Ordinal)
			&& flat.Contains("item.Glyph(Drawable(OffScreenArrowText.Glyph(placement.Direction)), color, ArrowGlyphFontSize);", StringComparison.Ordinal)
			&& !flat.Contains("Screen.width", StringComparison.Ordinal)
			&& !flat.Contains("Screen.height", StringComparison.Ordinal);
	}

	/// <summary>
	/// The fit and the marks (the review's M2 and M4): the boxes are the IMGUI rects in canvas units, the
	/// fit policy is stated (one line, no auto-sizing, overflow rather than a clip or an ellipsis), and both
	/// glyph call sites go through the font check that falls back to ASCII. The two glyph FONT sizes stay
	/// this view's own constants, because a symbol is not text.
	/// </summary>
	private static bool LabelsKeepTheirBoxes(string viewSource)
	{
		var flat = Flatten(viewSource);

		return flat.Contains("internal const float ArrowLabelWidth = 160f;", StringComparison.Ordinal)
			&& flat.Contains("internal const float ArrowLabelHeight = 20f;", StringComparison.Ordinal)
			&& flat.Contains("internal const float PingLabelWidth = 140f;", StringComparison.Ordinal)
			&& flat.Contains("internal const float PingLabelHeight = 20f;", StringComparison.Ordinal)
			&& flat.Contains("internal const float PingLabelOffsetY = 22f;", StringComparison.Ordinal)
			&& flat.Contains("internal const float HudWidth = 220f;", StringComparison.Ordinal)
			&& flat.Contains("internal const float HudRowHeight = 20f;", StringComparison.Ordinal)
			&& flat.Contains("text.enableWordWrapping = false;", StringComparison.Ordinal)
			&& flat.Contains("text.enableAutoSizing = false;", StringComparison.Ordinal)
			&& flat.Contains("text.overflowMode = TextOverflowModes.Overflow;", StringComparison.Ordinal)
			&& flat.Contains("item.Glyph(Drawable(marker.Glyph), color, PingGlyphFontSize);", StringComparison.Ordinal)
			&& flat.Contains("item.Glyph(Drawable(OffScreenArrowText.Glyph(placement.Direction)), color, ArrowGlyphFontSize);", StringComparison.Ordinal)
			&& flat.Contains("if (glyph.Length == 0 || _typography.Font is not { } font || font.HasCharacter(glyph[0], searchFallbacks: true))", StringComparison.Ordinal)
			&& flat.Contains("OffScreenArrowText.Up => \"^\",", StringComparison.Ordinal)
			&& flat.Contains("_ => \"*\",", StringComparison.Ordinal);
	}

	/// <summary>The input half: no graphic on the layer, a refused raycast on every label, no interactive
	/// component the view could have grown, no pointer read, and nothing reported back to the plugin. The
	/// absence checks are named after the components that would answer a click, so a `Button` or an
	/// `EventTrigger` added later is a failure here (the review's M3a).</summary>
	private static bool TakesNoInput(string viewSource)
	{
		var flat = Flatten(viewSource);

		return flat.Contains("var root = new GameObject(LayerName, typeof(RectTransform));", StringComparison.Ordinal)
			&& flat.Contains("text.raycastTarget = false;", StringComparison.Ordinal)
			&& !flat.Contains("OnlineUiIntent", StringComparison.Ordinal)
			&& !flat.Contains("Image", StringComparison.Ordinal)
			&& !flat.Contains("Graphic", StringComparison.Ordinal)
			&& !flat.Contains("Selectable", StringComparison.Ordinal)
			&& !flat.Contains("Button", StringComparison.Ordinal)
			&& !flat.Contains("Toggle", StringComparison.Ordinal)
			&& !flat.Contains("EventTrigger", StringComparison.Ordinal)
			&& !flat.Contains("IPointer", StringComparison.Ordinal)
			&& !flat.Contains("Input.mousePosition", StringComparison.Ordinal)
			&& !flat.Contains("OnlineUiPointerCensus", StringComparison.Ordinal);
	}

	/// <summary>The other half of the same contract (the review's M3b): the world overlay is not on the two
	/// world gestures' path, and both still ask the ONE census through the members they always did.</summary>
	private static bool KeepsTheWorldInputPathsOnTheCensus(string overlaySource)
	{
		var flat = Flatten(overlaySource);

		return flat.Contains("internal bool IsPointerOverUi() { RefreshPointerCensus(); return _pointerCensus.BlocksWorldPing(); }", StringComparison.Ordinal)
			&& flat.Contains("_contextMenu.HandleInput(ctx, _pointerCensus.OverContextMenu, BlocksWorldMenu);", StringComparison.Ordinal);
	}

	private static bool DrawsBehindCuosOwnPanels(string viewSource) =>
		Flatten(viewSource).Contains("root.transform.SetAsFirstSibling();", StringComparison.Ordinal);

	/// <summary>The two states, which the IMGUI pass used to answer by returning early: the start gate, and
	/// the command console. This pin owns the CONDITION; <c>OnlineUiSurfacePinTests</c> owns the shape of the
	/// push it guards, so a change to one does not have to be made twice (the review's N20).</summary>
	private static bool HidesTheWorldWhileTheScreenIsOwned(string hostSource)
	{
		var flat = Flatten(hostSource);

		return flat.Contains("_gateState is { IsWaitingForReady: true } || _onlineUi.IsCommandConsoleOpen", StringComparison.Ordinal)
			&& flat.Contains("? OnlineUiWorldOverlay.None : _onlineUi.BuildWorldOverlay(ctx);", StringComparison.Ordinal);
	}

	/// <summary>The ping's own mark and its fade, both of which the removed IMGUI overlay carried.</summary>
	private static bool KeepsThePingsOwnMarkAndFade(string overlaySource)
	{
		var flat = Flatten(overlaySource);

		return flat.Contains("private const float PingFadeMs = 1_000f;", StringComparison.Ordinal)
			&& flat.Contains("color.A * Mathf.Clamp01((float)remaining / PingFadeMs)", StringComparison.Ordinal)
			&& flat.Contains("ping.Kind == LocationPingKind.Exclamation ? \"!\" : \"●\"", StringComparison.Ordinal);
	}

	/// <summary>
	/// One member's declaration line and its body, up to the next member (a line that starts at one tab with
	/// a declaration or with its doc comment). Copied from the launcher-fade pin's own helper: a pin that
	/// asks WHERE a statement lives needs the body, not the file.
	/// </summary>
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

	/// <summary>The Runtime's marks: five codepoints and the direction they answer for.</summary>
	private static bool HoldsTheArrowMarks(string arrowSource)
	{
		var flat = Flatten(arrowSource);

		return flat.Contains("public const string Up = \"\\u25B2\";", StringComparison.Ordinal)
			&& flat.Contains("public const string Down = \"\\u25BC\";", StringComparison.Ordinal)
			&& flat.Contains("public const string Left = \"\\u25C0\";", StringComparison.Ordinal)
			&& flat.Contains("public const string Right = \"\\u25B6\";", StringComparison.Ordinal)
			&& flat.Contains("public const string OnScreen = \"\\u2022\";", StringComparison.Ordinal)
			&& flat.Contains("OffScreenArrowDirection.Up => Up,", StringComparison.Ordinal)
			&& flat.Contains("OffScreenArrowDirection.Down => Down,", StringComparison.Ordinal)
			&& flat.Contains("OffScreenArrowDirection.Left => Left,", StringComparison.Ordinal)
			&& flat.Contains("OffScreenArrowDirection.Right => Right,", StringComparison.Ordinal)
			&& flat.Contains("_ => OnScreen,", StringComparison.Ordinal);
	}

	/// <summary>The IMGUI fingerprints: a layout call, a fixed-rect draw, or a theme style — beside anything
	/// that names one of the world overlays.</summary>
	private static bool DrawsAWorldOverlayInImgui(string source)
	{
		var code = Flatten(source);
		var draws = code.Contains("GUI.Label(", StringComparison.Ordinal)
			|| code.Contains("GUILayout.Label(", StringComparison.Ordinal)
			|| code.Contains("GUIStyle", StringComparison.Ordinal);
		var overlay = code.Contains("NameplateLayout", StringComparison.Ordinal)
			|| code.Contains("OffScreenArrowGeometry", StringComparison.Ordinal)
			|| code.Contains("hud.rtt", StringComparison.Ordinal)
			|| code.Contains("hud.distance", StringComparison.Ordinal)
			|| code.Contains("LocationPingOverlay", StringComparison.Ordinal);

		return draws && overlay;
	}

	/// <summary>The three IMGUI calls a drawing surface makes, named after what it does rather than after
	/// one spelling of it.</summary>
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

	private static IReadOnlyList<(string Path, string Source)> PluginSources() =>
	[
		.. Directory.GetFiles(PluginDirectory, "*.cs", SearchOption.AllDirectories)
			.Select(path => (Path: path, Source: ReadNormalised(path))),
	];

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
