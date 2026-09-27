using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The free player colour's picker (ticket online-ui-art-and-controls-overhaul, S3): the hex field, the
/// palette of colour blocks and the live swatch, plus the two halves of the contract they rest on — the
/// stored preference is the colour itself rather than an index, and a block with no id is a preview that
/// reports nothing.
///
/// <para>
/// Like the window family's pin set, every pin carries a mutation of the REAL source as its negative
/// sample — the rows of <see cref="Mutations"/> — so none of them can pass vacuously, and every anchor is
/// matched after comment-stripping and whitespace flattening, so indentation, line endings and a comment
/// that merely names an API cannot decide a result.
/// </para>
///
/// <para>
/// What these pins cannot see: whether the blocks read as colours on the game's own row, whether the
/// field takes the typing and the caret behaves, and whether the swatch follows a pick in the frame it
/// lands are run observations — the pins hold the shape the plugin and the adapter were built to.
/// </para>
/// </summary>
public sealed class OnlineUiColorPickerPinTests
{
	[Fact]
	public void TheStoredPreferenceIsTheColourItself() =>
		Assert.True(
			StoresTheColourItself(Plugin("PlayerColorConfigEditor.cs")),
			"an arbitrary colour is not an index: the entry carries the colour's own hex text, and the index path is gone with it");

	[Fact]
	public void TheFieldCommitsOnlyAColourItParsed() =>
		Assert.True(
			CommitsOnlyWhatParses(Plugin("OnlineUiPreferencesDrawer.cs")),
			"the field applies what the player typed only once it IS a colour, and says so while it is not: committing unparsed text would store a value the marker cannot read");

	[Fact]
	public void ThePaletteIsTheRuntimesOwn() =>
		Assert.True(
			BuildsTheGridFromTheRuntime(Plugin("OnlineUiPreferencesDrawer.cs")),
			"the swatch grid must offer the Runtime's palette, or a colour the picker shows is not a colour the automatic assignment can produce");

	[Fact]
	public void AutoIsTheAbsentColour() =>
		Assert.True(
			AutoClearsTheColour(Plugin("OnlineUiPreferencesDrawer.cs")),
			"returning to automatic means storing no colour at all — the null the config, the wire and the automatic resolver all treat as \"derive it from the SteamId\"");

	[Fact]
	public void TheLiveSwatchShowsTheColourThePlayerCarries() =>
		Assert.True(
			PreviewsTheCarriedColour(Plugin("OnlineUiPreferencesDrawer.cs")),
			"the block beside the current colour is the live swatch: it must be built from the colour the markers are drawn from, so a pick moves it at once");

	[Fact]
	public void TheFieldMirrorsTheColourThePlayerCarries() =>
		Assert.True(
			MirrorsTheCarriedColour(Plugin("OnlineUiPreferencesDrawer.cs")),
			"the hex field and the colour correspond LIVE (the user's report: a palette pick left the box without its hex): the field shows the canonical text of the colour the player carries now, and drops its own text the moment a choice is applied — only text that is not a colour yet keeps the box to itself");

	[Fact]
	public void AColourBlockIsTheGamesOwnRowTinted() =>
		Assert.True(
			TheBlockIsTheGamesRow(Adapter("OnlineUiControlFactory.cs"), Adapter("OnlineUiControlView.cs")),
			"a colour block is the game's own button row with the colour laid over its graphic — a hand-built rectangle is the gap this overhaul exists to close");

	[Fact]
	public void AnElementWithNoIdReportsNothing() =>
		Assert.True(
			SilentWithoutAnId(Adapter("OnlineUiControlView.cs")),
			"the preview block carries no id, so clicking it must report nothing at all: reporting an empty id would be dropped by the window and logged as a control that vanished");

	[Fact]
	public void TheColourEditDoesNotOutliveTheWindow() =>
		Assert.True(
			TheEditDiesWithTheWindow(Plugin("OnlineUiWindow.cs"), Plugin("OnlineUiWindowState.cs")),
			"the field's half-typed text belongs to the window it was typed in: closing the window must drop the edit, or reopening it shows a text nobody can see the end of while the stored colour is a different one");

	[Fact]
	public void EveryColourTagCarriesAllFourChannels() =>
		Assert.True(
			EveryTagKeepsTheAlpha(Plugin("OnlineUiMemberListDrawer.cs"), Plugin("OnlineUiHomeDrawer.cs")),
			"the free colour can be translucent, so the two places that render it as a text tag must write all four channels: a three-channel tag would show a translucent marker as an opaque name");

	public static TheoryData<string, string, string, string, string> Mutations => new()
	{
		{ nameof(TheStoredPreferenceIsTheColourItself), "plugin/PlayerColorConfigEditor.cs", "internal void SetColor(PlayerColorValue? color)", "internal void SetColor(int color)", "a preference that stores an index again" },
		{ nameof(TheFieldCommitsOnlyAColourItParsed), "plugin/OnlineUiPreferencesDrawer.cs", "if (PlayerColorValue.TryParseHex(text, out var color))", "if (text.Length > 0)", "a field that stores whatever was typed" },
		{ nameof(ThePaletteIsTheRuntimesOwn), "plugin/OnlineUiPreferencesDrawer.cs", "var palette = PlayerColorResolver.PaletteValues;", "var palette = PickColorsSomewhereElse();", "a grid built from a list of its own" },
		{ nameof(AutoIsTheAbsentColour), "plugin/OnlineUiPreferencesDrawer.cs", "() => Choose(ctx, null)", "() => Choose(ctx, palette[0])", "an Auto control that picks a colour instead" },
		{ nameof(TheLiveSwatchShowsTheColourThePlayerCarries), "plugin/OnlineUiPreferencesDrawer.cs", "var current = ctx.PlayerColor(ctx.Session.LocalSteamId);", "var current = palette[0];", "a preview that shows something other than the carried colour" },
		{ nameof(TheFieldMirrorsTheColourThePlayerCarries), "plugin/OnlineUiPreferencesDrawer.cs", "var hexText = ctx.State.PlayerColorInput ?? current.ToHexString();", "var hexText = color.StoredHex;", "a field that does not follow the colour the player picks" },
		{ nameof(AColourBlockIsTheGamesOwnRowTinted), "adapter/OnlineUiControlFactory.cs", "OnlineUiElementKind.ColorSwatch => ButtonRowPrefabPath,", "OnlineUiElementKind.ColorSwatch => \"Special/GameSettingNonexistent\",", "a block with no game prefab behind it" },
		{ nameof(AnElementWithNoIdReportsNothing), "adapter/OnlineUiControlView.cs", "if (_id.Length > 0)", "if (true)", "a preview block whose click is reported as a control" },
		{ nameof(TheColourEditDoesNotOutliveTheWindow), "plugin/OnlineUiWindow.cs", "_state.PlayerColorInput = null;", "// the half-typed text is kept", "an edit that survives the window it was typed in" },
		{ nameof(EveryColourTagCarriesAllFourChannels), "plugin/OnlineUiMemberListDrawer.cs", "var colorHex = ColorUtility.ToHtmlStringRGBA(", "var colorHex = ColorUtility.ToHtmlStringRGB(", "a list that drops the alpha a translucent marker carries" },
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
		nameof(TheStoredPreferenceIsTheColourItself) => StoresTheColourItself,
		nameof(TheFieldCommitsOnlyAColourItParsed) => CommitsOnlyWhatParses,
		nameof(ThePaletteIsTheRuntimesOwn) => BuildsTheGridFromTheRuntime,
		nameof(AutoIsTheAbsentColour) => AutoClearsTheColour,
		nameof(TheLiveSwatchShowsTheColourThePlayerCarries) => PreviewsTheCarriedColour,
		nameof(TheFieldMirrorsTheColourThePlayerCarries) => MirrorsTheCarriedColour,
		nameof(AColourBlockIsTheGamesOwnRowTinted) => broken => TheBlockIsTheGamesRow(broken, Adapter("OnlineUiControlView.cs")),
		nameof(AnElementWithNoIdReportsNothing) => SilentWithoutAnId,
		nameof(TheColourEditDoesNotOutliveTheWindow) => broken => TheEditDiesWithTheWindow(broken, Plugin("OnlineUiWindowState.cs")),
		nameof(EveryColourTagCarriesAllFourChannels) => broken => EveryTagKeepsTheAlpha(broken, Plugin("OnlineUiHomeDrawer.cs")),
		_ => throw new InvalidOperationException($"no matcher is registered for the pin `{pin}`"),
	};

	/// <summary>The entry is a string holding the colour's text, and the selection is written as a
	/// <c>PlayerColorValue</c> — nothing about the palette index survives it.</summary>
	private static bool StoresTheColourItself(string configSource)
	{
		var flat = Flatten(configSource);

		return flat.Contains("private readonly ConfigEntry<string> _colorHex;", StringComparison.Ordinal)
			&& flat.Contains("internal void SetColor(PlayerColorValue? color)", StringComparison.Ordinal)
			&& flat.Contains("var hex = color?.ToHexString() ?? \"\";", StringComparison.Ordinal)
			&& !flat.Contains("ConfigEntry<int>", StringComparison.Ordinal)
			&& !flat.Contains("ColorIndex", StringComparison.Ordinal);
	}

	/// <summary>The applied value goes through the codec, and text that is not a colour yet only becomes the
	/// text the field holds — it is never stored and never announced.</summary>
	private static bool CommitsOnlyWhatParses(string drawerSource)
	{
		var flat = Flatten(drawerSource);

		return flat.Contains("if (PlayerColorValue.TryParseHex(text, out var color))", StringComparison.Ordinal)
			&& flat.Contains("ctx.ChangePlayerColor?.Invoke(color);", StringComparison.Ordinal)
			&& flat.Contains("ctx.State.PlayerColorInput = text;", StringComparison.Ordinal)
			&& flat.Contains("edited: text => ApplyHex(ctx, text),", StringComparison.Ordinal);
	}

	/// <summary>Every swatch is one of the Runtime's palette entries, offered under its own id.</summary>
	private static bool BuildsTheGridFromTheRuntime(string drawerSource)
	{
		var flat = Flatten(drawerSource);

		return flat.Contains("var palette = PlayerColorResolver.PaletteValues;", StringComparison.Ordinal)
			&& flat.Contains("var names = PlayerColorResolver.PaletteNames;", StringComparison.Ordinal)
			&& flat.Contains("swatches[index] = page.ColorSwatchElement( ColorSwatchControlPrefix + names[index],", StringComparison.Ordinal)
			&& flat.Contains("page.Row(swatches);", StringComparison.Ordinal);
	}

	/// <summary>The automatic choice is the null colour, through the same path every other choice uses.</summary>
	private static bool AutoClearsTheColour(string drawerSource)
	{
		var flat = Flatten(drawerSource);

		return flat.Contains("page.Button(ColorAutoControlId, ctx.T(\"prefs.player_color_auto\"), () => Choose(ctx, null), width: 90f);", StringComparison.Ordinal)
			&& flat.Contains("ctx.ChangePlayerColor?.Invoke(color);", StringComparison.Ordinal);
	}

	/// <summary>The live block is a swatch with no id, built from the colour the player carries now.</summary>
	private static bool PreviewsTheCarriedColour(string drawerSource)
	{
		var flat = Flatten(drawerSource);

		return flat.Contains("var current = ctx.PlayerColor(ctx.Session.LocalSteamId);", StringComparison.Ordinal)
			&& flat.Contains("page.ColorSwatchElement(\"\", current, width: PreviewWidth),", StringComparison.Ordinal);
	}

	/// <summary>The field shows the colour the player carries now — the canonical hex, live — and a choice
	/// made anywhere drops the field's own half-typed text instead of leaving the two out of step.</summary>
	private static bool MirrorsTheCarriedColour(string drawerSource)
	{
		var flat = Flatten(drawerSource);

		return flat.Contains("var hexText = ctx.State.PlayerColorInput ?? current.ToHexString();", StringComparison.Ordinal)
			&& flat.Contains("hexText, maxLength: PlayerColorValue.HexAlphaLength,", StringComparison.Ordinal)
			&& flat.Contains("ctx.State.PlayerColorInput = null;", StringComparison.Ordinal);
	}

	/// <summary>A block is the game's own button row, and the fill lands on the graphic the row shows.</summary>
	private static bool TheBlockIsTheGamesRow(string factorySource, string controlSource)
	{
		var factory = Flatten(factorySource);
		var control = Flatten(controlSource);

		return factory.Contains("OnlineUiElementKind.ColorSwatch => ButtonRowPrefabPath,", StringComparison.Ordinal)
			&& control.Contains("swatch: element.Kind == OnlineUiElementKind.ColorSwatch ? OnlineUiControlFactory.SwatchImageOn(root, button) : null,", StringComparison.Ordinal)
			&& control.Contains("_swatch.color = ToColor(_color);", StringComparison.Ordinal);
	}

	/// <summary>The button's listener reports its CURRENT id, and only when it has one.</summary>
	private static bool SilentWithoutAnId(string controlSource)
	{
		var flat = Flatten(controlSource);

		return flat.Contains("if (_id.Length > 0)", StringComparison.Ordinal)
			&& flat.Contains("_report(new OnlineUiIntent(OnlineUiIntentKind.ControlInvoked, _id));", StringComparison.Ordinal);
	}

	/// <summary>The window's closed branch drops the colour field's edit, and the state is where that edit
	/// lives — the one place a half-typed value is allowed to sit.</summary>
	private static bool TheEditDiesWithTheWindow(string windowSource, string stateSource)
	{
		var window = Flatten(windowSource);

		return window.Contains("if (!_state.Visible)", StringComparison.Ordinal)
			&& window.Contains("_state.PlayerColorInput = null;", StringComparison.Ordinal)
			&& Flatten(stateSource).Contains("internal string? PlayerColorInput;", StringComparison.Ordinal);
	}

	/// <summary>Both text tags that render the marker colour write its alpha too.</summary>
	private static bool EveryTagKeepsTheAlpha(string memberListSource, string homeSource) =>
		Flatten(memberListSource).Contains(
			"var colorHex = ColorUtility.ToHtmlStringRGBA(new Color(row.Color.R, row.Color.G, row.Color.B, row.Color.A));",
			StringComparison.Ordinal)
		&& Flatten(homeSource).Contains(
			"var hex = ColorUtility.ToHtmlStringRGBA(new Color(color.R, color.G, color.B, color.A));",
			StringComparison.Ordinal);

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
