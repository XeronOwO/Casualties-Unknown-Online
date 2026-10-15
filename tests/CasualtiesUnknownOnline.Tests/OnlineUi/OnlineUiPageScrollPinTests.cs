using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The page's own bar and the room it takes (ticket online-ui-paired-controls-and-page-scroll, S4, extended by
/// the same ticket's judging pass of 2026-10-15): a page whose content runs past what is shown says so with a
/// track and a handle, a page that fits carries none, and the content keeps the bar's own room clear whether or
/// not the bar is drawn.
///
/// <para>
/// It is a class of its own — split out of <see cref="OnlineUiPairedControlPinTests"/> by the cycle that gave
/// the content its clearance — because it pins one behaviour family: what a page that does not fit SAYS. The
/// user reported the missing bar on 2026-10-14 ("is there a scrollbar? the Admin page has a part that goes past
/// what is shown, and I don't see one on the right") and the overlap it then produced on 2026-10-15 ("the
/// content should keep clear of the scrollbar — that is what normal front-end design does": the value box ended
/// at <c>486.00</c> while the bar's track spanned <c>482.00..490.00</c>).
/// </para>
///
/// <para>
/// Every pin carries a mutation of the REAL source as its negative sample, and the anchors are matched after
/// comment-stripping and whitespace flattening, so indentation, line endings and a comment that merely names an
/// API cannot decide a result. What no pin here can see is a rendering: whether the bar reads as a margin note
/// beside a page of this game is the acceptance run's judgement.
/// </para>
/// </summary>
public sealed class OnlineUiPageScrollPinTests
{
	[Fact]
	public void APageThatDoesNotFitSaysSo() =>
		Assert.True(
			ThePageCarriesItsBar(
				Runtime("OnlineUiPageScroll.cs"),
				Adapter("OnlineUiPageScrollbar.cs"),
				Adapter("OnlineUiWindowView.cs")),
			"a page whose content runs past what is shown carries a bar that says how much more there is and where the player stands, its content keeps that bar's room clear, and a page that FITS keeps none");

	public static TheoryData<string, string, string, string, string> Mutations => new()
	{
		{ nameof(APageThatDoesNotFitSaysSo), "runtime/OnlineUiPageScroll.cs", "if (viewportHeight <= 0f || contentHeight <= viewportHeight + FitsTolerance)", "if (viewportHeight <= 0f)", "a bar drawn over every page, including the ones that fit — a control that can never move" },
		{ nameof(APageThatDoesNotFitSaysSo), "runtime/OnlineUiPageScroll.cs", "var fraction = Clamp(viewportHeight / contentHeight, MinimumFraction, 1f);", "var fraction = 1f;", "a handle that fills the whole track however long the page is, so it cannot say where the player stands" },
		{ nameof(APageThatDoesNotFitSaysSo), "runtime/OnlineUiPageScroll.cs", "private const float MinimumFraction = 0.05f;", "private const float MinimumFraction = 0f;", "a handle of zero height on a very long page — a bar nobody can see or grab" },
		{ nameof(APageThatDoesNotFitSaysSo), "runtime/OnlineUiPageScroll.cs", "return new OnlineUiScrollHandle(fraction, offset);", "return new OnlineUiScrollHandle(fraction, 0f);", "a handle pinned to the top of the track while the page scrolls under it" },
		{ nameof(APageThatDoesNotFitSaysSo), "runtime/OnlineUiPageScroll.cs", "public static float PositionFor(float offset) => Clamp(1f - offset, 0f, 1f);", "public static float PositionFor(float offset) => 1f;", "a click on the track that always asks for the top of the page" },
		{ nameof(APageThatDoesNotFitSaysSo), "runtime/OnlineUiPageScroll.cs", "public const float GutterWidth = TrackInset + TrackWidth;", "public const float GutterWidth = 0f;", "a bar that takes no room of its own, so the page's content is drawn under it — the value box four units under the track the user reported" },
		{ nameof(APageThatDoesNotFitSaysSo), "adapter/OnlineUiPageScrollbar.cs", "private const float Width = OnlineUiPageScroll.TrackWidth;", "private const float Width = 12f;", "a bar drawn wider than the room the page keeps for it, so the content ends up under the track again" },
		{ nameof(APageThatDoesNotFitSaysSo), "adapter/OnlineUiPageScrollbar.cs", "root.offsetMin = new Vector2(-Width - Inset, Inset);", "root.offsetMin = new Vector2(-Width, Inset);", "a bar drawn over part of the room the page's content has already given up — the premise the viewport's inset rests on" },
		{ nameof(APageThatDoesNotFitSaysSo), "adapter/OnlineUiWindowView.cs", "OnlineUiSurfaceParts.Stretch(viewport, 0f, OnlineUiPageScroll.GutterWidth, 0f, 0f);", "OnlineUiSurfaceParts.Stretch(viewport, 0f, 0f, 0f, 0f);", "a viewport taking the bar's own room, so every row of the page is drawn under it" },
		{ nameof(APageThatDoesNotFitSaysSo), "adapter/OnlineUiWindowView.cs", "internal const float ContentWidth = Width - (2f * Padding) - OnlineUiPageScroll.GutterWidth;", "internal const float ContentWidth = Width - (2f * Padding);", "a page wrapped at the width the bar has already taken a slice of, so a row that fits the wrap is drawn under the bar" },
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
		nameof(APageThatDoesNotFitSaysSo) => broken => ThePageCarriesItsBar(
			Is(file, "OnlineUiPageScroll.cs") ? broken : Runtime("OnlineUiPageScroll.cs"),
			Is(file, "OnlineUiPageScrollbar.cs") ? broken : Adapter("OnlineUiPageScrollbar.cs"),
			Is(file, "OnlineUiWindowView.cs") ? broken : Adapter("OnlineUiWindowView.cs")),
		_ => throw new InvalidOperationException($"no matcher is registered for the pin `{pin}`"),
	};

	/// <summary>Whether the mutation's file is the one this argument of the pin stands for. The names carry
	/// their extension: <c>OnlineUiPageScroll</c> is a PREFIX of <c>OnlineUiPageScrollbar</c>, so a bare stem
	/// would feed a broken bar source into the rule's own argument and the mutation would fail for the wrong
	/// file — a negative sample that proves nothing about the clause it names.</summary>
	private static bool Is(string file, string name) => file.EndsWith(name, StringComparison.Ordinal);

	/// <summary>
	/// The page's own bar: the rule says whether one is needed, how much room it takes on the page's band and
	/// where its handle stands, the bar draws it on the band (a sibling of the viewport, so the mask cannot clip
	/// it), hides itself on a page that fits, and the window applies it every frame it is up — while the page's
	/// CONTENT keeps the bar's room clear, whether or not the bar is drawn: a page that took the room only while
	/// its bar was up would wrap narrower the moment it overflowed, and the wrapping that decided the bar was
	/// needed would change with its own answer.
	/// </summary>
	private static bool ThePageCarriesItsBar(string scrollSource, string barSource, string windowSource)
	{
		var scroll = Flatten(scrollSource);
		var bar = Flatten(barSource);
		var window = Flatten(windowSource);

		return scroll.Contains("public static OnlineUiScrollHandle For(float viewportHeight, float contentHeight, float position)", StringComparison.Ordinal)
			&& scroll.Contains("if (viewportHeight <= 0f || contentHeight <= viewportHeight + FitsTolerance)", StringComparison.Ordinal)
			&& scroll.Contains("var fraction = Clamp(viewportHeight / contentHeight, MinimumFraction, 1f);", StringComparison.Ordinal)
			&& scroll.Contains("private const float MinimumFraction = 0.05f;", StringComparison.Ordinal)
			&& scroll.Contains("public static float PositionFor(float offset) => Clamp(1f - offset, 0f, 1f);", StringComparison.Ordinal)
			&& scroll.Contains("return new OnlineUiScrollHandle(fraction, offset);", StringComparison.Ordinal)
			&& scroll.Contains("public const float GutterWidth = TrackInset + TrackWidth;", StringComparison.Ordinal)
			&& bar.Contains("internal static OnlineUiPageScrollbar Create(RectTransform page, Sprite? sprite, Image.Type imageType, float pixelsPerUnit)", StringComparison.Ordinal)
			// The band the bar occupies is the room the content gives up: the bar's own box is the outermost
			// units of the page band, which is the premise the viewport's inset and the wrap's width rest on.
			&& bar.Contains("root.offsetMin = new Vector2(-Width - Inset, Inset);", StringComparison.Ordinal)
			&& bar.Contains("root.offsetMax = new Vector2(-Inset, -Inset);", StringComparison.Ordinal)
			&& bar.Contains("private const float Width = OnlineUiPageScroll.TrackWidth;", StringComparison.Ordinal)
			&& bar.Contains("private const float Inset = OnlineUiPageScroll.TrackInset;", StringComparison.Ordinal)
			&& bar.Contains("var handle = OnlineUiPageScroll.For(viewport, content, _scroll.verticalNormalizedPosition);", StringComparison.Ordinal)
			&& bar.Contains("SetShown(needed);", StringComparison.Ordinal)
			&& bar.Contains("var height = Mathf.Max(1f, track * handle.Fraction);", StringComparison.Ordinal)
			&& bar.Contains("_handle.anchoredPosition = new Vector2(0f, -offset);", StringComparison.Ordinal)
			&& window.Contains("OnlineUiSurfaceParts.Stretch(viewport, 0f, OnlineUiPageScroll.GutterWidth, 0f, 0f);", StringComparison.Ordinal)
			&& window.Contains("internal const float ContentWidth = Width - (2f * Padding) - OnlineUiPageScroll.GutterWidth;", StringComparison.Ordinal)
			&& window.Contains("var scrollbar = OnlineUiPageScrollbar.Create(scroll, sprite, imageType, pixelsPerUnit);", StringComparison.Ordinal)
			&& window.Contains("_scrollbar.Apply();", StringComparison.Ordinal);
	}

	private static string Read(string file)
	{
		var separator = file.IndexOf('/');
		Assert.True(separator > 0, $"the mutation's file `{file}` must be written as `<tree>/<name>.cs`");
		var tree = file.Substring(0, separator);
		var name = file.Substring(separator + 1);
		return tree switch
		{
			"runtime" => Runtime(name),
			"adapter" => Adapter(name),
			_ => throw new InvalidOperationException($"unknown tree `{tree}` in `{file}`"),
		};
	}

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
