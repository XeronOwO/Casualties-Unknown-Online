using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Configuration;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Preferences page: local CUO multiplayer personal settings, deliberately
/// separate from the game's own options — the UI language, the CUO log level,
/// the local player colour and the saved configuration profiles. All of them are
/// BepInEx-config-backed and apply immediately without a restart.
///
/// <para>
/// Since ticket online-ui-art-and-controls-overhaul (S2b) each of the three choices is the game's own
/// dropdown (<c>TMP_Dropdown</c>): it opens on click, tracks the pointer and closes on click-away,
/// Escape and selection. The hand-rolled "a button that lists buttons" shape — and the three
/// open/closed booleans it needed on the window state — are gone with the IMGUI path.
/// </para>
/// </summary>
internal static class OnlineUiPreferencesDrawer
{
	private const float ChoiceWidth = 150f;

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

	private static readonly string[] ColorKeys =
	[
		"red",
		"blue",
		"green",
		"orange",
		"purple",
		"cyan",
		"pink",
		"yellow",
	];

	internal static void Build(OnlineUiContext ctx, OnlineUiPageBuilder page)
	{
		page.Section(ctx.T("prefs.title"));
		page.Muted(ctx.T("prefs.local_note"));

		page.Space();
		BuildLogLevel(ctx, page);

		page.Space();
		BuildLanguage(ctx, page);

		page.Space();
		BuildColor(ctx, page);

		page.Space();
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

	private static void BuildColor(OnlineUiContext ctx, OnlineUiPageBuilder page)
	{
		if (ctx.ColorConfig is not { } color)
		{
			return;
		}

		page.Section(ctx.T("prefs.player_color"));
		page.Muted(ctx.T("prefs.player_color_hint"));

		var options = new List<string>(ColorKeys.Length + 1)
		{
			ctx.T("prefs.player_color_auto"),
		};
		foreach (var key in ColorKeys)
		{
			options.Add(ctx.T($"prefs.color.{key}"));
		}

		// The auto entry sits at index 0, so the config's -1 (auto) and 0..7 (palette) shift by one.
		var selected = color.ColorIndex >= 0 && color.ColorIndex < ColorKeys.Length ? color.ColorIndex + 1 : 0;
		page.Dropdown(
			"prefs.player_color",
			ctx.T("prefs.player_color_current"),
			options,
			selected,
			index => ctx.ChangePlayerColor?.Invoke(index - 1),
			width: ChoiceWidth);
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
