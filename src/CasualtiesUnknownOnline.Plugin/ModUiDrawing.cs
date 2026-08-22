using System;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The Unity IMGUI bridge for the local mod UI surface. The mod domain only
/// stores the mod-facing <see cref="IModUiWindow"/> callbacks; this class owns
/// the only Unity knowledge: it projects each registered window into a
/// <see cref="GUI.Window"/> and translates the tiny mod control alphabet into
/// GUILayout calls. A mod draw callback throwing never breaks the frame — the
/// error is shown in the window and forwarded to the plugin logger.
/// </summary>
internal static class ModUiDrawing
{
	internal static void DrawAll(IModUiControl control, Action<Exception>? onError)
	{
		var windows = control.Windows;
		if (windows.Count == 0)
		{
			return;
		}

		for (var i = 0; i < windows.Count; i++)
		{
			var window = windows[i];
			var width = 260f;
			var rect = new Rect(Screen.width - width - 12f - (i * 24f), 40f, width, 220f);
			GUI.Window(2000 + i, rect, id =>
			{
				GUILayout.Label(window.Title);
				try
				{
					window.Draw(new ModUiRenderer());
				}
				catch (Exception e)
				{
					onError?.Invoke(e);
					GUILayout.Label($"Mod UI error: {e.Message}");
				}
			}, window.Title);
		}
	}
}
