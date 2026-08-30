using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Preferences page: local CUO multiplayer personal settings, deliberately
/// separate from the game's own options. Current entries are UI language and
/// CUO log level; both are BepInEx-config-backed and apply immediately without
/// a restart.
/// </summary>
internal static class OnlineUiPreferencesDrawer
{
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

	internal static void Draw(OnlineUiContext ctx)
	{
		GUILayout.Label(ctx.T("prefs.title"), OnlineUiTheme.Section());
		GUILayout.Label(ctx.T("prefs.local_note"), OnlineUiTheme.MutedLabel());

		GUILayout.Space(10f);
		DrawLogLevel(ctx);

		GUILayout.Space(10f);
		DrawLanguage(ctx);

		GUILayout.Space(10f);
		DrawColor(ctx);

		GUILayout.Space(10f);
		DrawProfiles(ctx);
	}

	private static void DrawProfiles(OnlineUiContext ctx)
	{
		if (ctx.Profiles is not { } profiles)
		{
			return;
		}

		GUILayout.Label(ctx.T("prefs.profiles"), OnlineUiTheme.Section());
		GUILayout.Label(ctx.T("prefs.profiles_hint"), OnlineUiTheme.MutedLabel());

		GUILayout.BeginHorizontal();
		GUILayout.Label(ctx.T("prefs.profile_name"), OnlineUiTheme.MutedLabel());
		ctx.State.ProfileNameInput = GUILayout.TextField(ctx.State.ProfileNameInput, 32, GUILayout.Width(200f));
		if (GUILayout.Button(ctx.T("prefs.profile_save"), OnlineUiTheme.Button(), GUILayout.Width(110f)))
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

		GUILayout.EndHorizontal();

		GUILayout.Space(8f);
		var saved = profiles.ListProfiles();
		if (saved.Count == 0)
		{
			GUILayout.Label(ctx.T("prefs.profiles_empty"), OnlineUiTheme.MutedLabel());
		}

		foreach (var name in saved)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(name, OnlineUiTheme.Label());
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(ctx.T("prefs.profile_apply"), OnlineUiTheme.Button(), GUILayout.Width(80f)))
			{
				SetProfileStatus(ctx, profiles.TryApply(name, out var error)
					? ctx.F("prefs.profile_applied", name)
					: ctx.F("prefs.profile_error", error),
					isError: error.Length > 0);
			}

			if (GUILayout.Button(ctx.T("prefs.profile_delete"), OnlineUiTheme.Button(), GUILayout.Width(80f)))
			{
				SetProfileStatus(ctx, profiles.TryDelete(name, out var error)
					? ctx.F("prefs.profile_deleted", name)
					: ctx.F("prefs.profile_error", error),
					isError: error.Length > 0);
			}

			GUILayout.EndHorizontal();
		}

		if (ctx.State.ProfileStatus is { } status)
		{
			var color = ctx.State.ProfileStatusIsError ? OnlineUiTheme.Error : OnlineUiTheme.Positive;
			GUILayout.Label($"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{status}</color>", OnlineUiTheme.MutedLabel());
		}
	}

	private static void SetProfileStatus(OnlineUiContext ctx, string status, bool isError)
	{
		ctx.State.ProfileStatus = status;
		ctx.State.ProfileStatusIsError = isError;
	}

	private static void DrawLogLevel(OnlineUiContext ctx)
	{
		if (ctx.Logging is not { } logging)
		{
			return;
		}

		GUILayout.Label(ctx.T("prefs.log_level"), OnlineUiTheme.Section());
		DrawDropdown(
			ctx,
			ctx.T("prefs.log_level_current"),
			logging.Current,
			() => ctx.State.LogLevelOptionsOpen,
			value => ctx.State.LogLevelOptionsOpen = value,
			LogLevels.Select(level => (level, level)),
			level => logging.Set(level));
		GUILayout.Label(ctx.T("prefs.log_level_hint"), OnlineUiTheme.MutedLabel());
	}

	private static void DrawLanguage(OnlineUiContext ctx)
	{
		if (ctx.Language is not { } language)
		{
			return;
		}

		GUILayout.Label(ctx.T("prefs.language"), OnlineUiTheme.Section());
		var currentLabel = language.Current.StartsWith("zh", System.StringComparison.OrdinalIgnoreCase) ? "简体中文" : "English";
		DrawDropdown(
			ctx,
			ctx.T("prefs.language_current"),
			currentLabel,
			() => ctx.State.LanguageOptionsOpen,
			value => ctx.State.LanguageOptionsOpen = value,
			Languages,
			code => language.Set(code));
		GUILayout.Label(ctx.T("prefs.language_hint"), OnlineUiTheme.MutedLabel());
	}

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

	private static void DrawColor(OnlineUiContext ctx)
	{
		if (ctx.ColorConfig is not { } color)
		{
			return;
		}

		GUILayout.Label(ctx.T("prefs.player_color"), OnlineUiTheme.Section());
		GUILayout.Label(ctx.T("prefs.player_color_hint"), OnlineUiTheme.MutedLabel());

		var currentLabel = color.ColorIndex >= 0
			? ctx.T($"prefs.color.{ColorKeys[color.ColorIndex]}")
			: ctx.T("prefs.player_color_auto");
		var options = new List<(string Code, string Label)>
		{
			("-1", ctx.T("prefs.player_color_auto")),
		};
		for (var i = 0; i < ColorKeys.Length; i++)
		{
			options.Add((i.ToString(), ctx.T($"prefs.color.{ColorKeys[i]}")));
		}

		DrawDropdown(
			ctx,
			ctx.T("prefs.player_color_current"),
			currentLabel,
			() => ctx.State.ColorOptionsOpen,
			value => ctx.State.ColorOptionsOpen = value,
			options,
			code => ctx.ChangePlayerColor?.Invoke(int.Parse(code)));
	}

	private static void DrawDropdown(
		OnlineUiContext ctx,
		string currentLabel,
		string currentValue,
		Func<bool> isOpen,
		Action<bool> setOpen,
		IEnumerable<(string Code, string Label)> options,
		Action<string> select)
	{
		GUILayout.BeginHorizontal();
		GUILayout.Label(currentLabel, OnlineUiTheme.MutedLabel());
		GUILayout.FlexibleSpace();
		if (GUILayout.Button(currentValue, OnlineUiTheme.Button(), GUILayout.Width(150f)))
		{
			setOpen(!isOpen());
		}

		GUILayout.EndHorizontal();

		if (!isOpen())
		{
			return;
		}

		foreach (var option in options)
		{
			if (GUILayout.Button(option.Label, OnlineUiTheme.Button()))
			{
				select(option.Code);
				setOpen(false);
			}
		}
	}
}
