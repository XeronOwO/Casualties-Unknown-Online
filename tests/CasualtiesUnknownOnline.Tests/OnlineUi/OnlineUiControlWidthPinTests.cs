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

	public static TheoryData<string, string, string, string, string> Mutations => new()
	{
		{ nameof(AControlTakesWhateverItsOwnContentNeeds), "adapter/OnlineUiControlSizing.cs", "OnlineUiControlBox.EffectiveWidth(element.Width, CaptionWidthOrZero(caption) + (2f * typographySize))", "element.Width;", "a button sized by the model's hint alone, which is what cut the English captions and ran `Preferences` into its border" },
		{ nameof(AControlTakesWhateverItsOwnContentNeeds), "adapter/OnlineUiControlSizing.cs", "internal static float PrefabWidth(RectTransform rect)", "internal static float PrefabWidth(string unused)", "a prefab width nobody can read" },
		{ nameof(AControlTakesWhateverItsOwnContentNeeds), "adapter/OnlineUiWindowView.cs", "widths[index] = view.EffectiveWidth(elements[index]);", "widths[index] = elements[index].Width;", "a wrap asked with the model's hints instead of the declared widths" },
		{ nameof(AControlTakesWhateverItsOwnContentNeeds), "adapter/OnlineUiWindowView.cs", "view.LayOutRow(view.EffectiveWidth(element));", "// the tab keeps the width its own group measured,", "a tab strip whose declared floor is never read, which is the disagreement the cycle's own review found" },
		{ nameof(AControlOfARowIsAsWideAsTheGameAuthoredIt), "adapter/OnlineUiControlView.cs", "OnlineUiControlSizing.AuthoredChildWidth(controlRect)", "OnlineUiControlSizing.PrefabWidth(_rect)", "a control sized from the element root that contains it — the width the row's own group just wrote" },
		{ nameof(AControlOfARowIsAsWideAsTheGameAuthoredIt), "adapter/OnlineUiControlSizing.cs", "internal static float AuthoredChildWidth(RectTransform? child)", "internal static float AuthoredChildWidth(string unused)", "a prefab whose authored child width nobody can read" },
		{ nameof(AControlOfARowIsAsWideAsTheGameAuthoredIt), "adapter/OnlineUiControlSizing.cs", "OnlineUiControlBox.EffectiveWidth(element.Width, authoredControlWidth)", "OnlineUiControlBox.EffectiveWidth(element.Width, 0f)", "a toggle left at the model's floor with the size the game authored for it ignored" },
		{ nameof(AnElementThatIsItsOwnControlDeclaresItsWidth), "adapter/OnlineUiRowGeometry.cs", "layout.preferredWidth = controlWidth;", "// the element keeps the width its own group measured,", "a colour block as wide as its own padding — 88 declared, 18 rendered" },
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
			&& sizing.Contains("OnlineUiControlBox.EffectiveWidth(element.Width, CaptionWidthOrZero(caption) + (2f * typographySize))", StringComparison.Ordinal)
			&& sizing.Contains("OnlineUiControlBox.EffectiveWidth(WithMinimum(element.Width), 0f)", StringComparison.Ordinal)
			&& sizing.Contains("internal static float PrefabWidth(RectTransform rect)", StringComparison.Ordinal)
			&& sizing.Contains("internal static float CaptionWidth(TMP_Text caption)", StringComparison.Ordinal)
			&& Flatten(windowSource).Contains("widths[index] = view.EffectiveWidth(elements[index]);", StringComparison.Ordinal)
			&& Flatten(windowSource).Contains("view.LayOutRow(view.EffectiveWidth(element));", StringComparison.Ordinal);
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
