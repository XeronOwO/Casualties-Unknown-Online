using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The Online UI's layout and pointer detail (ticket online-ui-layout-and-input-detail-pass): the shell's
/// bands come from the Runtime's own arithmetic, every control of a page is the same compact height, a label is
/// as tall as its wrapped text, the frame keeps the game's border, the inside of one of the game's rows is CUO's
/// geometry, an open dropdown's list rides the window's own popup layer, and a control the game's prefab left
/// unclickable is given a pointer surface and REPORTED.
///
/// <para>
/// These are the user's acceptance findings of 2026-09-27 written as pins, each with a mutation of the real
/// source as its negative sample, so none of them can pass vacuously, plus the parts of the cycle of
/// 2026-10-14 that belong to THIS family: a row's own prefab art does not draw
/// (<see cref="TheRowsOwnPrefabArtDoesNotDraw"/>), and the surfaces build their parts in one place
/// (<see cref="TheSurfacesBuildTheirPartsInOnePlace"/>). How WIDE an element is — the rule, the control's
/// authored size and the element that is its own control — is <see cref="OnlineUiControlWidthPinTests"/>, one
/// family per class. What these pins cannot see is a rendering: whether the game's prefabs behave under CUO's
/// geometry, whether a popup lands where the player expects and whether the result reads as this game are the
/// acceptance run's.
/// </para>
/// </summary>
public sealed class OnlineUiLayoutDetailPinTests
{
	[Fact]
	public void ThePageStartsBelowTheTabStrip() =>
		Assert.True(
			TheShellUsesTheRuntimeLayout(Adapter("OnlineUiWindowView.cs")),
			"the shell's three bands are placed from OnlineUiWindowLayout's numbers, so the page cannot start inside the tab strip the way the first cut's own sum left it");

	[Fact]
	public void EveryControlOfAPageIsTheSameCompactHeight() =>
		Assert.True(
			OneControlHeight(Adapter("OnlineUiControlView.cs")),
			"a page must read as one column of controls: every control that is not a label takes the layout's own compact height instead of the game's row prefab's much taller authored height");

	[Fact]
	public void ALabelIsAsTallAsItsWrappedText() =>
		Assert.True(
			LabelsGrowWithTheirText(Adapter("OnlineUiControlView.cs")),
			"a label's height is its own wrapped text (TMP is the layout element that knows it) over a one-line floor: a fixed one-line height is what made a two-line hint paint over the row below it");

	[Fact]
	public void TheFrameKeepsTheGamesOwnBorder() =>
		Assert.True(
			TheFrameShowsTheGamesBorder(Adapter("OnlineUiWindowView.cs"), Adapter("OnlineUiControlFactory.cs"), Adapter("OnlineUiPanelView.cs")),
			"the game's own windows show a light border and the sprite is what carries it: the frame keeps the sprite untinted and the dark surface is a fill laid inside it");

	[Fact]
	public void TheRowsInsideIsCuosGeometry() =>
		Assert.True(
			TheRowInsideIsLaidOutByCuo(
				Adapter("OnlineUiControlView.cs"),
				Adapter("OnlineUiRowGeometry.cs"),
				Adapter("OnlineUiBoxGeometry.cs"),
				Adapter("OnlineUiSliderGeometry.cs")),
			"the game places its rows' children by hand for its own screen: a row instantiated into this window must have its label and its control laid out by CUO, and a control's own insides stretched onto the box CUO sizes");

	[Fact]
	public void TheRowsOwnPrefabArtDoesNotDraw() =>
		Assert.True(
			TheRowsOwnArtIsHidden(Adapter("OnlineUiRowGeometry.cs")),
			"the game draws a grey frame around every row of its own full-width settings screen, and a row of a page is a container CUO arranges rather than a control: the frame is hidden so the page's own fill shows through (the user's report of 2026-10-15), while the control's own box and every element that IS a control keep the game's art");

	[Fact]
	public void TheSurfacesBuildTheirPartsInOnePlace() =>
		Assert.True(
			TheSurfacePartsHaveOneOwner(
				Adapter("OnlineUiSurfaceParts.cs"),
				Adapter("OnlineUiWindowView.cs"),
				Adapter("OnlineUiPanelView.cs"),
				Adapter("OnlineUiWorldOverlayView.cs")),
			"the window and the two panels each carried their own copy of the same three construction helpers, byte for byte (the cycle of 2026-10-15 found them): one place builds them, so a fix cannot land on one surface and miss its sibling");

	[Fact]
	public void AnOpenDropdownListRidesTheWindowsPopupLayer() =>
		Assert.True(
			ThePopupLayerCarriesTheLists(Adapter("OnlineUiWindowView.cs"), Adapter("OnlineUiDropdownPopup.cs"), Adapter("OnlineUiControlView.cs")),
			"an open dropdown's options are the user's report: a list built inside the page is clipped by the page's mask and drawn under the rows after it, so the templates must live on the window's popup layer, sorted above CUO's own canvas");

	[Fact]
	public void AControlWithNoPointerSurfaceIsGivenOneAndReported() =>
		Assert.True(
			PointerSurfacesAreEnsured(Adapter("OnlineUiControlView.cs"), Adapter("OnlineUiRowGeometry.cs"), Adapter("OnlineUiViewSource.cs")),
			"uGUI hit-tests graphics: a field or a dropdown whose own box carries no raycast target is a box the player cannot click, which is the user's report — CUO gives it a surface and says so once per kind");

	[Fact]
	public void ASectionHeadingCarriesItsOwnRoom() =>
		Assert.True(
			TheHeadingOwnsItsSpacing(Plugin("OnlineUiPageBuilder.cs"), Adapter("OnlineUiWindowView.cs")),
			"a heading OPENS a block, so the room above and below it belongs to the builder's Section and to the layout's own scale — the acceptance pass found headings flush against the row above them because the gap lived in each drawer's memory, and a spacer row is how a uGUI layout group is given room for one child");

	[Fact]
	public void TheTabStripKeepsItsOwnHeight() =>
		Assert.True(
			BandsKeepTheirOwnHeight(Adapter("OnlineUiWindowView.cs")),
			"a band's declared height is the height it gets: the tab strip's inner layout group reported its children's flexible sum, so the shell fed the strip the window's leftover height and the row grew to 333 units — ten times TabHeight (user report, 2026-09-27)");

	public static TheoryData<string, string, string, string, string> Mutations => new()
	{
		{ nameof(ThePageStartsBelowTheTabStrip), "adapter/OnlineUiWindowView.cs", "shell.spacing = OnlineUiWindowLayout.TabGap;", "shell.spacing = 0f;", "bands stacked with no gap between them" },
		{ nameof(ThePageStartsBelowTheTabStrip), "adapter/OnlineUiWindowView.cs", "DeclareBandHeight(tabRow, TabHeight);", "// the tab strip declares no height of its own", "a tab strip the engine cannot size" },
		{ nameof(EveryControlOfAPageIsTheSameCompactHeight), "adapter/OnlineUiControlView.cs", "_layout.preferredHeight = OnlineUiWindowLayout.ControlHeight;", "_layout.preferredHeight = _rect.sizeDelta.y;", "a control that takes the game's own authored height" },
		{ nameof(ALabelIsAsTallAsItsWrappedText), "adapter/OnlineUiControlView.cs", "_layout.preferredHeight = -1f;", "_layout.preferredHeight = _typography.Size + OnlineUiControlFactory.LabelHeightPadding;", "a label pinned to one line while its text wraps" },
		{ nameof(TheFrameKeepsTheGamesOwnBorder), "adapter/OnlineUiControlFactory.cs", "border.color = Color.white;", "border.color = fill;", "a frame that tints the game's border away" },
		{ nameof(TheFrameKeepsTheGamesOwnBorder), "adapter/OnlineUiWindowView.cs", "OnlineUiControlFactory.MakeFrame(root, sprite, imageType, pixelsPerUnit, PanelTint);", "// the frame is tinted by hand", "a frame that never lays the fill inside the border" },
		{ nameof(TheRowsInsideIsCuosGeometry), "adapter/OnlineUiControlView.cs", "OnlineUiRowGeometry.PrepareInternals(dropdown, input, toggle);", "// the game's own row places its own children", "a row left at the rects the game's own settings screen authored for a screen three times this height" },
		{ nameof(TheRowsInsideIsCuosGeometry), "adapter/OnlineUiRowGeometry.cs", "var width = Mathf.Max(controlFloor, DeclaredWidthOf(control));", "var width = controlFloor;", "a control left at the model's floor with the game's own authored width ignored" },
		{ nameof(TheRowsInsideIsCuosGeometry), "adapter/OnlineUiRowGeometry.cs", "LayOutInterior(control, width, prefabValue);", "// the control's insides are never placed", "a control whose own text area, caret and caption are left where the game's own screen put them" },
		// The slider's own reading: its parts are the control's MEANING, so hiding them is the user's report of
		// 2026-10-14 and placing them is the fix.
		{ nameof(TheRowsInsideIsCuosGeometry), "adapter/OnlineUiRowGeometry.cs", "OnlineUiSliderGeometry.LayOut(slider, control, controlWidth, prefabValue);", "OnlineUiBoxGeometry.LayOutInterior(control, controlWidth);", "a slider sent down the box rule, which hides the track, the fill and the handle it is read by" },
		{ nameof(TheRowsInsideIsCuosGeometry), "adapter/OnlineUiSliderGeometry.cs", "PlaceTrack(slider.targetGraphic);", "OnlineUiBoxGeometry.HidePrefabBoxArt(slider.targetGraphic.rectTransform);", "a slider whose track keeps the prefab's hidden art — the invisible track the user reported" },
		{ nameof(TheRowsInsideIsCuosGeometry), "adapter/OnlineUiSliderGeometry.cs", "PlaceContainer(slider.fillRect, control, controlWidth, insetsTheTrack: true);", "// the filled fraction is left where the prefab put it", "a slider whose filled fraction is never placed on the compact box" },
		{ nameof(TheRowsInsideIsCuosGeometry), "adapter/OnlineUiSliderGeometry.cs", "PlaceContainer(slider.handleRect, control, controlWidth, insetsTheTrack: false);", "// the handle is left where the prefab put it", "a slider with no handle placed inside its box" },
		{ nameof(TheRowsInsideIsCuosGeometry), "adapter/OnlineUiSliderGeometry.cs", "Tint(slider);", "// the slider keeps whatever colours the prefab carried", "a slider drawn in the prefab's own colours instead of the page's" },
		{ nameof(TheRowsOwnPrefabArtDoesNotDraw), "adapter/OnlineUiRowGeometry.cs", "OnlineUiBoxGeometry.HidePrefabBoxArt((RectTransform)row.transform);", "// the game's own row frame stays", "the grey frame the game draws around every row of its own screen, left on a page" },
		{ nameof(TheRowsOwnPrefabArtDoesNotDraw), "adapter/OnlineUiRowGeometry.cs", "OnlineUiBoxGeometry.HidePrefabBoxArt((RectTransform)row.transform);", "OnlineUiBoxGeometry.HidePrefabBoxArt((RectTransform)control.transform);", "a row that hides its CONTROL's box instead of its own decoration" },
		{ nameof(TheSurfacesBuildTheirPartsInOnePlace), "adapter/OnlineUiSurfaceParts.cs", "internal static void Stretch(RectTransform rect, float left, float right, float top, float bottom)", "private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)", "a shared construction part nothing outside its own file can reach" },
		{ nameof(TheSurfacesBuildTheirPartsInOnePlace), "adapter/OnlineUiSurfaceParts.cs", "text.raycastTarget = false;", "text.raycastTarget = true;", "the one label construction, with every surface's label answering the pointer" },
		{ nameof(TheSurfacesBuildTheirPartsInOnePlace), "adapter/OnlineUiSurfaceParts.cs", "text.font = typography.Font;", "// the game's own font is not applied", "a label construction that leaves the game's own font unapplied" },
		{ nameof(TheSurfacesBuildTheirPartsInOnePlace), "adapter/OnlineUiWindowView.cs", "private static void DeclareBandHeight(RectTransform band, float height)", "private static void Stretch(RectTransform rect, float left, float right, float top, float bottom) { } private static void DeclareBandHeight(RectTransform band, float height)", "a surface that grows its own copy of a shared part again, which is how the window and the panels drifted apart" },
		{ nameof(TheSurfacesBuildTheirPartsInOnePlace), "adapter/OnlineUiWorldOverlayView.cs", "var text = OnlineUiSurfaceParts.CreateLabel(name, parent, typography, OnlineUiTextStyle.Muted);", "var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();", "a label built by hand beside the one construction the surfaces share" },
		{ nameof(AnOpenDropdownListRidesTheWindowsPopupLayer), "adapter/OnlineUiWindowView.cs", "var popup = OnlineUiDropdownPopup.Create(rect, OnlineUiSurfaceHost.SortingOrder);", "var popup = null!;", "a window with no popup layer for its dropdowns" },
		{ nameof(AnOpenDropdownListRidesTheWindowsPopupLayer), "adapter/OnlineUiDropdownPopup.cs", "template.SetParent(_layer, worldPositionStays: false);", "// the template stays inside the page", "a list instantiated inside the page's mask again" },
		{ nameof(AControlWithNoPointerSurfaceIsGivenOneAndReported), "adapter/OnlineUiControlView.cs", "pointerFixed = OnlineUiRowGeometry.EnsurePointerSurface(control.gameObject);", "pointerFixed = false;", "a field left without a pointer surface" },
		{ nameof(AControlWithNoPointerSurfaceIsGivenOneAndReported), "adapter/OnlineUiRowGeometry.cs", "graphic.raycastTarget = true;", "graphic.raycastTarget = false;", "a pointer surface that still refuses the raycast" },
		{ nameof(AControlWithNoPointerSurfaceIsGivenOneAndReported), "adapter/OnlineUiViewSource.cs", "view.FixedPointerSurface && _reportedPointerFixes.Add(element.Kind)", "view.FixedPointerSurface", "a control the game left unclickable that nobody was told about" },
		{ nameof(ASectionHeadingCarriesItsOwnRoom), "plugin/OnlineUiPageBuilder.cs", "_rows.Add(OnlineUiRowModel.Space(OnlineUiWindowLayout.SectionGap));", "// no room above a heading", "a heading with no room above it, which is the acceptance pass' own finding" },
		{ nameof(ASectionHeadingCarriesItsOwnRoom), "adapter/OnlineUiWindowView.cs", "row.SetGap(0, model.Gap > 0f ? model.Gap : GapHeight);", "row.SetGap(0, GapHeight);", "a page that ignores the room its rows asked for" },
		{ nameof(TheTabStripKeepsItsOwnHeight), "adapter/OnlineUiWindowView.cs", "element.flexibleHeight = 0f;", "element.flexibleHeight = 1f;", "a band that absorbs the window's leftover height — the tab strip grew to 333 units" },
	};

	/// <summary>
	/// Every pin of this class, against a broken source: the anchor must be in the real file, the replacement
	/// must change it, and the pin's own matcher must then be false.
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
		// Two rows point at this pin — one for the tab strip, one for the page — and both anchor in the same
		// file, so the pin's own matcher is its matcher.
		nameof(ThePageStartsBelowTheTabStrip) => TheShellUsesTheRuntimeLayout,
		nameof(EveryControlOfAPageIsTheSameCompactHeight) => OneControlHeight,
		nameof(ALabelIsAsTallAsItsWrappedText) => LabelsGrowWithTheirText,
		nameof(TheFrameKeepsTheGamesOwnBorder) => broken => TheFrameShowsTheGamesBorder(
			Is(broken, "internal static Image MakeFrame") ? Adapter("OnlineUiWindowView.cs") : broken,
			Is(broken, "internal static Image MakeFrame") ? broken : Adapter("OnlineUiControlFactory.cs"),
			Adapter("OnlineUiPanelView.cs")),
		nameof(TheRowsInsideIsCuosGeometry) => broken => TheRowInsideIsLaidOutByCuo(
			Is(broken, "internal sealed class OnlineUiControlView") ? broken : Adapter("OnlineUiControlView.cs"),
			Is(broken, "internal static class OnlineUiRowGeometry") ? broken : Adapter("OnlineUiRowGeometry.cs"),
			Is(broken, "internal static class OnlineUiBoxGeometry") ? broken : Adapter("OnlineUiBoxGeometry.cs"),
			Is(broken, "internal static class OnlineUiSliderGeometry") ? broken : Adapter("OnlineUiSliderGeometry.cs")),
		nameof(TheRowsOwnPrefabArtDoesNotDraw) => TheRowsOwnArtIsHidden,
		// The wrap and the model's own breaks are the paired-control ticket's business:
		// OnlineUiPairedControlPinTests holds that rule and the mutation rows that pin it.
		nameof(TheSurfacesBuildTheirPartsInOnePlace) => broken => TheSurfacePartsHaveOneOwner(
			Is(broken, "internal static class OnlineUiSurfaceParts") ? broken : Adapter("OnlineUiSurfaceParts.cs"),
			Is(broken, "internal sealed class OnlineUiWindowView") ? broken : Adapter("OnlineUiWindowView.cs"),
			Is(broken, "internal sealed class OnlineUiPanelView") ? broken : Adapter("OnlineUiPanelView.cs"),
			Is(broken, "internal sealed class OnlineUiWorldOverlayView") ? broken : Adapter("OnlineUiWorldOverlayView.cs")),
		nameof(AnOpenDropdownListRidesTheWindowsPopupLayer) => broken => ThePopupLayerCarriesTheLists(
			Is(broken, "internal sealed class OnlineUiDropdownPopup") ? Adapter("OnlineUiWindowView.cs") : broken,
			Is(broken, "internal sealed class OnlineUiDropdownPopup") ? broken : Adapter("OnlineUiDropdownPopup.cs"),
			Adapter("OnlineUiControlView.cs")),
		nameof(AControlWithNoPointerSurfaceIsGivenOneAndReported) => broken => PointerSurfacesAreEnsured(
			Is(broken, "internal sealed class OnlineUiControlView") ? broken : Adapter("OnlineUiControlView.cs"),
			Is(broken, "internal static class OnlineUiRowGeometry") ? broken : Adapter("OnlineUiRowGeometry.cs"),
			Is(broken, "internal sealed class OnlineUiViewSource") ? broken : Adapter("OnlineUiViewSource.cs")),
		nameof(ASectionHeadingCarriesItsOwnRoom) => broken => TheHeadingOwnsItsSpacing(
			Is(broken, "internal sealed class OnlineUiPageBuilder") ? broken : Plugin("OnlineUiPageBuilder.cs"),
			Is(broken, "internal sealed class OnlineUiWindowView") ? broken : Adapter("OnlineUiWindowView.cs")),
		nameof(TheTabStripKeepsItsOwnHeight) => BandsKeepTheirOwnHeight,
		_ => throw new InvalidOperationException($"no matcher is registered for the pin `{pin}`"),
	};

	/// <summary>Whether a source is the file a mutation row says it is — the routing a pin with more than one
	/// file needs, since only one side of it is the broken one.</summary>
	private static bool Is(string source, string declaration) => source.Contains(declaration, StringComparison.Ordinal);

	/// <summary>
	/// The shell's bands are CHILDREN of one layout group: each declares its own height (the title bar and the
	/// tab strip fixed, the page flexible) and the engine stacks them with the layout's own spacing between
	/// them. No band's position is computed in the adapter, which is what let the page start inside the tab
	/// strip, and the derived positions live in <see cref="OnlineUiWindowLayout"/> as the shape this stacking
	/// produces.
	/// </summary>
	private static bool TheShellUsesTheRuntimeLayout(string windowSource)
	{
		var flat = Flatten(windowSource);

		return flat.Contains("var shell = root.GetComponent<VerticalLayoutGroup>();", StringComparison.Ordinal)
			&& flat.Contains("shell.spacing = OnlineUiWindowLayout.TabGap;", StringComparison.Ordinal)
			&& flat.Contains("shell.childForceExpandHeight = false;", StringComparison.Ordinal)
			&& flat.Contains("DeclareBandHeight(titleBar, TitleHeight);", StringComparison.Ordinal)
			&& flat.Contains("DeclareBandHeight(tabRow, TabHeight);", StringComparison.Ordinal)
			&& flat.Contains("scrollLayout.flexibleHeight = 1f;", StringComparison.Ordinal)
			&& !flat.Contains("TitleHeight + TabHeight", StringComparison.Ordinal);
	}

	private static bool OneControlHeight(string controlSource)
	{
		var flat = Flatten(controlSource);

		return flat.Contains("if (!_layout.preferredHeight.Equals(OnlineUiWindowLayout.ControlHeight))", StringComparison.Ordinal)
			&& flat.Contains("_layout.preferredHeight = OnlineUiWindowLayout.ControlHeight;", StringComparison.Ordinal)
			&& flat.Contains("_layout.minHeight = OnlineUiWindowLayout.ControlHeight;", StringComparison.Ordinal);
	}

	/// <summary>A label leaves its own height to TMP (the layout element that knows the wrapped text) and
	/// keeps only a one-line floor.</summary>
	private static bool LabelsGrowWithTheirText(string controlSource)
	{
		var flat = Flatten(controlSource);

		return flat.Contains("_layout.preferredHeight = -1f;", StringComparison.Ordinal)
			&& flat.Contains("_layout.minHeight = _typography.Size + OnlineUiControlFactory.LabelHeightPadding;", StringComparison.Ordinal);
	}

	/// <summary>The frame shows the game's border: the sprite stays untinted on the frame's own image and the
	/// dark surface is a fill inside it.</summary>
	private static bool TheFrameShowsTheGamesBorder(string windowSource, string factorySource, string panelSource)
	{
		var flat = Flatten(factorySource);

		return Flatten(windowSource).Contains(
				"OnlineUiControlFactory.MakeFrame(root, sprite, imageType, pixelsPerUnit, PanelTint);",
				StringComparison.Ordinal)
			&& Flatten(panelSource).Contains(
				"OnlineUiControlFactory.MakeFrame(root, sprite, imageType, pixelsPerUnit, PanelTint);",
				StringComparison.Ordinal)
			&& flat.Contains("border.color = Color.white;", StringComparison.Ordinal)
			&& flat.Contains("rect.offsetMin = new Vector2(FrameBorder, FrameBorder);", StringComparison.Ordinal)
			&& flat.Contains("rect.offsetMax = new Vector2(-FrameBorder, -FrameBorder);", StringComparison.Ordinal);
	}

	/// <summary>The row's own label and control are placed by CUO's group at the width CUO declares, and the
	/// control's insides are laid out one rect at a time — never measured from content that stretches inside the
	/// box. Which of the two inside-rules runs is the control's own: a box
	/// (<see cref="OnlineUiBoxGeometry"/>) or a slider's reading (<see cref="OnlineUiSliderGeometry"/>), and a
	/// slider keeps the game's own track, fill and handle rather than having them hidden with the prefab's
	/// decoration (ticket online-ui-paired-controls-and-page-scroll, S1).</summary>
	private static bool TheRowInsideIsLaidOutByCuo(string controlSource, string geometrySource, string boxSource, string sliderSource)
	{
		var geometry = Flatten(geometrySource);
		var box = Flatten(boxSource);
		var slider = Flatten(sliderSource);
		var flat = Flatten(controlSource);

		return geometry.Contains("private static HorizontalLayoutGroup ConfigureGroup(HorizontalLayoutGroup? group, GameObject row, int trailingRoom)", StringComparison.Ordinal)
			&& geometry.Contains("added = row.AddComponent<HorizontalLayoutGroup>();", StringComparison.Ordinal)
			&& geometry.Contains("flexibleWidth: columnWidth > 0f ? 0f : 1f,", StringComparison.Ordinal)
			&& geometry.Contains("var width = Mathf.Max(controlFloor, DeclaredWidthOf(control));", StringComparison.Ordinal)
			&& geometry.Contains("LayOut(control, flexibleWidth: ownsItsLine ? 1f : 0f, width: width, height: height);", StringComparison.Ordinal)
			&& geometry.Contains("LayOutInterior(control, width, prefabValue);", StringComparison.Ordinal)
			&& geometry.Contains("internal static void LayOutRow(", StringComparison.Ordinal)
			&& geometry.Contains("OnlineUiSliderGeometry.LayOut(slider, control, controlWidth, prefabValue);", StringComparison.Ordinal)
			&& geometry.Contains("OnlineUiBoxGeometry.LayOutInterior(control, controlWidth);", StringComparison.Ordinal)
			&& box.Contains("private static void StretchInside(RectTransform child, float left, float right)", StringComparison.Ordinal)
			&& box.Contains("private static bool IsContainer(RectTransform child) =>", StringComparison.Ordinal)
			&& box.Contains("HidePrefabBoxArt(child);", StringComparison.Ordinal)
			// A slider's own parts are PLACED, never hidden: hiding them is the defect S1 undoes.
			&& slider.Contains("internal static void LayOut(Slider slider, RectTransform control, float controlWidth, RectTransform? prefabValue)", StringComparison.Ordinal)
			&& slider.Contains("PlaceTrack(slider.targetGraphic);", StringComparison.Ordinal)
			&& slider.Contains("PlaceContainer(slider.fillRect, control, controlWidth, insetsTheTrack: true);", StringComparison.Ordinal)
			&& slider.Contains("PlaceContainer(slider.handleRect, control, controlWidth, insetsTheTrack: false);", StringComparison.Ordinal)
			&& slider.Contains("Tint(slider);", StringComparison.Ordinal)
			&& !slider.Contains("HidePrefabBoxArt", StringComparison.Ordinal)
			&& flat.Contains("OnlineUiRowGeometry.LayOutPairedRow(_root, _layout, _labelRect, _controlRect, controlWidth, columnWidth, prefabValue);", StringComparison.Ordinal)
			&& flat.Contains("OnlineUiRowGeometry.PrepareInternals(dropdown, input, toggle);", StringComparison.Ordinal);
	}

	/// <summary>The row's own prefab art is hidden — the grey frame the game draws around one of its own settings
	/// rows — and the CONTROL's own box is not: a row of a page shows exactly one box, the one CUO draws.</summary>
	private static bool TheRowsOwnArtIsHidden(string geometrySource)
	{
		var flat = Flatten(geometrySource);

		return flat.Contains("OnlineUiBoxGeometry.HidePrefabBoxArt((RectTransform)row.transform);", StringComparison.Ordinal)
			&& !flat.Contains("HidePrefabBoxArt((RectTransform)control.transform);", StringComparison.Ordinal);
	}

	/// <summary>The surface parts have ONE owner: the type that constructs them declares all three, and no
	/// surface keeps a copy of its own — the window and the two panels used to, which is how a fix lands on one
	/// of them and not on its sibling. The overlay may carry its own fit policy, but it builds its label from
	/// the shared construction.</summary>
	private static bool TheSurfacePartsHaveOneOwner(
		string partsSource,
		string windowSource,
		string panelSource,
		string overlaySource)
	{
		var parts = Flatten(partsSource);

		return parts.Contains("internal static RectTransform CreateRect(string name, Transform parent, params Type[] components)", StringComparison.Ordinal)
			&& parts.Contains("internal static TextMeshProUGUI CreateLabel(", StringComparison.Ordinal)
			&& parts.Contains("internal static void Stretch(RectTransform rect, float left, float right, float top, float bottom)", StringComparison.Ordinal)
			// The one label construction is also where the game's own font and its size ladder are applied, and
			// where the raycast is refused — the three things every surface's label used to set for itself.
			&& parts.Contains("text.fontSize = typography.SizeOf(style);", StringComparison.Ordinal)
			&& parts.Contains("text.font = typography.Font;", StringComparison.Ordinal)
			&& parts.Contains("text.raycastTarget = false;", StringComparison.Ordinal)
			&& !Flatten(windowSource).Contains("private static RectTransform CreateRect(", StringComparison.Ordinal)
			&& !Flatten(windowSource).Contains("private static void Stretch(", StringComparison.Ordinal)
			&& !Flatten(panelSource).Contains("private static RectTransform CreateRect(", StringComparison.Ordinal)
			&& !Flatten(panelSource).Contains("private static void Stretch(", StringComparison.Ordinal)
			&& Flatten(overlaySource).Contains("var text = OnlineUiSurfaceParts.CreateLabel(name, parent, typography, OnlineUiTextStyle.Muted);", StringComparison.Ordinal);
	}

	/// <summary>The window owns a popup layer, every dropdown hands its template to it, the layer sorts above
	/// CUO's own canvas and its list is normalised every frame the window is applied.</summary>
	private static bool ThePopupLayerCarriesTheLists(string windowSource, string popupSource, string controlSource)
	{
		var window = Flatten(windowSource);
		var popup = Flatten(popupSource);

		return window.Contains("var popup = OnlineUiDropdownPopup.Create(rect, OnlineUiSurfaceHost.SortingOrder);", StringComparison.Ordinal)
			&& window.Contains("view.AdoptPopup(_popup);", StringComparison.Ordinal)
			&& window.Contains("_popup.Normalise();", StringComparison.Ordinal)
			&& popup.Contains("canvas.overrideSorting = true;", StringComparison.Ordinal)
			&& popup.Contains("canvas.sortingOrder = surfaceSortingOrder + 1;", StringComparison.Ordinal)
			&& popup.Contains("canvas.overrideSorting = true;", StringComparison.Ordinal)
			&& popup.Contains("template.SetParent(_layer, worldPositionStays: false);", StringComparison.Ordinal)
			&& Flatten(controlSource).Contains("popup.Adopt(_dropdown);", StringComparison.Ordinal);
	}

	/// <summary>A dropdown or a field is given a pointer surface when the game's row left it without one, and
	/// the window reports it once per kind.</summary>
	private static bool PointerSurfacesAreEnsured(string controlSource, string geometrySource, string windowSource)
	{
		var geometry = Flatten(geometrySource);

		return Flatten(controlSource).Contains("pointerFixed = OnlineUiRowGeometry.EnsurePointerSurface(control.gameObject);", StringComparison.Ordinal)
			&& geometry.Contains("if (graphic.raycastTarget)", StringComparison.Ordinal)
			&& geometry.Contains("graphic.raycastTarget = true;", StringComparison.Ordinal)
			&& Flatten(windowSource).Contains("view.FixedPointerSurface && _reportedPointerFixes.Add(element.Kind)", StringComparison.Ordinal);
	}

	/// <summary>A heading owns the room around itself: the builder adds the layout's own section room above
	/// (unless the heading opens the page) and its smaller body room below, a plain gap is the layout's block
	/// room, and the window gives a space row the room the model asked for.</summary>
	private static bool TheHeadingOwnsItsSpacing(string builderSource, string windowSource)
	{
		var flat = Flatten(builderSource);

		return flat.Contains("if (_rows.Count > 0)", StringComparison.Ordinal)
			&& flat.Contains("_rows.Add(OnlineUiRowModel.Space(OnlineUiWindowLayout.SectionGap));", StringComparison.Ordinal)
			&& flat.Contains("_rows.Add(OnlineUiRowModel.Space(OnlineUiWindowLayout.SectionBodyGap));", StringComparison.Ordinal)
			&& flat.Contains("internal void Space() => _rows.Add(OnlineUiRowModel.Space(OnlineUiWindowLayout.BlockGap));", StringComparison.Ordinal)
			&& Flatten(windowSource).Contains("row.SetGap(0, model.Gap > 0f ? model.Gap : GapHeight);", StringComparison.Ordinal);
	}

	/// <summary>A band's declared height is the height it gets: the flexible value is zeroed (only the page's
	/// own band is set flexible), so no band can absorb the window's leftover height.</summary>
	private static bool BandsKeepTheirOwnHeight(string windowSource)
	{
		var flat = Flatten(windowSource);

		return flat.Contains("element.flexibleHeight = 0f;", StringComparison.Ordinal)
			&& flat.Contains("scrollLayout.flexibleHeight = 1f;", StringComparison.Ordinal);
	}

	private static string Read(string file)
	{
		var separator = file.IndexOf('/');
		Assert.True(separator > 0, $"the mutation's file `{file}` must be written as `<tree>/<name>.cs`");
		var tree = file.Substring(0, separator);
		var name = file.Substring(separator + 1);
		return tree switch
		{
			"plugin" => ReadNormalised(Path.Combine(PluginDirectory, name)),
			"adapter" => ReadNormalised(Path.Combine(GameAdapterDirectory, "OnlineUi", name)),
			_ => throw new InvalidOperationException($"unknown tree `{tree}` in `{file}`"),
		};
	}

	private static string Plugin(string fileName) => ReadNormalised(Path.Combine(PluginDirectory, fileName));

	private static string Adapter(string fileName) => ReadNormalised(Path.Combine(GameAdapterDirectory, "OnlineUi", fileName));

	/// <summary>Comments cut and whitespace collapsed, so indentation, line endings and a comment that merely
	/// names an API cannot decide a pin.</summary>
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
