using System;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The drag-use cue's WIRING: its decision existed and was tested since 2026-10-18, and the label was never
/// drawn because nothing called <c>DragUseCueOverlay.Draw</c> — batch `20261022-f`'s row 9 found the state
/// answering <c>showing: true</c> beside a frame with no label. A compiled overlay with no caller is green in
/// every behaviour test, so the wire is pinned here on the flattened, literal-stripped source:
/// <list type="number">
/// <item>the plugin's painted frame (<c>OnGUI</c>) calls the draw after the Online UI's own pass;</item>
/// <item>the draw method returns on the decision before it calls the overlay — a draw inside the guard would
/// paint exactly when the gesture says NOT to;</item>
/// <item>the decision is only trusted inside a live world with no CUO surface up, so a stale state (a scene
/// change, a session end, the modal that suspends the game's drag frame) cannot draw over a menu;</item>
/// <item>the overlay builds its rect from the cursor flipped into IMGUI's top-down space and clamps both axes.
/// </item>
/// </list>
/// </summary>
public class DragUseCueGateTests
{
	private const string PluginFile = "src/CasualtiesUnknownOnline.Plugin/Plugin.cs";

	private const string CueOverlayFile = "src/CasualtiesUnknownOnline.Plugin/DragUseCueOverlay.cs";

	private const string BridgeFile = "src/CasualtiesUnknownOnline.GameAdapter/GameAdapterBridge.cs";

	private const string SessionBindingFile = "src/CasualtiesUnknownOnline.GameAdapter/GameAdapterSessionBinding.cs";

	private const string TrackerFile = "src/CasualtiesUnknownOnline.GameAdapter/DragUseCueTracker.cs";

	private static readonly Regex StringLiteral = new("\"(?:\\\\.|[^\"\\\\])*\"");
	private static readonly Regex CharLiteral = new("'(?:\\\\.|[^'\\\\])*'");

	[Fact]
	public void TheCueDecision_ReachesTheScreen()
	{
		var plugin = RepositoryPaths.ReadText(PluginFile);
		var overlay = RepositoryPaths.ReadText(CueOverlayFile);

		Assert.True(
			WiringHolds(plugin, overlay),
			"the cue must be drawn from OnGUI, gated on the decision and on a live world, with the overlay's cursor flipped into IMGUI's space");
	}

	[Fact]
	public void TheWiringMatcher_SeesEveryShapeItRefuses()
	{
		Assert.True(WiringHolds(GoodPlugin, GoodOverlay));

		// OnGUI no longer calls the draw: the shipped defect.
		Assert.False(WiringHolds(GoodPlugin.Replace("DrawDragUseCue();", "", StringComparison.Ordinal), GoodOverlay));

		// The draw sits inside the guard body: it would paint exactly when the gesture says NOT to.
		Assert.False(WiringHolds(
			GoodPlugin.Replace(
				"        return;",
				"        DragUseCueOverlay.Draw(\"x\");\n        return;",
				StringComparison.Ordinal),
			GoodOverlay));

		// The only OnGUI call is spelled inside a log literal: not a call site. The draw method itself is
		// real, so this sample fails ONLY when the literal strip works.
		Assert.False(WiringHolds(
			GoodPlugin.Replace(
				"    DrawDragUseCue();",
				"    Debug.Log(\"DrawDragUseCue();\");",
				StringComparison.Ordinal),
			GoodOverlay));

		// The overlay keeps the raw bottom-left y for its rect.
		Assert.False(WiringHolds(
			GoodPlugin,
			GoodOverlay.Replace("Screen.height - Input.mousePosition.y", "Input.mousePosition.y", StringComparison.Ordinal)));
	}

	/// <summary>
	/// The stale-state half: the decision is cleared where no while-dragging frame can ever run again (the scene
	/// that destroys the camera, the session that ends the world), and the tracker asks the game's own release
	/// bind, so a release swallowed by a CUO modal cannot keep the label up.
	/// </summary>
	[Fact]
	public void TheCueIsClearedWhereTheDragCannotRun()
	{
		var bridge = CodeOnly(RepositoryPaths.ReadText(BridgeFile));
		var binding = CodeOnly(RepositoryPaths.ReadText(SessionBindingFile));
		var tracker = RepositoryPaths.ReadText(TrackerFile);

		Assert.Contains(
			"domains.DragUseCue.Clear();",
			SourceScan.MethodBody(bridge, "public void OnSceneLoadBegin()"),
			StringComparison.Ordinal);
		Assert.Contains(
			"domains.DragUseCue.Clear();",
			SourceScan.MethodBody(binding, "private void OnSessionEnded()"),
			StringComparison.Ordinal);
		Assert.Contains(
			"Input.GetKey(KeyBinds.GetBind(\"iteminteract\"))",
			SourceScan.MethodBody(tracker, "internal void Update(Item? dragItem)"),
			StringComparison.Ordinal);
	}

	/// <summary>
	/// The pin's own scan surface: comments and literals are removed, so a comment or a log string that spells
	/// the call can neither satisfy it nor hide a real one.
	/// </summary>
	[Fact]
	public void ThePinIgnoresCommentsAndLiterals()
	{
		var flattened = CodeOnly("// DragUseCueOverlay.Draw(name);\nvar s = \"DragUseCueOverlay.Draw(name)\";\nvoid M() { }");
		Assert.DoesNotContain("DragUseCueOverlay.Draw(", flattened, StringComparison.Ordinal);
	}

	private static bool WiringHolds(string pluginSource, string overlaySource)
	{
		var plugin = CodeOnly(pluginSource);
		var overlay = CodeOnly(overlaySource);

		var onGui = SourceScan.MethodBody(plugin, "private void OnGUI()");
		if (!Before(onGui, "_onlineUi.Draw();", "DrawDragUseCue();"))
		{
			return false;
		}

		var draw = SourceScan.MethodBody(plugin, "private void DrawDragUseCue()");
		var guard = draw.IndexOf("is not { Showing: true }", StringComparison.Ordinal);
		var earlyReturn = guard < 0 ? -1 : draw.IndexOf("return;", guard, StringComparison.Ordinal);
		var call = draw.IndexOf("DragUseCueOverlay.Draw(", StringComparison.Ordinal);
		if (guard < 0 || earlyReturn <= guard || call <= earlyReturn)
		{
			return false;
		}

		return draw.Contains("SessionActive: true, LocalInWorld: true", StringComparison.Ordinal)
			&& draw.Contains("IsModalSurfaceOpen", StringComparison.Ordinal)
			&& OverlayHolds(overlay);
	}

	private static bool OverlayHolds(string overlay)
	{
		var body = SourceScan.MethodBody(overlay, "internal static void Draw(string text)");
		var flip = body.IndexOf("Screen.height - Input.mousePosition.y", StringComparison.Ordinal);
		var rect = body.IndexOf("new Rect(", StringComparison.Ordinal);
		var clamps = Regex.Matches(body, @"Mathf\.Clamp\(").Count;
		return flip >= 0 && rect > flip && clamps >= 2;
	}

	private static bool Before(string text, string first, string second)
	{
		var firstIndex = text.IndexOf(first, StringComparison.Ordinal);
		var secondIndex = text.IndexOf(second, StringComparison.Ordinal);
		return firstIndex >= 0 && secondIndex > firstIndex;
	}

	/// <summary>Comments and string/char literals blanked, so a token has to be CODE to count.</summary>
	private static string CodeOnly(string source) =>
		CharLiteral.Replace(StringLiteral.Replace(SourceScan.Flatten(source), "\"\""), "''");

	private const string GoodPlugin = """
private void OnGUI()
{
    _onlineUi.Draw();
    DrawDragUseCue();
}

private void DrawDragUseCue()
{
    if (_dragUseCue is not { Showing: true } cue
        || _session is not { SessionActive: true, LocalInWorld: true }
        || _onlineUi.IsModalSurfaceOpen)
    {
        return;
    }

    DragUseCueOverlay.Draw(_localization.Format("cue.use_on", _onlineUi.DisplayName(cue.TargetSteamId)));
}
""";

	private const string GoodOverlay = """
internal static void Draw(string text)
{
    var size = style.CalcSize(new GUIContent(text));
    var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
    var x = Mathf.Clamp(mouse.x + CursorOffset, 0f, Mathf.Max(0f, Screen.width - size.x));
    var y = Mathf.Clamp(mouse.y - CursorOffset - size.y, 0f, Mathf.Max(0f, Screen.height - size.y));
    var rect = new Rect(x, y, size.x, size.y);
    GUI.Label(rect, text, style);
}
""";
}
