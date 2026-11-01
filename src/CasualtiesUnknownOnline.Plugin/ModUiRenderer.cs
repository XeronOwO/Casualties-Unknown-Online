using System;
using CasualtiesUnknownOnline.Abstractions;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The Unity-backed <see cref="IModUiWindow"/> passed to mod draw callbacks.
/// This is the projection point for the mod UI alphabet: the mod calls
/// Label/Button/TextField/Separator, and this class drives Unity's GUILayout.
/// </summary>
internal sealed class ModUiRenderer : IModUiWindow
{
	// Every control here passes a style of its own: an un-styled GUILayout call inherits the GAME's skin, whose padding, font
	// size and colours are not this panel's (measured 2026-11-02 on the console's list, which inherited the game's scroll-view
	// padding and indented every row).
	public void Label(string text) => GUILayout.Label(text ?? string.Empty, OnlineUiTheme.Label());

	public bool Button(string text) => GUILayout.Button(text ?? string.Empty, OnlineUiTheme.Button());

	public string TextField(string current, int maxLength = 64) =>
		GUILayout.TextField(current ?? string.Empty, Math.Max(1, maxLength), OnlineUiTheme.TextField());

	public void Separator() => GUILayout.Space(6f);
}
