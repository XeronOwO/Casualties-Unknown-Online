using System;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The drag-use cue's WIRING and its SURFACE. The decision existed and was tested since 2026-10-18 while no label
/// was drawn at all (batch `20261022-f` row 9: `showing: true` beside a frame with no label), so the wire is
/// pinned here on the literal-stripped source:
/// <list type="number">
/// <item>the plugin's OnGUI pass calls the draw after the Online UI's own pass;</item>
/// <item>the draw returns on the decision before it calls the presenter — a call inside the guard would paint
/// exactly when the gesture says NOT to, and it hides the label on the way out;</item>
/// <item>the decision is only trusted inside a live world with no CUO surface up;</item>
/// <item>the surface is the GAME's: the presenter clones the game's own row label and material
/// (<c>ReadRowTemplate</c> + <c>CreateLabel</c>) and draws no IMGUI box — the user's 2026-11-07 ruling that a CUO
/// surface must look like the game's UI.</item>
/// </list>
/// </summary>
public class DragUseCueGateTests
{
	private const string PluginFile = "src/CasualtiesUnknownOnline.Plugin/PluginHost.cs";

	private const string PresenterFile = "src/CasualtiesUnknownOnline.GameAdapter/OnlineUi/DragUseCuePresenter.cs";

	private const string BridgeFile = "src/CasualtiesUnknownOnline.GameAdapter/GameAdapterBridge.cs";

	private const string SessionBindingFile = "src/CasualtiesUnknownOnline.GameAdapter/GameAdapterSessionBinding.cs";

	private const string TrackerFile = "src/CasualtiesUnknownOnline.GameAdapter/DragUseCueTracker.cs";

	private static readonly Regex StringLiteral = new("\"(?:\\\\.|[^\"\\\\])*\"");
	private static readonly Regex CharLiteral = new("'(?:\\\\.|[^'\\\\])*'");

	[Fact]
	public void TheCueDecision_ReachesTheGameStyledSurface()
	{
		var plugin = CodeOnly(RepositoryPaths.ReadText(PluginFile));
		var presenter = CodeOnly(RepositoryPaths.ReadText(PresenterFile));

		var onGui = SourceScan.MethodBody(plugin, "private void OnGUI()");
		Assert.True(
			Before(onGui, "_onlineUi.Draw();", "DrawDragUseCue();"),
			"the cue must be drawn from OnGUI, after the Online UI's own pass");

		var draw = SourceScan.MethodBody(plugin, "private void DrawDragUseCue()");
		var guard = draw.IndexOf("is not { Showing: true }", StringComparison.Ordinal);
		var earlyReturn = guard < 0 ? -1 : draw.IndexOf("return;", guard, StringComparison.Ordinal);
		var call = draw.IndexOf("_dragUseCuePresenter.Show(", StringComparison.Ordinal);
		Assert.True(guard >= 0 && earlyReturn > guard && call > earlyReturn, "the presenter call must sit after the guard");
		Assert.Contains("SessionActive: true, LocalInWorld: true", draw, StringComparison.Ordinal);
		Assert.Contains("IsModalSurfaceOpen", draw, StringComparison.Ordinal);
		Assert.Contains("_dragUseCuePresenter?.Hide();", draw, StringComparison.Ordinal);

		Assert.Contains("ReadRowTemplate", presenter, StringComparison.Ordinal);
		Assert.Contains("CreateLabel", presenter, StringComparison.Ordinal);
		Assert.DoesNotContain("GUI.", presenter, StringComparison.Ordinal);
		Assert.DoesNotContain("GUIStyle", presenter, StringComparison.Ordinal);
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
		var flattened = CodeOnly("// _dragUseCuePresenter.Show(name);\nvar s = \"_dragUseCuePresenter.Show(name)\";\nvoid M() { }");
		Assert.DoesNotContain("_dragUseCuePresenter.Show(", flattened, StringComparison.Ordinal);
	}

	/// <summary>Comments and string/char literals blanked, so a token has to be CODE to count.</summary>
	private static string CodeOnly(string source) =>
		CharLiteral.Replace(StringLiteral.Replace(SourceScan.Flatten(source), "\"\""), "''");

	private static bool Before(string text, string first, string second)
	{
		var firstIndex = text.IndexOf(first, StringComparison.Ordinal);
		var secondIndex = text.IndexOf(second, StringComparison.Ordinal);
		return firstIndex >= 0 && secondIndex > firstIndex;
	}
}
