using System;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Preferences page: local CUO multiplayer personal settings, deliberately
/// separate from the game's own options — the UI language, the CUO log level,
/// the local player colour and the saved configuration profiles. All of them are
/// BepInEx-config-backed and apply immediately without a restart.
///
/// <para>
/// Since ticket online-ui-art-and-controls-overhaul (S2b) each choice is the game's own control: a
/// dropdown (<c>TMP_Dropdown</c>) that opens on click, tracks the pointer and closes on click-away,
/// Escape and selection. The hand-rolled "a button that lists buttons" shape — and the three
/// open/closed booleans it needed on the window state — are gone with the IMGUI path. The player colour
/// stopped being a choice among presets in S3: it is a hex field plus a palette of blocks, so any colour
/// the game's own hex idiom can name is available.
/// </para>
/// </summary>
internal static class OnlineUiPreferencesDrawer
{
	private const float ChoiceWidth = 150f;

	/// <summary>The width of one palette swatch in the colour picker's grid.</summary>
	private const float SwatchWidth = 88f;

	/// <summary>The width of the live block that shows the colour the player carries now.</summary>
	private const float PreviewWidth = 64f;

	/// <summary>The id of the free colour's hex field.</summary>
	private const string ColorHexControlId = "prefs.player_color.hex";

	/// <summary>The id of the control that returns the marker to the automatic palette.</summary>
	private const string ColorAutoControlId = "prefs.player_color.auto";

	/// <summary>The prefix of one palette swatch's id; the palette entry's own name follows it.</summary>
	private const string ColorSwatchControlPrefix = "prefs.player_color.swatch.";

	private static readonly string[] LogLevels =
	[
		"Trace",
		"Debug",
		"Information",
		"Warning",
		"Error",
		"Critical",
		"None",
	];

	private static readonly (string Code, string Label)[] Languages =
	[
		("en", "English"),
		("zh", "简体中文"),
	];

	internal static void Build(OnlineUiContext ctx, OnlineUiPageBuilder page)
	{
		page.Section(ctx.T("prefs.title"));
		page.Muted(ctx.T("prefs.local_note"));

		BuildLogLevel(ctx, page);

		BuildLanguage(ctx, page);

		BuildColor(ctx, page);

		BuildProfiles(ctx, page);
	}

	private static void BuildLogLevel(OnlineUiContext ctx, OnlineUiPageBuilder page)
	{
		if (ctx.Logging is not { } logging)
		{
			return;
		}

		page.Section(ctx.T("prefs.log_level"));
		var current = Array.IndexOf(LogLevels, logging.Current);
		page.Dropdown(
			"prefs.log_level",
			ctx.T("prefs.log_level_current"),
			LogLevels,
			current < 0 ? 0 : current,
			index => logging.Set(LogLevels[index]),
			width: ChoiceWidth);
		page.Muted(ctx.T("prefs.log_level_hint"));
	}

	private static void BuildLanguage(OnlineUiContext ctx, OnlineUiPageBuilder page)
	{
		if (ctx.Language is not { } language)
		{
			return;
		}

		page.Section(ctx.T("prefs.language"));
		var labels = new string[Languages.Length];
		var current = 0;
		for (var index = 0; index < Languages.Length; index++)
		{
			labels[index] = Languages[index].Label;
			if (language.Current.StartsWith(Languages[index].Code, StringComparison.OrdinalIgnoreCase))
			{
				current = index;
			}
		}

		page.Dropdown(
			"prefs.language",
			ctx.T("prefs.language_current"),
			labels,
			current,
			index => language.Set(Languages[index].Code),
			width: ChoiceWidth);
		page.Muted(ctx.T("prefs.language_hint"));
	}

	/// <summary>
	/// The free colour picker (ticket online-ui-art-and-controls-overhaul, S3). The player's colour is no
	/// longer an index into a fixed row: the hex field accepts any colour the game's own idiom names, and
	/// the palette is offered beside it as blocks for speed. The block beside the current colour is the
	/// live swatch — it is built from the colour the player carries now, so it follows a pick at once.
	/// </summary>
	private static void BuildColor(OnlineUiContext ctx, OnlineUiPageBuilder page)
	{
		if (ctx.ColorConfig is not { } color)
		{
			return;
		}

		page.Section(ctx.T("prefs.player_color"));
		page.Muted(ctx.T("prefs.player_color_hint"));

		// The colour the player carries now, as a block beside its name. It is read from the same fact the
		// markers are drawn from, so a pick moves the block in the frame it lands.
		var current = ctx.PlayerColor(ctx.Session.LocalSteamId);
		page.Row(
			page.LabelElement(ctx.T("prefs.player_color_current"), color: OnlineUiTheme.Muted),
			page.ColorSwatchElement("", current, width: PreviewWidth),
			page.LabelElement(ColorName(ctx, color.StoredHex, current)));

		// The field shows what the player has typed for as long as the edit lasts, and the stored colour when
		// there is none — a closed window takes its half-typed text with it. What the player types is applied
		// the moment it IS a colour, which is what keeps the swatch and every marker live while the field is
		// in use; until then the line below says what is missing.
		// The field and the colour correspond LIVE, in both directions (the user's ask, 2026-09-27): a pick
		// anywhere — a palette block, Auto, a colour the player typed — moves what the field shows in the
		// same frame, because the field is the canonical text of the colour the player carries now. The only
		// text it keeps for itself is one that is not a colour yet, which is what makes the line below able
		// to say so.
		var hexText = ctx.State.PlayerColorInput ?? current.ToHexString();
		page.Row(
			page.LabelElement(ctx.T("prefs.player_color_hex"), color: OnlineUiTheme.Muted),
			page.TextFieldElement(
				ColorHexControlId,
				"",
				hexText,
				maxLength: PlayerColorValue.HexAlphaLength,
				edited: text => ApplyHex(ctx, text),
				width: ChoiceWidth));

		if (PlayerColorInputIsInvalid(ctx))
		{
			page.Status(ctx.T("prefs.player_color_invalid"), OnlineUiTheme.Error);
		}

		var palette = PlayerColorResolver.PaletteValues;
		var names = PlayerColorResolver.PaletteNames;
		var swatches = new OnlineUiElementModel[palette.Count];
		for (var index = 0; index < palette.Count; index++)
		{
			var value = palette[index];
			swatches[index] = page.ColorSwatchElement(
				ColorSwatchControlPrefix + names[index],
				value,
				() => Choose(ctx, value),
				width: SwatchWidth);
		}

		page.Row(swatches);
		page.Button(ColorAutoControlId, ctx.T("prefs.player_color_auto"), () => Choose(ctx, null), width: 90f);
	}

	/// <summary>
	/// Reads the typed text: a colour is applied (and the field goes on to show the colour's own canonical
	/// text), an emptied field is the automatic colour — the same thing the Auto control sets — and anything
	/// else only becomes the text the field holds, with the line below saying what is missing. What the
	/// player already had is left alone in that last case.
	/// </summary>
	private static void ApplyHex(OnlineUiContext ctx, string text)
	{
		ctx.State.PlayerColorInput = text;
		if (text.Trim().Length == 0)
		{
			Choose(ctx, null);
			return;
		}

		if (PlayerColorValue.TryParseHex(text, out var color))
		{
			Choose(ctx, color);
		}
	}

	/// <summary>Applies a colour choice — null returns the marker to the automatic palette — and drops the
	/// half-typed text: the field shows the colour itself from here (its canonical hex), so there is nothing
	/// for CUO to keep in step with it.</summary>
	private static void Choose(OnlineUiContext ctx, PlayerColorValue? color)
	{
		ctx.State.PlayerColorInput = null;
		ctx.ChangePlayerColor?.Invoke(color);
	}

	/// <summary>Whether what the field holds is not a colour yet. An empty field is the automatic colour,
	/// which is a choice rather than a mistake, so it is not reported.</summary>
	private static bool PlayerColorInputIsInvalid(OnlineUiContext ctx) =>
		ctx.State.PlayerColorInput is { Length: > 0 } text && !PlayerColorValue.TryParseHex(text, out _);

	/// <summary>
	/// The name the current colour is shown under: the palette's own name when the stored colour is one of
	/// the palette's, the colour's hex text when it is a free colour, and the automatic label when nothing
	/// is stored. Matching by the stored text rather than by the channels is what makes a palette colour
	/// recognisable after the round trip through the config file.
	/// </summary>
	private static string ColorName(OnlineUiContext ctx, string storedHex, PlayerColorValue current)
	{
		if (storedHex.Trim().Length == 0)
		{
			return ctx.T("prefs.player_color_auto");
		}

		var index = PaletteIndexOf(storedHex);
		return index >= 0
			? ctx.T($"prefs.color.{PlayerColorResolver.PaletteNames[index]}")
			: current.ToHexString();
	}

	/// <summary>The palette entry the stored text came from, or -1 when it is a free colour.</summary>
	private static int PaletteIndexOf(string storedHex)
	{
		var palette = PlayerColorResolver.PaletteValues;
		for (var index = 0; index < palette.Count; index++)
		{
			if (string.Equals(palette[index].ToHexString(), storedHex, StringComparison.OrdinalIgnoreCase))
			{
				return index;
			}
		}

		return -1;
	}

	private static void BuildProfiles(OnlineUiContext ctx, OnlineUiPageBuilder page)
	{
		if (ctx.Profiles is not { } profiles)
		{
			return;
		}

		page.Section(ctx.T("prefs.profiles"));
		page.Muted(ctx.T("prefs.profiles_hint"));

		page.Row(
			page.LabelElement(ctx.T("prefs.profile_name"), color: OnlineUiTheme.Muted),
			page.TextFieldElement(
				"prefs.profile_name",
				"",
				ctx.State.ProfileNameInput,
				maxLength: 32,
				edited: text => ctx.State.ProfileNameInput = text,
				width: 200f),
			page.ButtonElement("prefs.profile_save", ctx.T("prefs.profile_save"), () => SaveProfile(ctx, profiles), width: 110f));

		page.Space();
		var saved = profiles.ListProfiles();
		if (saved.Count == 0)
		{
			page.Muted(ctx.T("prefs.profiles_empty"));
		}

		foreach (var name in saved)
		{
			page.Row(
				page.LabelElement(name),
				page.ButtonElement(
					$"prefs.profile_apply.{name}",
					ctx.T("prefs.profile_apply"),
					() => SetProfileStatus(
						ctx,
						profiles.TryApply(name, out var error)
							? ctx.F("prefs.profile_applied", name)
							: ctx.F("prefs.profile_error", error),
						isError: error.Length > 0),
					width: 80f),
				page.ButtonElement(
					$"prefs.profile_delete.{name}",
					ctx.T("prefs.profile_delete"),
					() => SetProfileStatus(
						ctx,
						profiles.TryDelete(name, out var error)
							? ctx.F("prefs.profile_deleted", name)
							: ctx.F("prefs.profile_error", error),
						isError: error.Length > 0),
					width: 80f));
		}

		if (ctx.State.ProfileStatus is { } status)
		{
			// The status keeps the muted body size and takes the outcome's colour, as the IMGUI row did.
			var color = ctx.State.ProfileStatusIsError ? OnlineUiTheme.Error : OnlineUiTheme.Positive;
			page.Muted($"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{status}</color>");
		}
	}

	private static void SaveProfile(OnlineUiContext ctx, ConfigurationProfileStore profiles)
	{
		var name = ctx.State.ProfileNameInput.Trim();
		if (profiles.TrySaveCurrent(name, out var error))
		{
			ctx.State.ProfileNameInput = "";
			SetProfileStatus(ctx, ctx.F("prefs.profile_saved", name), isError: false);
		}
		else
		{
			SetProfileStatus(ctx, ctx.F("prefs.profile_error", error), isError: true);
		}
	}

	private static void SetProfileStatus(OnlineUiContext ctx, string status, bool isError)
	{
		ctx.State.ProfileStatus = status;
		ctx.State.ProfileStatusIsError = isError;
	}
}
