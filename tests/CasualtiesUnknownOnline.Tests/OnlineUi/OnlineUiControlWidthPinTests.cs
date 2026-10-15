using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The width ONE element of a page is given (ticket online-ui-layout-and-input-detail-pass, the cycle of
/// 2026-10-14): the rule lives with every other width of the surface, a toggle and a slider take the size the
/// game authored for the CONTROL itself, an element that IS its own control takes the width its model declares,
/// and the window's wrap is asked with exactly those — so the line a control lands on and the width it is given
/// cannot disagree.
///
/// <para>
/// The window's TAB STRIP is the same family one level up: a tab is an element whose width is its caption, the
/// strip is a band of a fixed width, and six English captions need more than it has. <see cref="ATabStripFitsItsOwnBand"/>
/// is where that half lives — a strip that hands every tab what it asked for is what ran the last tab past the
/// window's right edge (batch `20261015-a`).
/// </para>
///
/// <para>
/// It is a class of its own because the family is one: every pin here answers "how wide is this element", while
/// <see cref="OnlineUiLayoutDetailPinTests"/> answers where a page's parts sit and how they read. Both carry a
/// mutation of the REAL source as each pin's negative sample, and both flatten a source before matching it, so
/// indentation, line endings and a comment that merely names an API cannot decide a result.
/// </para>
/// </summary>
public sealed class OnlineUiControlWidthPinTests
{
	[Fact]
	public void AControlTakesWhateverItsOwnContentNeeds() =>
		Assert.True(
			WidthsFollowTheContent(
				Adapter("OnlineUiControlView.cs"),
				Adapter("OnlineUiControlSizing.cs"),
				Adapter("OnlineUiWindowView.cs")),
			"the model's width is a floor, not a ceiling: the caption measured at the game's own font decides the width and the wrap is asked with what the control actually takes, or an English label is cut inside a box sized for Chinese");

	[Fact]
	public void AControlOfARowIsAsWideAsTheGameAuthoredIt() =>
		Assert.True(
			TheControlTakesTheGamesAuthoredWidth(Adapter("OnlineUiControlView.cs"), Adapter("OnlineUiControlSizing.cs")),
			"a row's control takes the width the GAME authored for that control, read off the control itself when the view is built: the element root's own preferred width CONTAINS the control's declared width (the row's group sums its children), so asking the root is asking the box how wide it is — the Admin page's rule rows grew 7282 → 30537 units inside a 984-unit page while their flexible label, whose minimum is zero, was squeezed to one character per line (batch `20261015-a`)");

	[Fact]
	public void AnElementThatIsItsOwnControlDeclaresItsWidth() =>
		Assert.True(
			TheOwnControlKeepsItsDeclaredWidth(Adapter("OnlineUiRowGeometry.cs")),
			"a tab, a page button and a colour block ARE one of the game's rows, so no control is laid out inside them and the width their model declared is written on the element itself: without that write a colour block is as wide as its own caption group's padding, which is the rejected row of batch `20261014-a` (88 declared, 18 rendered)");

	[Fact]
	public void ATabStripFitsItsOwnBand() =>
		Assert.True(
			TheStripSharesItsOwnWidth(
				Adapter("OnlineUiWindowView.cs"),
				Adapter("OnlineUiControlSizing.cs"),
				Plugin("OnlineUiPageBuilder.cs")),
			"the tab strip is a band of a fixed width and a tab is as wide as its caption, so the widths the tabs take are the Runtime rule's answer over the least each caption can be drawn at, budgeted from the WINDOW's own inner width — a budget read off the band equals what the tabs already asked for, because the band is as wide as its children's minimums, and then nothing is ever given up and the strip runs past the window again; handing every tab what it asked for is what measured six English tabs at 1038.7 units against a 984-unit page (batch `20261015-a`)");

	public static TheoryData<string, string, string, string, string> Mutations => new()
	{
		{ nameof(AControlTakesWhateverItsOwnContentNeeds), "adapter/OnlineUiControlSizing.cs", "OnlineUiControlBox.CaptionedWidth(element.Width, CaptionTextWidthOrZero(caption), 2f * typographySize)", "element.Width;", "a button sized by the model's hint alone, which is what cut the English captions and ran `Preferences` into its border" },
		{ nameof(AControlTakesWhateverItsOwnContentNeeds), "adapter/OnlineUiControlSizing.cs", "internal static float PrefabWidth(RectTransform rect)", "internal static float PrefabWidth(string unused)", "a prefab width nobody can read" },
		{ nameof(AControlTakesWhateverItsOwnContentNeeds), "adapter/OnlineUiWindowView.cs", "widths[index] = view.EffectiveWidth(elements[index]);", "widths[index] = elements[index].Width;", "a wrap asked with the model's hints instead of the declared widths" },
		{ nameof(AControlOfARowIsAsWideAsTheGameAuthoredIt), "adapter/OnlineUiControlView.cs", "OnlineUiControlSizing.AuthoredChildWidth(controlRect)", "OnlineUiControlSizing.PrefabWidth(_rect)", "a control sized from the element root that contains it — the width the row's own group just wrote" },
		{ nameof(AControlOfARowIsAsWideAsTheGameAuthoredIt), "adapter/OnlineUiControlSizing.cs", "internal static float AuthoredChildWidth(RectTransform? child)", "internal static float AuthoredChildWidth(string unused)", "a prefab whose authored child width nobody can read" },
		{ nameof(AControlOfARowIsAsWideAsTheGameAuthoredIt), "adapter/OnlineUiControlSizing.cs", "OnlineUiControlBox.EffectiveWidth(element.Width, authoredControlWidth)", "OnlineUiControlBox.EffectiveWidth(element.Width, 0f)", "a toggle left at the model's floor with the size the game authored for it ignored" },
		{ nameof(AnElementThatIsItsOwnControlDeclaresItsWidth), "adapter/OnlineUiRowGeometry.cs", "layout.preferredWidth = controlWidth;", "// the element keeps the width its own group measured,", "a colour block as wide as its own padding — 88 declared, 18 rendered" },
		{ nameof(ATabStripFitsItsOwnBand), "adapter/OnlineUiWindowView.cs", "OnlineUiTabLayout.Widths(natural, minimum, TabStripWidth, LineSpacing)", "natural", "a strip that gives every tab the width it asked for, which is the 1038.7 units of English tabs the user found inside a 984-unit page" },
		{ nameof(ATabStripFitsItsOwnBand), "adapter/OnlineUiWindowView.cs", "minimum[index] = _tabs[index].MinimumWidth(tabs[index]);", "minimum[index] = 0f;", "a strip with no floor under any tab, so the excess comes off the captions themselves" },
		{ nameof(ATabStripFitsItsOwnBand), "adapter/OnlineUiControlSizing.cs", "OnlineUiControlBox.CaptionedWidth(element.Width, CaptionTextWidthOrZero(caption), 2f * typographySize)", "OnlineUiControlBox.CaptionedWidth(element.Width, CaptionTextWidthOrZero(caption), 4f * typographySize)", "the room around a caption counted twice, which measured the English `Preferences` tab at 231.2 units and is what pushed the strip past the window" },
		{ nameof(ATabStripFitsItsOwnBand), "adapter/OnlineUiWindowView.cs", "_rect.rect.width > 0f ? _rect.rect.width - (2f * Padding) : ContentWidth", "_tabRow.rect.width > 0f ? _tabRow.rect.width : ContentWidth", "a strip budgeted from the band the tabs THEMSELVES size — the band is as wide as its children's minimums, each of which is the width the rule just wrote, so the budget equals what was asked for, nothing is ever given up and the rule never fires (the live reading is `Tabs rect=1038.7x36.0` against `Title Bar rect=984.0x28.0` on the same shell, batch `20261015-a`)" },
		{ nameof(ATabStripFitsItsOwnBand), "adapter/OnlineUiControlSizing.cs", "private const float MinimumCaptionRoom = 2f * OnlineUiWindowLayout.ControlInnerPadding;", "private const float MinimumCaptionRoom = 1f;", "a tab's floor with no room left for its caption, which is the caption cut the floor exists to prevent" },
		{ nameof(ATabStripFitsItsOwnBand), "plugin/OnlineUiPageBuilder.cs", "internal const float TabWidth = 112f;", "internal const float TabWidth = 130f;", "a tab floor a strip is no longer sized against: `OnlineUiTabLayoutTests` repeats this constant, because the plugin is not referenced by the suite" },
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
		nameof(AControlTakesWhateverItsOwnContentNeeds) => broken => WidthsFollowTheContent(
			Is(broken, "internal sealed class OnlineUiControlView") ? broken : Adapter("OnlineUiControlView.cs"),
			Is(broken, "internal static class OnlineUiControlSizing") ? broken : Adapter("OnlineUiControlSizing.cs"),
			Is(broken, "internal sealed class OnlineUiWindowView") ? broken : Adapter("OnlineUiWindowView.cs")),
		nameof(AControlOfARowIsAsWideAsTheGameAuthoredIt) => broken => TheControlTakesTheGamesAuthoredWidth(
			Is(broken, "internal sealed class OnlineUiControlView") ? broken : Adapter("OnlineUiControlView.cs"),
			Is(broken, "internal static class OnlineUiControlSizing") ? broken : Adapter("OnlineUiControlSizing.cs")),
		nameof(AnElementThatIsItsOwnControlDeclaresItsWidth) => TheOwnControlKeepsItsDeclaredWidth,
		nameof(ATabStripFitsItsOwnBand) => broken => TheStripSharesItsOwnWidth(
			Is(broken, "internal sealed class OnlineUiWindowView") ? broken : Adapter("OnlineUiWindowView.cs"),
			Is(broken, "internal static class OnlineUiControlSizing") ? broken : Adapter("OnlineUiControlSizing.cs"),
			Is(broken, "internal sealed class OnlineUiPageBuilder") ? broken : Plugin("OnlineUiPageBuilder.cs")),
		_ => throw new InvalidOperationException($"no matcher is registered for the pin `{pin}`"),
	};

	/// <summary>Whether a source is the file a mutation row says it is — the routing a pin with more than one
	/// file needs, since only one side of it is the broken one.</summary>
	private static bool Is(string source, string declaration) => source.Contains(declaration, StringComparison.Ordinal);

	/// <summary>The width a control takes is DECLARED — the model's hint under the caption's own measured text —
	/// and the window's wrap is asked with exactly that, so nothing measures content that stretches inside the
	/// box it was just given. The rule lives with every other width of this surface.</summary>
	private static bool WidthsFollowTheContent(string controlSource, string sizingSource, string windowSource)
	{
		var flat = Flatten(controlSource);
		var sizing = Flatten(sizingSource);

		return flat.Contains("internal float EffectiveWidth(OnlineUiElementModel element) =>", StringComparison.Ordinal)
			&& flat.Contains("OnlineUiControlSizing.EffectiveWidth(element, _caption, _typography.Size, _controlAuthoredWidth);", StringComparison.Ordinal)
			&& !flat.Contains("LayoutUtility.GetPreferredSize(_rect", StringComparison.Ordinal)
			&& !flat.Contains("PrefabWidth(_rect", StringComparison.Ordinal)
			&& sizing.Contains("internal static float EffectiveWidth(", StringComparison.Ordinal)
			&& sizing.Contains("float authoredControlWidth) => element.Kind switch", StringComparison.Ordinal)
			&& sizing.Contains("OnlineUiControlBox.CaptionedWidth(element.Width, CaptionTextWidthOrZero(caption), 2f * typographySize)", StringComparison.Ordinal)
			&& sizing.Contains("OnlineUiControlBox.EffectiveWidth(WithMinimum(element.Width), 0f)", StringComparison.Ordinal)
			&& sizing.Contains("internal static float PrefabWidth(RectTransform rect)", StringComparison.Ordinal)
			&& sizing.Contains("internal static float CaptionWidth(TMP_Text caption)", StringComparison.Ordinal)
			&& Flatten(windowSource).Contains("widths[index] = view.EffectiveWidth(elements[index]);", StringComparison.Ordinal);
	}

	/// <summary>
	/// The tab strip shares ONE band of a fixed width: every tab asks for its caption plus the room a row keeps
	/// around it, the strip asks the Runtime rule what each one actually takes, and a tab's floor is the least
	/// its caption can be drawn at rather than nothing at all. Three things are pinned, because the defect needed
	/// all three — a strip that hands out what was asked for, a caption whose room is counted twice (each of them
	/// put the English strip past the window on its own), and a budget that cannot be moved by the tabs it is
	/// shared among: the band's own rect is as wide as its children's minimums, so reading it there makes the
	/// budget equal to the request and the rule never fires. The model's own floor is the fourth: the strip is
	/// sized against the width a tab's model declares, which is what <c>OnlineUiTabLayoutTests</c> repeats as its
	/// own constant, because the plugin is not referenced by this suite.
	/// </summary>
	private static bool TheStripSharesItsOwnWidth(string windowSource, string sizingSource, string pluginSource)
	{
		var flat = Flatten(windowSource);
		var sizing = Flatten(sizingSource);

		return flat.Contains("OnlineUiTabLayout.Widths(natural, minimum, TabStripWidth, LineSpacing)", StringComparison.Ordinal)
			&& flat.Contains("natural[index] = _tabs[index].EffectiveWidth(tabs[index]);", StringComparison.Ordinal)
			&& flat.Contains("minimum[index] = _tabs[index].MinimumWidth(tabs[index]);", StringComparison.Ordinal)
			&& flat.Contains("_tabs[index].LayOutRow(widths[index]);", StringComparison.Ordinal)
			&& flat.Contains("private float TabStripWidth => _rect.rect.width > 0f ? _rect.rect.width - (2f * Padding) : ContentWidth;", StringComparison.Ordinal)
			&& !flat.Contains("_tabRow.rect.width", StringComparison.Ordinal)
			&& Flatten(pluginSource).Contains("internal const float TabWidth = 112f;", StringComparison.Ordinal)
			&& sizing.Contains("private const float MinimumCaptionRoom = 2f * OnlineUiWindowLayout.ControlInnerPadding;", StringComparison.Ordinal)
			&& sizing.Contains("OnlineUiControlBox.CaptionedWidth(element.Width, CaptionTextWidthOrZero(caption), 2f * typographySize)", StringComparison.Ordinal)
			&& sizing.Contains("OnlineUiControlBox.CaptionedWidth(element.Width, CaptionTextWidthOrZero(caption), MinimumCaptionRoom)", StringComparison.Ordinal);
	}

	/// <summary>
	/// The width one of a row's controls takes is the width the GAME authored for that control, read off the
	/// control (never off the element root that contains it) at the moment the view is built — the one moment
	/// the prefab's own rect is still intact, since the row's layout group writes it from the first pass on.
	/// </summary>
	private static bool TheControlTakesTheGamesAuthoredWidth(string controlSource, string sizingSource)
	{
		var flat = Flatten(controlSource);
		var sizing = Flatten(sizingSource);

		return flat.Contains("OnlineUiControlSizing.AuthoredChildWidth(controlRect)", StringComparison.Ordinal)
			&& flat.Contains("private readonly float _controlAuthoredWidth;", StringComparison.Ordinal)
			&& flat.Contains("OnlineUiControlSizing.EffectiveWidth(element, _caption, _typography.Size, _controlAuthoredWidth);", StringComparison.Ordinal)
			&& !flat.Contains("PrefabWidth(_rect)", StringComparison.Ordinal)
			&& sizing.Contains("OnlineUiControlBox.EffectiveWidth(element.Width, authoredControlWidth)", StringComparison.Ordinal)
			&& sizing.Contains("internal static float AuthoredChildWidth(RectTransform? child)", StringComparison.Ordinal);
	}

	/// <summary>An element that IS its own control — a tab, a page button, a colour block — carries the width
	/// its model declared: nothing lays a control out inside it, so the write is what makes the model's floor
	/// true for the wrap AND for the line the element lands on.</summary>
	private static bool TheOwnControlKeepsItsDeclaredWidth(string geometrySource)
	{
		var flat = Flatten(geometrySource);

		return flat.Contains("internal static void LayOutRow(", StringComparison.Ordinal)
			&& flat.Contains("if (controlWidth > 0f)", StringComparison.Ordinal)
			&& flat.Contains("layout.minWidth = controlWidth;", StringComparison.Ordinal)
			&& flat.Contains("layout.preferredWidth = controlWidth;", StringComparison.Ordinal);
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
