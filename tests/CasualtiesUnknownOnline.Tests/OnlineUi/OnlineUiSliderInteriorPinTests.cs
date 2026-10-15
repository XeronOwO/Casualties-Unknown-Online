using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The inside of one slider of a page (ticket online-ui-paired-controls-and-page-scroll, S1, corrected by the
/// same ticket's judging pass of 2026-10-15): the game's own track, the fraction of it the value fills and the
/// handle the player drags are KEPT where the game put them and take the page's own colours, and the one layer
/// CUO re-cuts is the box that knob slides in.
///
/// <para>
/// It is a class of its own — split out of <see cref="OnlineUiLayoutDetailPinTests"/> by the cycle that re-cut
/// the slider — because it pins one behaviour family: what a slider's inside IS. That family has two failures
/// behind it, one per direction. Hiding the parts (<c>HidePrefabBoxArt</c> over a slider's children) left the
/// control with nothing to read — the user's report of 2026-10-14, "the slider's middle has no thin line, so
/// you cannot see where its ends are" — and re-placing them onto the compact box is the report of 2026-10-15,
/// "the yellow rectangle in front and the background's full rectangle do not overlap exactly": the acceptance
/// reading of batch `20261015-d` is a fill <c>551.99x36.00</c> stretched over the whole control inside a rail
/// the prefab authored <c>571.99x28.00</c>, with the knob's own rect placed as if it were that rail
/// (<c>targetGraphic</c> carries the handle's own <c>whitesquare</c> in this prefab).
/// </para>
///
/// <para>
/// Every pin carries a mutation of the REAL source as its negative sample, and the anchors are matched after
/// comment-stripping and whitespace flattening, so indentation, line endings and a comment that merely names an
/// API cannot decide a result. What no pin here can see is a rendering: whether one rail with the fraction over
/// it and a knob the row's height reads as a slider on screen is the acceptance run's judgement.
/// </para>
/// </summary>
public sealed class OnlineUiSliderInteriorPinTests
{
	[Fact]
	public void TheSlidersInteriorIsTheGamesOwn() =>
		Assert.True(
			TheSliderKeepsTheGamesInterior(
				Adapter("OnlineUiRowGeometry.cs"),
				Adapter("OnlineUiSliderGeometry.cs")),
			"a slider's children ARE its reading, so they are kept and tinted rather than hidden — and they are not re-cut onto the compact box either: the game's own rail and filled fraction stay where the prefab put them, and the one layer CUO re-cuts is the box that knob slides in (the user's two reports of 2026-10-14 and 2026-10-15)");

	public static TheoryData<string, string, string, string, string> Mutations => new()
	{
		{ nameof(TheSlidersInteriorIsTheGamesOwn), "adapter/OnlineUiRowGeometry.cs", "OnlineUiSliderGeometry.LayOut(slider, prefabValue);", "OnlineUiBoxGeometry.LayOutInterior(control, controlWidth);", "a slider sent down the box rule, which hides the track, the fill and the handle it is read by" },
		{ nameof(TheSlidersInteriorIsTheGamesOwn), "adapter/OnlineUiSliderGeometry.cs", "PlaceKnobTravel(slider);", "PlaceContainer(slider.fillRect, control); PlaceKnobTravel(slider);", "a slider whose filled fraction is stretched onto the whole control again — the 36-unit-tall rectangle over a 28-unit-tall rail the user rejected" },
		{ nameof(TheSlidersInteriorIsTheGamesOwn), "adapter/OnlineUiSliderGeometry.cs", "PlaceKnobTravel(slider);", "OnlineUiBoxGeometry.HidePrefabBoxArt(slider.fillRect); PlaceKnobTravel(slider);", "a slider whose own art is hidden again — the invisible track the user reported" },
		{ nameof(TheSlidersInteriorIsTheGamesOwn), "adapter/OnlineUiSliderGeometry.cs", "PlaceKnobTravel(slider);", "// the knob's travel is whatever the game's own row authored", "a knob left in the travel box the game's own row resolves to, whose height this surface then cannot say" },
		{ nameof(TheSlidersInteriorIsTheGamesOwn), "adapter/OnlineUiSliderGeometry.cs", "container.offsetMin = left;", "container.offsetMin = Vector2.zero;", "a knob whose travel reaches the control's own ends, so half of it hangs off the rail at either end of the range" },
		{ nameof(TheSlidersInteriorIsTheGamesOwn), "adapter/OnlineUiSliderGeometry.cs", "handle.sizeDelta = new Vector2(KnobWidth, 0f);", "handle.sizeDelta = new Vector2(KnobWidth, handle.sizeDelta.y);", "a knob keeping the height the prefab adds to its travel box, so it is drawn past the compact row it lives in" },
		{ nameof(TheSlidersInteriorIsTheGamesOwn), "adapter/OnlineUiSliderGeometry.cs", "Tint(slider);", "// the slider keeps whatever colours the prefab carried", "a slider drawn in the prefab's own colours instead of the page's" },
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
		nameof(TheSlidersInteriorIsTheGamesOwn) => broken => TheSliderKeepsTheGamesInterior(
			Is(file, "OnlineUiRowGeometry") ? broken : Adapter("OnlineUiRowGeometry.cs"),
			Is(file, "OnlineUiSliderGeometry") ? broken : Adapter("OnlineUiSliderGeometry.cs")),
		_ => throw new InvalidOperationException($"no matcher is registered for the pin `{pin}`"),
	};

	/// <summary>Whether the mutation's file is the one this argument of the pin stands for.</summary>
	private static bool Is(string file, string name) => file.Contains(name, StringComparison.Ordinal);

	/// <summary>
	/// The slider's inside, end to end: the row's own geometry hands a slider to its own rule instead of the
	/// box rule, and that rule keeps the game's own parts, tints the two the page draws, and re-cuts only the
	/// box the engine slides the knob in — never the parts themselves, which is what the first cut did.
	/// </summary>
	private static bool TheSliderKeepsTheGamesInterior(string geometrySource, string sliderSource)
	{
		var geometry = Flatten(geometrySource);
		var slider = Flatten(sliderSource);

		return geometry.Contains("if (control.GetComponent<Slider>() is { } slider)", StringComparison.Ordinal)
			&& geometry.Contains("OnlineUiSliderGeometry.LayOut(slider, prefabValue);", StringComparison.Ordinal)
			&& slider.Contains("internal static void LayOut(Slider slider, RectTransform? prefabValue)", StringComparison.Ordinal)
			&& slider.Contains("private static RectTransform? HandleContainerOf(Slider slider) =>", StringComparison.Ordinal)
			&& slider.Contains("PlaceKnobTravel(slider);", StringComparison.Ordinal)
			&& slider.Contains("Tint(slider);", StringComparison.Ordinal)
			// The knob's travel is the ONE layer re-cut for the compact row, and it keeps half a knob clear at
			// each end so the knob cannot hang off the rail at either end of the range.
			&& slider.Contains("var room = KnobWidth * 0.5f;", StringComparison.Ordinal)
			&& slider.Contains("container.offsetMin = left;", StringComparison.Ordinal)
			&& slider.Contains("container.offsetMax = right;", StringComparison.Ordinal)
			// The knob is a square the height of the row, and its place stays the engine's: the height comes
			// from the travel box, and the size is written as sizeDelta because SetSizeWithCurrentAnchors
			// re-places a point-anchored rect.
			&& slider.Contains("if (handle is not null && handle.sizeDelta != new Vector2(KnobWidth, 0f))", StringComparison.Ordinal)
			&& slider.Contains("handle.sizeDelta = new Vector2(KnobWidth, 0f);", StringComparison.Ordinal)
			&& slider.Contains("private static void CentreTheKnob(RectTransform? handle)", StringComparison.Ordinal)
			// No part is re-parented onto the control, and none of them is hidden: those are the two defects
			// this family exists to reject.
			&& !slider.Contains("PlaceContainer(", StringComparison.Ordinal)
			&& !slider.Contains("HidePrefabBoxArt", StringComparison.Ordinal)
			&& !slider.Contains("SetSizeWithCurrentAnchors", StringComparison.Ordinal);
	}

	private static string Read(string file)
	{
		var separator = file.IndexOf('/');
		Assert.True(separator > 0, $"the mutation's file `{file}` must be written as `<tree>/<name>.cs`");
		var tree = file.Substring(0, separator);
		var name = file.Substring(separator + 1);
		return tree switch
		{
			"adapter" => ReadNormalised(Path.Combine(GameAdapterDirectory, "OnlineUi", name)),
			_ => throw new InvalidOperationException($"unknown tree `{tree}` in `{file}`"),
		};
	}

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
