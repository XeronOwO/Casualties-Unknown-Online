using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The Online UI window family on the game's own surface (ticket online-ui-art-and-controls-overhaul,
/// S2b). S2a put the launcher on the game's own control and proved the mechanism; this pin set is what
/// holds the window that rode on it: the shell, the tab row and the six pages' controls are the game's
/// own row prefabs, the plugin builds a display list and dispatches what comes back, and the pixels are
/// put where the Runtime's own rules say.
///
/// <para>
/// Like S2a's set, every pin carries a mutation of the REAL source as its negative sample — the rows of
/// <see cref="Mutations"/> — so none of them can pass vacuously, and every anchor is matched after
/// comment-stripping and whitespace flattening, so indentation, line endings and a comment that merely
/// names an API cannot decide a result.
/// </para>
///
/// <para>
/// What these pins cannot see: a rendering. Whether the game's row prefab behaves when instantiated
/// ACTIVE, whether the layout groups produce the shape the IMGUI window had, and whether the result
/// looks like this game are the user's run.
/// </para>
/// </summary>
public sealed class OnlineUiWindowSurfacePinTests
{
	[Fact]
	public void TheWindowsControlsAreTheGamesOwnRowPrefabs() =>
		Assert.True(
			UsesTheGamesRowPrefabs(Adapter("OnlineUiControlFactory.cs"), Adapter("OnlineUiWindowView.cs")),
			"every control of the window must be the game's own row prefab, and the frame's art must be read from it — a hand-built look is the gap this overhaul exists to close");

	[Fact]
	public void TheWindowIsShownOnlyWhileTheFrameCarriesAModel() =>
		Assert.True(
			TheFrameDrivesVisibility(Adapter("OnlineUiSurfaceHost.cs")),
			"a null window in the frame means the window is closed: the surface must show and hide it from the frame, and keep polling its rect either way");

	[Fact]
	public void TheWindowHangsOnTheSameCanvasAsTheLauncher() =>
		Assert.True(
			CreatedUnderCuosCanvas(Adapter("OnlineUiSurfaceHost.cs")),
			"the window is created under CUO's own canvas root, next to the launcher — the canvas that is parented under the game's own");

	[Fact]
	public void TheRowsAreWrappedByTheRuntimesOwnRule() =>
		Assert.True(
			WrappedByTheRuntimeRule(Adapter("OnlineUiWindowView.cs")),
			"whether a row fits is the Runtime's rule (OnlineUiRowLayout), not a number invented in the adapter: the adapter consumes the answer");

	[Fact]
	public void AControlIsWrittenOnlyWhereItChanged() =>
		Assert.True(
			WritesOnlyChanges(Adapter("OnlineUiControlView.cs")),
			"a steady window must not dirty the canvas every frame: text, selected state and layout are each written only when they differ");

	[Fact]
	public void ATextFieldIsNotOverwrittenWhileThePlayerTypes() =>
		Assert.True(
			LeavesAFocusedFieldAlone(Adapter("OnlineUiControlView.cs")),
			"the model carries the value the plugin holds; writing it into a focused field would fight the caret and lose keystrokes");

	[Fact]
	public void EveryInteractiveControlReportsItsOwnId() =>
		Assert.True(
			ReportsTheCurrentId(Adapter("OnlineUiControlView.cs")),
			"a control reports the id it holds NOW, not the one it was built with: a row is reused when a page or a roster changes, and a stale id would fire another member's action — and a model that gives a control no id (the colour picker's preview block) must report nothing at all");

	[Fact]
	public void ThePointerFactIsPolledFromTheWindowsRect() =>
		Assert.True(
			PollsTheWindowsRect(Adapter("OnlineUiWindowView.cs")),
			"the window's own rect is the surface's fact, and it is polled: an in-world right-click inside the window belongs to the UI, and uGUI's callbacks fire on movement only");

	[Fact]
	public void TheWindowStopsTheWorldBehindItFromBeingClicked() =>
		Assert.True(
			TheFrameSwallowsThePointer(Adapter("OnlineUiWindowView.cs")),
			"the frame's image is a raycast target: a click inside the window must land on CUO's surface and never fall through to the world");

	[Fact]
	public void TheTitleBarDragsTheWindow() =>
		Assert.True(
			TheTitleBarDrags(Adapter("OnlineUiWindowView.cs"), Adapter("OnlineUiWindowDragHandler.cs")),
			"the IMGUI window was draggable and the uGUI one must be too, or a player cannot move the window off the HUD");

	[Fact]
	public void ThePluginDispatchesEveryIntentKind() =>
		Assert.True(
			DispatchesEveryIntentKind(Plugin("OnlineUiHost.cs")),
			"every intent kind the surface can report must reach the action table — an unhandled kind is a control that silently does nothing");

	/// <summary>A control with no width hint must still take a size: the game's row prefab is placed by
	/// hand in its own screen (sizeDelta), and a layout group reads nothing off a rect, so the view seeds
	/// that size into the LayoutElement or the row lays out at zero height.</summary>
	[Fact]
	public void AControlTakesTheGamesRowSizeWhenTheModelGivesNone() =>
		Assert.True(
			SeedsThePrefabsOwnSize(Adapter("OnlineUiControlFactory.cs")),
			"the prefab's own size must be seeded into the layout: a row whose prefab carries no LayoutElement value would otherwise be zero-sized");

	[Fact]
	public void AnIntentForAControlTheWindowNoLongerOffersIsDropped() =>
		Assert.True(
			DropsIntentsForGoneControls(Plugin("OnlineUiWindow.cs"), Plugin("OnlineUiHost.cs")),
			"an intent whose id is no longer in the frame's model is dropped and logged: the control the player clicked is gone, and the click must not be applied to whatever took its place");

	[Fact]
	public void TheCloseControlIsTheShellsAndItsMeaningIsThePlugins() =>
		Assert.True(
			ClosesThroughTheSharedId(Plugin("OnlineUiWindow.cs"), Adapter("OnlineUiWindowView.cs")),
			"the close button is chrome (the surface builds it) and its meaning is an action (the plugin registers it) — both halves must address the same id");

	public static TheoryData<string, string, string, string, string> Mutations => new()
	{
		{ nameof(TheWindowsControlsAreTheGamesOwnRowPrefabs), "adapter/OnlineUiControlFactory.cs", "\"Special/GameSettingBool\"", "\"Special/GameSettingNonexistent\"", "a hand-built checkbox instead of the game's row" },
		{ nameof(TheWindowsControlsAreTheGamesOwnRowPrefabs), "adapter/OnlineUiWindowView.cs", "ReadGameRowTemplate(root.transform, out var typography", "ReadNoTemplate(out var typography", "a frame whose art and typography are guessed" },
		{ nameof(TheWindowIsShownOnlyWhileTheFrameCarriesAModel), "adapter/OnlineUiSurfaceHost.cs", "_window.SetVisible(frame.Window is not null);", "_window.SetVisible(true);", "a window that never closes" },
		{ nameof(TheWindowIsShownOnlyWhileTheFrameCarriesAModel), "adapter/OnlineUiSurfaceHost.cs", "_window.PollPointer(_intents);", "// poll removed", "a window whose pointer fact never reaches the plugin" },
		{ nameof(TheWindowHangsOnTheSameCanvasAsTheLauncher), "adapter/OnlineUiSurfaceHost.cs", "_window = OnlineUiWindowView.Create(root.transform, _log, _intents.Enqueue);", "_window = null;", "a window that is never built on CUO's canvas" },
		{ nameof(TheRowsAreWrappedByTheRuntimesOwnRule), "adapter/OnlineUiWindowView.cs", "OnlineUiRowLayout.LineOf(widths, ContentWidth, LineSpacing)", "new int[widths.Length]", "rows wrapped by no rule at all" },
		{ nameof(AControlIsWrittenOnlyWhereItChanged), "adapter/OnlineUiControlView.cs", "if (kindChanged || _text != element.Text)", "if (true)", "a control rewritten every frame" },
		{ nameof(ATextFieldIsNotOverwrittenWhileThePlayerTypes), "adapter/OnlineUiControlView.cs", "if (!input.isFocused && _value != element.Value)", "if (_value != element.Value)", "a field that overwrites the caret while the player types" },
		{ nameof(EveryInteractiveControlReportsItsOwnId), "adapter/OnlineUiControlView.cs", "OnlineUiIntentKind.ControlToggled, _id, Flag: value", "OnlineUiIntentKind.ControlToggled, \"\", Flag: value", "a toggle whose click cannot be attributed" },
		{ nameof(ThePointerFactIsPolledFromTheWindowsRect), "adapter/OnlineUiWindowView.cs", "RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, camera)", "true", "a pointer fact that never asks the pointer" },
		{ nameof(TheWindowStopsTheWorldBehindItFromBeingClicked), "adapter/OnlineUiWindowView.cs", "panel.raycastTarget = true;", "panel.raycastTarget = false;", "a frame that lets clicks fall through to the world" },
		{ nameof(TheTitleBarDragsTheWindow), "adapter/OnlineUiWindowDragHandler.cs", "_target.anchoredPosition += eventData.delta / scale;", "_target.anchoredPosition += Vector2.zero;", "a title bar that does not drag" },
		{ nameof(ThePluginDispatchesEveryIntentKind), "plugin/OnlineUiHost.cs", "case OnlineUiIntentKind.ControlEdited:", "case OnlineUiIntentKind.ControlInvoked:", "an intent kind the plugin silently drops" },
		{ nameof(AControlTakesTheGamesRowSizeWhenTheModelGivesNone), "adapter/OnlineUiControlFactory.cs", "layout.preferredHeight = authoredHeight;", "layout.preferredHeight = -1f;", "a control whose prefab size is never seeded into the layout" },
		{ nameof(AnIntentForAControlTheWindowNoLongerOffersIsDropped), "plugin/OnlineUiWindow.cs", "if (!_actions.TryGetValue(intent.ControlId, out var action))", "if (false)", "an intent applied without checking that its control still exists" },
		{ nameof(TheCloseControlIsTheShellsAndItsMeaningIsThePlugins), "plugin/OnlineUiWindow.cs", "_actions[OnlineUiControlIds.WindowClose] = _ => _state.Visible = false;", "_actions[\"window.dismiss\"] = _ => _state.Visible = false;", "a close control whose two halves disagree about its id" },
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
		nameof(TheWindowsControlsAreTheGamesOwnRowPrefabs) => broken => UsesTheGamesRowPrefabs(broken, Adapter("OnlineUiWindowView.cs")),
		nameof(TheWindowIsShownOnlyWhileTheFrameCarriesAModel) => TheFrameDrivesVisibility,
		nameof(TheWindowHangsOnTheSameCanvasAsTheLauncher) => CreatedUnderCuosCanvas,
		nameof(TheRowsAreWrappedByTheRuntimesOwnRule) => WrappedByTheRuntimeRule,
		nameof(AControlIsWrittenOnlyWhereItChanged) => WritesOnlyChanges,
		nameof(ATextFieldIsNotOverwrittenWhileThePlayerTypes) => LeavesAFocusedFieldAlone,
		nameof(EveryInteractiveControlReportsItsOwnId) => ReportsTheCurrentId,
		nameof(ThePointerFactIsPolledFromTheWindowsRect) => PollsTheWindowsRect,
		nameof(TheWindowStopsTheWorldBehindItFromBeingClicked) => TheFrameSwallowsThePointer,
		nameof(TheTitleBarDragsTheWindow) => broken => TheTitleBarDrags(Adapter("OnlineUiWindowView.cs"), broken),
		nameof(AControlTakesTheGamesRowSizeWhenTheModelGivesNone) => SeedsThePrefabsOwnSize,
		nameof(ThePluginDispatchesEveryIntentKind) => DispatchesEveryIntentKind,
		nameof(AnIntentForAControlTheWindowNoLongerOffersIsDropped) => broken => DropsIntentsForGoneControls(broken, Plugin("OnlineUiHost.cs")),
		nameof(TheCloseControlIsTheShellsAndItsMeaningIsThePlugins) => broken => ClosesThroughTheSharedId(broken, Adapter("OnlineUiWindowView.cs")),
		_ => throw new InvalidOperationException($"no matcher is registered for the pin `{pin}`"),
	};

	/// <summary>The game's own row prefabs, one per kind, and the frame's art read from the same family.</summary>
	private static bool UsesTheGamesRowPrefabs(string controlSource, string windowSource)
	{
		var flat = Flatten(controlSource);

		return flat.Contains("internal const string ButtonRowPrefabPath = \"Special/GameSettingLanguage\";", StringComparison.Ordinal)
			&& flat.Contains("OnlineUiElementKind.Toggle => \"Special/GameSettingBool\",", StringComparison.Ordinal)
			&& flat.Contains("OnlineUiElementKind.Dropdown => \"Special/GameSettingDropdown\",", StringComparison.Ordinal)
			&& flat.Contains("OnlineUiElementKind.TextField => \"Special/GameSettingInt\",", StringComparison.Ordinal)
			&& flat.Contains("OnlineUiElementKind.Slider => \"Special/GameSettingFloat\",", StringComparison.Ordinal)
			&& Flatten(windowSource).Contains("ReadGameRowTemplate(root.transform, out var typography", StringComparison.Ordinal);
	}

	/// <summary>The frame is the only thing that opens and closes the window, and its rect is polled even
	/// while it is closed.</summary>
	private static bool TheFrameDrivesVisibility(string hostSource)
	{
		var flat = Flatten(hostSource);

		return flat.Contains("_window.SetVisible(frame.Window is not null);", StringComparison.Ordinal)
			&& flat.Contains("_window.Apply(model);", StringComparison.Ordinal)
			&& flat.Contains("_window.PollPointer(_intents);", StringComparison.Ordinal);
	}

	private static bool CreatedUnderCuosCanvas(string hostSource) =>
		Flatten(hostSource).Contains(
			"_window = OnlineUiWindowView.Create(root.transform, _log, _intents.Enqueue);",
			StringComparison.Ordinal);

	/// <summary>The wrap is the Runtime's rule, against the content width the window declares.</summary>
	private static bool WrappedByTheRuntimeRule(string windowSource)
	{
		var flat = Flatten(windowSource);

		return flat.Contains("internal const float ContentWidth = Width - (2f * Padding);", StringComparison.Ordinal)
			&& flat.Contains("var lines = OnlineUiRowLayout.LineOf(widths, ContentWidth, LineSpacing);", StringComparison.Ordinal);
	}

	private static bool WritesOnlyChanges(string controlSource)
	{
		var flat = Flatten(controlSource);

		return flat.Contains("if (kindChanged || _text != element.Text)", StringComparison.Ordinal)
			&& flat.Contains("if (kindChanged || _selected != element.Selected)", StringComparison.Ordinal)
			&& flat.Contains("if (element.Width > 0f && !_layout.preferredWidth.Equals(element.Width))", StringComparison.Ordinal);
	}

	private static bool LeavesAFocusedFieldAlone(string controlSource)
	{
		var flat = Flatten(controlSource);

		return flat.Contains("if (!input.isFocused && _value != element.Value)", StringComparison.Ordinal)
			&& flat.Contains("input.SetTextWithoutNotify(element.Value);", StringComparison.Ordinal);
	}

	/// <summary>Every listener reports the view's CURRENT id and the value the control now holds, and a
	/// control whose element carries no id reports nothing: there is no action it could belong to.</summary>
	private static bool ReportsTheCurrentId(string controlSource)
	{
		var flat = Flatten(controlSource);

		return flat.Contains("if (_id.Length > 0)", StringComparison.Ordinal)
			&& flat.Contains(
				"_report(new OnlineUiIntent(OnlineUiIntentKind.ControlInvoked, _id));",
				StringComparison.Ordinal)
			&& flat.Contains(
				"_report(new OnlineUiIntent(OnlineUiIntentKind.ControlToggled, _id, Flag: value)));",
				StringComparison.Ordinal)
			&& flat.Contains(
				"_report(new OnlineUiIntent(OnlineUiIntentKind.ControlSelected, _id, Index: index)));",
				StringComparison.Ordinal)
			&& flat.Contains(
				"_report(new OnlineUiIntent(OnlineUiIntentKind.ControlChanged, _id, Number: value)));",
				StringComparison.Ordinal)
			&& flat.Contains(
				"_report(new OnlineUiIntent(OnlineUiIntentKind.ControlEdited, _id, Text: text)));",
				StringComparison.Ordinal);
	}

	private static bool PollsTheWindowsRect(string windowSource)
	{
		var flat = Flatten(windowSource);

		return flat.Contains("var hovered = _root.activeInHierarchy", StringComparison.Ordinal)
			&& flat.Contains(
				"&& RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, camera);",
				StringComparison.Ordinal)
			&& flat.Contains(
				"hovered ? OnlineUiIntentKind.WindowHoverEntered : OnlineUiIntentKind.WindowHoverLeft",
				StringComparison.Ordinal)
			&& flat.Contains("if (hovered == _hovered) { return; }", StringComparison.Ordinal);
	}

	private static bool TheFrameSwallowsThePointer(string windowSource) =>
		Flatten(windowSource).Contains("panel.raycastTarget = true;", StringComparison.Ordinal);

	private static bool TheTitleBarDrags(string windowSource, string handlerSource)
	{
		var flat = Flatten(windowSource);

		return flat.Contains("titleBar.gameObject.AddComponent<OnlineUiWindowDragHandler>().Bind(rect);", StringComparison.Ordinal)
			&& Flatten(handlerSource).Contains("_target.anchoredPosition += eventData.delta / scale;", StringComparison.Ordinal);
	}

	/// <summary>
	/// Every member of <see cref="OnlineUiIntentKind"/> — enumerated from the enum itself, never listed by
	/// hand — must appear as a case label in the plugin's drain, and the control kinds must reach the
	/// action table. A further kind added to the vocabulary therefore fails here instead of being dropped
	/// silently at runtime, and a kind this test does not know about cannot be forgotten.
	/// </summary>
	private static bool DispatchesEveryIntentKind(string hostSource)
	{
		var flat = Flatten(hostSource);

		foreach (var kind in Enum.GetValues(typeof(OnlineUiIntentKind)).Cast<OnlineUiIntentKind>())
		{
			if (!flat.Contains($"case OnlineUiIntentKind.{kind}:", StringComparison.Ordinal))
			{
				return false;
			}
		}

		return flat.Contains("ApplyControlIntent(intent);", StringComparison.Ordinal);
	}

	/// <summary>The view seeds the prefab's own size into the layout it will be governed by.</summary>
	private static bool SeedsThePrefabsOwnSize(string controlSource)
	{
		var flat = Flatten(controlSource);

		return flat.Contains("var authored = prefab != null ? rect.sizeDelta : Vector2.zero;", StringComparison.Ordinal)
			&& flat.Contains("if (layout.preferredHeight <= 0f)", StringComparison.Ordinal)
			&& flat.Contains("layout.preferredHeight = authoredHeight;", StringComparison.Ordinal)
			&& flat.Contains("if (layout.preferredWidth <= 0f && authoredWidth > 0f)", StringComparison.Ordinal)
			&& flat.Contains("layout.preferredWidth = authoredWidth;", StringComparison.Ordinal);
	}

	private static bool DropsIntentsForGoneControls(string windowSource, string hostSource)
	{
		var flat = Flatten(windowSource);

		return flat.Contains("if (!_actions.TryGetValue(intent.ControlId, out var action))", StringComparison.Ordinal)
			&& flat.Contains("return false;", StringComparison.Ordinal)
			&& Flatten(hostSource).Contains("if (!_onlineUi.Window.Apply(intent))", StringComparison.Ordinal);
	}

	private static bool ClosesThroughTheSharedId(string windowSource, string windowViewSource)
	{
		var flat = Flatten(windowSource);
		var view = Flatten(windowViewSource);

		return flat.Contains("_actions[OnlineUiControlIds.WindowClose] = _ => _state.Visible = false;", StringComparison.Ordinal)
			&& view.Contains("OnlineUiElementModel.Button(OnlineUiControlIds.WindowClose, CloseCaption, CloseWidth)", StringComparison.Ordinal);
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
			_ => throw new InvalidOperationException($"unknown tree `{tree}` in `{file}`"),
		};
	}

	private static string Plugin(string fileName) => ReadNormalised(Path.Combine(PluginDirectory, fileName));

	private static string Adapter(string fileName) => ReadNormalised(Path.Combine(GameAdapterDirectory, "OnlineUi", fileName));

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
