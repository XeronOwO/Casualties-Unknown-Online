using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The paired control of a page (ticket online-ui-paired-controls-and-page-scroll, S3): a row whose NAME is
/// on one line and whose control — a slider, with the editable value bound to the same number — is on the
/// next, and the page's own control COLUMN, which lines the controls of the rows that stay on one line up
/// under each other.
///
/// <para>
/// The shape is the user's ask of 2026-10-15 in their own words: a slider row read as
/// <c>背负重量系数 [slider] 0.80</c> with the control right-aligned against a label of any length, and
/// "this kind of paired control could be made into a template: the name on the first line, the slider and the
/// value on the second; squeezing it onto one line is not pretty, and when the names differ in length it
/// looks ragged".
/// </para>
///
/// <para>
/// Every pin carries a mutation of the REAL source as its negative sample, and the anchors are matched after
/// comment-stripping and whitespace flattening, so indentation, line endings and a comment that merely names
/// an API cannot decide a result. What no pin here can see is a rendering: whether the two lines read as one
/// control on screen is the acceptance run's.
/// </para>
/// </summary>
public sealed class OnlineUiPairedControlPinTests
{
	[Fact]
	public void APairedControlCarriesItsNameOnItsOwnLine() =>
		Assert.True(
			PairedIsTwoLines(
				Runtime("OnlineUiElementModel.cs"),
				Runtime("OnlineUiRowLayout.cs"),
				Adapter("OnlineUiWindowView.cs"),
				Adapter("OnlineUiRowGeometry.cs"),
				Plugin("OnlineUiPageBuilder.cs")),
			"a paired control is a row of TWO lines — the name above, the slider and its value below — so the model carries the break, the Runtime's wrap honours it and the adapter stacks the row");

	[Fact]
	public void ThePagesControlsShareOneColumn() =>
		Assert.True(
			TheControlsLineUp(
				Adapter("OnlineUiWindowRowView.cs"),
				Adapter("OnlineUiControlView.cs"),
				Adapter("OnlineUiWindowView.cs"),
				Adapter("OnlineUiPanelView.cs"),
				Adapter("OnlineUiRowGeometry.cs")),
			"the rows that stay on one line put their controls in the page's own column, measured from the models' names rather than from the boxes the layout just wrote, and a panel shares it so a control lines up wherever it is drawn");

	[Fact]
	public void APageThatDoesNotFitSaysSo() =>
		Assert.True(
			ThePageCarriesItsBar(
				Runtime("OnlineUiPageScroll.cs"),
				Adapter("OnlineUiPageScrollbar.cs"),
				Adapter("OnlineUiWindowView.cs")),
			"a page whose content runs past what is shown carries a bar that says how much more there is and where the player stands — the user's own report of 2026-10-14 (\"is there a scrollbar? … I don't see one on the right\") — and a page that FITS keeps none");

	public static TheoryData<string, string, string, string, string> Mutations => new()
	{
		{ nameof(APairedControlCarriesItsNameOnItsOwnLine), "runtime/OnlineUiElementModel.cs", "control with { NewLine = true },", "control,", "a paired row whose two halves never ask for their own lines" },
		{ nameof(APairedControlCarriesItsNameOnItsOwnLine), "runtime/OnlineUiRowLayout.cs", "if (index > 0 && index < newLine.Count && newLine[index] && fitted[index] + shift <= previous)", "if (false)", "a wrap that ignores the model's own breaks, which is the one-line row the user rejected" },
		{ nameof(APairedControlCarriesItsNameOnItsOwnLine), "runtime/OnlineUiRowLayout.cs", "lines[index] = fitted[index] + shift;", "lines[index] = fitted[index];", "a break that moves its own element down but leaves every element after it behind, so a row's lines collide" },
		{ nameof(APairedControlCarriesItsNameOnItsOwnLine), "adapter/OnlineUiWindowView.cs", "var lines = OnlineUiRowLayout.LineOf(widths, breaks, ContentWidth, LineSpacing);", "var lines = OnlineUiRowLayout.LineOf(widths, ContentWidth, LineSpacing);", "a page that wraps by the fit alone, which puts the name and its control back on one line" },
		{ nameof(APairedControlCarriesItsNameOnItsOwnLine), "adapter/OnlineUiRowGeometry.cs", "ConfigureGroup<VerticalLayoutGroup>(", "ConfigureGroup<HorizontalLayoutGroup>(", "a paired row stacked sideways, so the name and the control share one line again" },
		{ nameof(APairedControlCarriesItsNameOnItsOwnLine), "plugin/OnlineUiPageBuilder.cs", "_rows.Add(OnlineUiElementModel.Paired(", "_rows.Add(OnlineUiRowModel.Of(", "a numeric rule that goes back to the one-line row shape" },
		{ nameof(ThePagesControlsShareOneColumn), "adapter/OnlineUiWindowRowView.cs", "widest = Mathf.Max(widest, nameWidth(rowIndex, index, elements[index]));", "widest = 0f;", "a page with no column at all, so every control sits against its own label's end" },
		{ nameof(ThePagesControlsShareOneColumn), "adapter/OnlineUiWindowRowView.cs", "widest = Mathf.Max(widest, nameWidth(rowIndex, index, elements[index]));", "widest = Mathf.Max(widest, elements[index].Width);", "a column measured from the model's own floor instead of the name it draws" },
		{ nameof(ThePagesControlsShareOneColumn), "adapter/OnlineUiWindowRowView.cs", "Elements[index].LayOutRow(elements[index], widths[index], columnWidth);", "Elements[index].LayOutRow(elements[index], widths[index], 0f);", "a pass that measures the column and then lays nothing out at it" },
		{ nameof(ThePagesControlsShareOneColumn), "adapter/OnlineUiWindowRowView.cs", "private const float ColumnShare = 0.8f;", "private const float ColumnShare = 2f;", "a column cap wider than the page itself, which leaves the controls no room at all" },
		{ nameof(ThePagesControlsShareOneColumn), "adapter/OnlineUiWindowView.cs", "OnlineUiWindowRowView.ColumnWidthOf(rows, NameWidth, ContentWidth)", "0f", "a window whose rows are never aligned into a column" },
		{ nameof(ThePagesControlsShareOneColumn), "adapter/OnlineUiControlView.cs", "return OnlineUiControlSizing.CaptionTextWidth(label);", "return 0f;", "a column measured from nothing, which leaves every control where its own label ended" },
		{ nameof(ThePagesControlsShareOneColumn), "adapter/OnlineUiPanelView.cs", "OnlineUiWindowRowView.ColumnWidthOf(rows, NameWidth, ContentWidth)", "0f", "a panel whose controls do not line up with the pages' own column" },
		{ nameof(ThePagesControlsShareOneColumn), "adapter/OnlineUiRowGeometry.cs", "width: columnWidth > 0f ? columnWidth : -1f,", "width: -1f,", "a row that ignores the page's column and keeps its label flexible" },
		{ nameof(APageThatDoesNotFitSaysSo), "runtime/OnlineUiPageScroll.cs", "if (viewportHeight <= 0f || contentHeight <= viewportHeight + FitsTolerance)", "if (viewportHeight <= 0f)", "a bar drawn over every page, including the ones that fit — a control that can never move" },
		{ nameof(APageThatDoesNotFitSaysSo), "runtime/OnlineUiPageScroll.cs", "var fraction = Clamp(viewportHeight / contentHeight, MinimumFraction, 1f);", "var fraction = 1f;", "a handle that fills the whole track however long the page is, so it cannot say where the player stands" },
		{ nameof(APageThatDoesNotFitSaysSo), "runtime/OnlineUiPageScroll.cs", "private const float MinimumFraction = 0.05f;", "private const float MinimumFraction = 0f;", "a handle of zero height on a very long page — a bar nobody can see or grab" },
		{ nameof(APageThatDoesNotFitSaysSo), "runtime/OnlineUiPageScroll.cs", "return new OnlineUiScrollHandle(fraction, offset);", "return new OnlineUiScrollHandle(fraction, 0f);", "a handle pinned to the top of the track while the page scrolls under it" },
		{ nameof(APageThatDoesNotFitSaysSo), "runtime/OnlineUiPageScroll.cs", "public static float PositionFor(float offset) => Clamp(1f - offset, 0f, 1f);", "public static float PositionFor(float offset) => 1f;", "a click on the track that always asks for the top of the page" },
		{ nameof(APageThatDoesNotFitSaysSo), "adapter/OnlineUiPageScrollbar.cs", "SetShown(needed);", "SetShown(true);", "a bar that is shown whether or not the page overflows" },
		{ nameof(APageThatDoesNotFitSaysSo), "adapter/OnlineUiPageScrollbar.cs", "var height = Mathf.Max(1f, track * handle.Fraction);", "var height = track;", "a handle as tall as the track, which says nothing about how much of the page is shown" },
		{ nameof(APageThatDoesNotFitSaysSo), "adapter/OnlineUiPageScrollbar.cs", "_handle.anchoredPosition = new Vector2(0f, -offset);", "_handle.anchoredPosition = Vector2.zero;", "a handle that never moves as the page is scrolled" },
		{ nameof(APageThatDoesNotFitSaysSo), "adapter/OnlineUiWindowView.cs", "_scrollbar.Apply();", "// the page reports no position", "a window that never applies the bar it built" },
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
		Assert.False(Matcher(pin, file)(broken), $"{pin}: {why}");
	}

	private static Func<string, bool> Matcher(string pin, string file) => pin switch
	{
		nameof(APairedControlCarriesItsNameOnItsOwnLine) => broken => PairedIsTwoLines(
			Is(file, "OnlineUiElementModel") && Is(broken, "record struct OnlineUiElementModel") ? broken : Runtime("OnlineUiElementModel.cs"),
			Is(file, "OnlineUiRowLayout") && Is(broken, "static class OnlineUiRowLayout") ? broken : Runtime("OnlineUiRowLayout.cs"),
			Is(file, "OnlineUiWindowView") ? broken : Adapter("OnlineUiWindowView.cs"),
			Is(file, "OnlineUiRowGeometry") ? broken : Adapter("OnlineUiRowGeometry.cs"),
			Is(file, "OnlineUiPageBuilder") ? broken : Plugin("OnlineUiPageBuilder.cs")),
		nameof(ThePagesControlsShareOneColumn) => broken => TheControlsLineUp(
			Is(file, "OnlineUiWindowRowView") ? broken : Adapter("OnlineUiWindowRowView.cs"),
			Is(file, "OnlineUiControlView") ? broken : Adapter("OnlineUiControlView.cs"),
			Is(file, "OnlineUiWindowView") ? broken : Adapter("OnlineUiWindowView.cs"),
			Is(file, "OnlineUiPanelView") ? broken : Adapter("OnlineUiPanelView.cs"),
			Is(file, "OnlineUiRowGeometry") ? broken : Adapter("OnlineUiRowGeometry.cs")),
		nameof(APageThatDoesNotFitSaysSo) => broken => ThePageCarriesItsBar(
			Is(file, "OnlineUiPageScroll") ? broken : Runtime("OnlineUiPageScroll.cs"),
			Is(file, "OnlineUiPageScrollbar") ? broken : Adapter("OnlineUiPageScrollbar.cs"),
			Is(file, "OnlineUiWindowView") ? broken : Adapter("OnlineUiWindowView.cs")),
		_ => throw new InvalidOperationException($"no matcher is registered for the pin `{pin}`"),
	};

	/// <summary>Whether the mutation's file is the one this argument of the pin stands for.</summary>
	private static bool Is(string file, string name) => file.Contains(name, StringComparison.Ordinal);

	/// <summary>
	/// The two-line shape, from the model that asks for it to the group that stacks it: the factory that
	/// writes a paired row marks its control, the Runtime's wrap turns that mark into a line of its own, and
	/// the adapter places the name and the control in a VERTICAL group rather than side by side.
	/// </summary>
	private static bool PairedIsTwoLines(string modelSource, string layoutSource, string windowSource, string geometrySource, string builderSource)
	{
		var model = Flatten(modelSource);
		var layout = Flatten(layoutSource);
		var window = Flatten(windowSource);
		var geometry = Flatten(geometrySource);
		var builder = Flatten(builderSource);

		return model.Contains("public static OnlineUiRowModel Paired(string name, OnlineUiElementModel control, params OnlineUiElementModel[] beside)", StringComparison.Ordinal)
			&& model.Contains("control with { NewLine = true },", StringComparison.Ordinal)
			&& layout.Contains("public static int[] LineOf(IReadOnlyList<float> widths, IReadOnlyList<bool> newLine, float contentWidth, float spacing = 0f)", StringComparison.Ordinal)
			&& layout.Contains("if (index > 0 && index < newLine.Count && newLine[index] && fitted[index] + shift <= previous)", StringComparison.Ordinal)
			&& layout.Contains("lines[index] = fitted[index] + shift;", StringComparison.Ordinal)
			&& window.Contains("var lines = OnlineUiRowLayout.LineOf(widths, breaks, ContentWidth, LineSpacing);", StringComparison.Ordinal)
			&& window.Contains("breaks[index] = elements[index].NewLine;", StringComparison.Ordinal)
			&& geometry.Contains("internal static void LayOutPairedRow(GameObject root, LayoutElement layout, RectTransform? label, RectTransform? control, float controlWidth, RectTransform? prefabValue = null)", StringComparison.Ordinal)
			&& geometry.Contains("ConfigureGroup<VerticalLayoutGroup>(", StringComparison.Ordinal)
			&& builder.Contains("_rows.Add(OnlineUiElementModel.Paired(", StringComparison.Ordinal);
	}

	/// <summary>
	/// The column: the row view is the one owner of its width and of the pass that writes it, it is measured
	/// from the views' own names (never from a box the layout wrote), the window and the panel both ask for
	/// it, and the row geometry is what gives a label that width.
	/// </summary>
	private static bool TheControlsLineUp(string rowSource, string controlSource, string windowSource, string panelSource, string geometrySource)
	{
		var row = Flatten(rowSource);
		var control = Flatten(controlSource);
		var geometry = Flatten(geometrySource);

		return row.Contains("internal static float ColumnWidthOf(", StringComparison.Ordinal)
			&& row.Contains("private const float ColumnShare = 0.8f;", StringComparison.Ordinal)
			&& row.Contains("widest = Mathf.Max(widest, nameWidth(rowIndex, index, elements[index]));", StringComparison.Ordinal)
			&& row.Contains("internal void AlignToColumn(IReadOnlyList<OnlineUiElementModel> elements, float[] widths, float columnWidth)", StringComparison.Ordinal)
			&& row.Contains("Elements[index].LayOutRow(elements[index], widths[index], columnWidth);", StringComparison.Ordinal)
			&& Flatten(controlSource).Contains("return OnlineUiControlSizing.CaptionTextWidth(label);", StringComparison.Ordinal)
			&& Flatten(windowSource).Contains("OnlineUiWindowRowView.ColumnWidthOf(rows, NameWidth, ContentWidth)", StringComparison.Ordinal)
			&& Flatten(panelSource).Contains("OnlineUiWindowRowView.ColumnWidthOf(rows, NameWidth, ContentWidth)", StringComparison.Ordinal)
			&& geometry.Contains("width: columnWidth > 0f ? columnWidth : -1f,", StringComparison.Ordinal);
	}

	/// <summary>
	/// The page's own bar: the rule says whether one is needed and where its handle stands, the bar draws it
	/// on the page's band (a sibling of the viewport, so the mask cannot clip it), hides itself on a page that
	/// fits, and the window applies it every frame it is up.
	/// </summary>
	private static bool ThePageCarriesItsBar(string scrollSource, string barSource, string windowSource)
	{
		var scroll = Flatten(scrollSource);
		var bar = Flatten(barSource);

		return scroll.Contains("public static OnlineUiScrollHandle For(float viewportHeight, float contentHeight, float position)", StringComparison.Ordinal)
			&& scroll.Contains("if (viewportHeight <= 0f || contentHeight <= viewportHeight + FitsTolerance)", StringComparison.Ordinal)
			&& scroll.Contains("var fraction = Clamp(viewportHeight / contentHeight, MinimumFraction, 1f);", StringComparison.Ordinal)
			&& scroll.Contains("private const float MinimumFraction = 0.05f;", StringComparison.Ordinal)
			&& scroll.Contains("public static float PositionFor(float offset) => Clamp(1f - offset, 0f, 1f);", StringComparison.Ordinal)
			&& scroll.Contains("return new OnlineUiScrollHandle(fraction, offset);", StringComparison.Ordinal)
			&& bar.Contains("internal static OnlineUiPageScrollbar Create(RectTransform page, Sprite? sprite, Image.Type imageType, float pixelsPerUnit)", StringComparison.Ordinal)
			&& bar.Contains("var handle = OnlineUiPageScroll.For(viewport, content, _scroll.verticalNormalizedPosition);", StringComparison.Ordinal)
			&& bar.Contains("SetShown(needed);", StringComparison.Ordinal)
			&& bar.Contains("var height = Mathf.Max(1f, track * handle.Fraction);", StringComparison.Ordinal)
			&& bar.Contains("_handle.anchoredPosition = new Vector2(0f, -offset);", StringComparison.Ordinal)
			&& Flatten(windowSource).Contains("var scrollbar = OnlineUiPageScrollbar.Create(scroll, sprite, imageType, pixelsPerUnit);", StringComparison.Ordinal)
			&& Flatten(windowSource).Contains("_scrollbar.Apply();", StringComparison.Ordinal);
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
			"runtime" => Runtime(name),
			_ => throw new InvalidOperationException($"unknown tree `{tree}` in `{file}`"),
		};
	}

	private static string Plugin(string fileName) => ReadNormalised(Path.Combine(PluginDirectory, fileName));

	private static string Adapter(string fileName) => ReadNormalised(Path.Combine(GameAdapterDirectory, "OnlineUi", fileName));

	private static string Runtime(string fileName) => ReadNormalised(Path.Combine(RuntimeDirectory, "OnlineUi", fileName));

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

	private static string RuntimeDirectory =>
		Path.Combine(FindRepositoryRoot(), "src", "CasualtiesUnknownOnline.Runtime");

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
