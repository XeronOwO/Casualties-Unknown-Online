using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The popup layer's own behaviour family (ticket online-ui-layout-and-input-detail-pass, row 4): the window
/// owns a layer, every dropdown hands its template to it, the box pass leaves the game's template alone, and
/// the layer puts the template — and the open list — on the field it belongs to, on the side the canvas has
/// room for, closing a second list and destroying a template whose row was rebuilt.
///
/// <para>
/// This family was split out of <see cref="OnlineUiLayoutDetailPinTests"/> when its own mutation rows took that
/// class past the 40-case cap: one pin class is one behaviour family, and the cap is met by the split the gate
/// asks for rather than by a raised limit.
/// </para>
/// </summary>
public sealed class OnlineUiPopupLayerPinTests
{
	[Fact]
	public void AnOpenDropdownListRidesTheWindowsPopupLayer() =>
		Assert.True(
			ThePopupLayerCarriesTheLists(
				Adapter("OnlineUiWindowView.cs"),
				Adapter("OnlineUiDropdownPopup.cs"),
				Adapter("OnlineUiControlView.cs"),
				Adapter("OnlineUiBoxGeometry.cs")),
			"an open dropdown's options are the user's report: a list built inside the page is clipped by the page's mask and drawn under the rows after it, so the templates must live on the window's popup layer, sorted above CUO's own canvas — and the layer must put them on the field they belong to, on the side the canvas has room for");

	public static TheoryData<string, string, string, string, string> Mutations => new()
	{
		{ nameof(AnOpenDropdownListRidesTheWindowsPopupLayer), "adapter/OnlineUiWindowView.cs", "var popup = OnlineUiDropdownPopup.Create(rect, OnlineUiSurfaceHost.SortingOrder, log);", "var popup = null!;", "a window with no popup layer for its dropdowns" },
		{ nameof(AnOpenDropdownListRidesTheWindowsPopupLayer), "adapter/OnlineUiDropdownPopup.cs", "template.SetParent(_layer, worldPositionStays: false);", "// the template stays inside the page", "a list instantiated inside the page's mask again" },
		{ nameof(AnOpenDropdownListRidesTheWindowsPopupLayer), "adapter/OnlineUiDropdownPopup.cs", "rect.pivot = new Vector2(0.5f, opensAbove ? 0f : 1f);", "rect.pivot = new Vector2(0.5f, 0.5f);", "a list centred on the field's edge instead of hanging under or over it" },
		{ nameof(AnOpenDropdownListRidesTheWindowsPopupLayer), "adapter/OnlineUiDropdownPopup.cs", "rect.sizeDelta = new Vector2(width, height);", "rect.sizeDelta = new Vector2(width, 0f);", "a list with no height (the batch's negative rect was its degenerate form)" },
		{ nameof(AnOpenDropdownListRidesTheWindowsPopupLayer), "adapter/OnlineUiDropdownPopup.cs", "adopted.Dropdown.Hide();", "// the second list is left open", "a second field's list left open, so the layer's single-list assumption breaks" },
		{ nameof(AnOpenDropdownListRidesTheWindowsPopupLayer), "adapter/OnlineUiDropdownPopup.cs", "_log.LogDebug(\"Online UI window: the popup layer dropped the template of a dropdown whose row was rebuilt.\");", "// the prune is silent", "a template dropped with no log line, so the layer's one low-frequency cleanup is unobservable" },
		{ nameof(AnOpenDropdownListRidesTheWindowsPopupLayer), "adapter/OnlineUiBoxGeometry.cs", "if (control.GetChild(index) is not RectTransform child || child == template)", "if (control.GetChild(index) is not RectTransform child)", "the box pass stretching the game's template again, which is what made the list negative" },
		{ nameof(AnOpenDropdownListRidesTheWindowsPopupLayer), "adapter/OnlineUiBoxGeometry.cs", "part != template && IsContainer(part)", "IsContainer(part)", "the caption's room counting the template's authored width, which the box pass no longer writes" },
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
		nameof(AnOpenDropdownListRidesTheWindowsPopupLayer) => broken => ThePopupLayerCarriesTheLists(
			Is(broken, "internal sealed class OnlineUiWindowView") ? broken : Adapter("OnlineUiWindowView.cs"),
			Is(broken, "internal sealed class OnlineUiDropdownPopup") ? broken : Adapter("OnlineUiDropdownPopup.cs"),
			Adapter("OnlineUiControlView.cs"),
			Is(broken, "internal static class OnlineUiBoxGeometry") ? broken : Adapter("OnlineUiBoxGeometry.cs")),
		_ => throw new InvalidOperationException($"no matcher is registered for the pin `{pin}`"),
	};

	/// <summary>Whether a source is the file a mutation row says it is — the routing a pin with more than one
	/// file needs, since only one side of it is the broken one.</summary>
	private static bool Is(string source, string declaration) => source.Contains(declaration, StringComparison.Ordinal);

	/// <summary>The window owns a popup layer, every dropdown hands its template to it, the layer sorts above
	/// CUO's own canvas and normalises its list every frame the window is applied; the box pass leaves the
	/// game's template alone (in BOTH loops), and the layer owns the template's rect: it captures the authored
	/// maximum, puts the open list on the field's bottom or top edge as the canvas's room decides, closes a
	/// second list and reports a template it had to drop.</summary>
	private static bool ThePopupLayerCarriesTheLists(string windowSource, string popupSource, string controlSource, string boxSource)
	{
		var window = Flatten(windowSource);
		var popup = Flatten(popupSource);
		var box = Flatten(boxSource);

		return window.Contains("var popup = OnlineUiDropdownPopup.Create(rect, OnlineUiSurfaceHost.SortingOrder, log);", StringComparison.Ordinal)
			&& window.Contains("view.AdoptPopup(_popup);", StringComparison.Ordinal)
			&& window.Contains("_popup.Normalise();", StringComparison.Ordinal)
			&& popup.Contains("canvas.overrideSorting = true;", StringComparison.Ordinal)
			&& popup.Contains("canvas.sortingOrder = surfaceSortingOrder + 1;", StringComparison.Ordinal)
			&& popup.Contains("template.SetParent(_layer, worldPositionStays: false);", StringComparison.Ordinal)
			&& popup.Contains("var maxHeight = template.rect.height > 0f ? template.rect.height : _layer.rect.height;", StringComparison.Ordinal)
			&& popup.Contains("OnlineUiDropdownPlacement.SideFor(", StringComparison.Ordinal)
			&& popup.Contains("rect.pivot = new Vector2(0.5f, opensAbove ? 0f : 1f);", StringComparison.Ordinal)
			&& popup.Contains("rect.sizeDelta = new Vector2(width, height);", StringComparison.Ordinal)
			&& popup.Contains("_layer.InverseTransformPoint(_corners[0])", StringComparison.Ordinal)
			&& popup.Contains("adopted.Dropdown.Hide();", StringComparison.Ordinal)
			&& popup.Contains("_log.LogDebug(\"Online UI window: the popup layer dropped the template of a dropdown whose row was rebuilt.\");", StringComparison.Ordinal)
			&& box.Contains("var template = control.GetComponent<TMP_Dropdown>()?.template;", StringComparison.Ordinal)
			&& box.Contains("part != template && IsContainer(part)", StringComparison.Ordinal)
			&& box.Contains("is not RectTransform child || child == template", StringComparison.Ordinal)
			&& Flatten(controlSource).Contains("popup.Adopt(_dropdown);", StringComparison.Ordinal);
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
