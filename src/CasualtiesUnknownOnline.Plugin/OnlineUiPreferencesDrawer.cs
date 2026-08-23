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
