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
	public void Label(string text) => GUILayout.Label(text ?? string.Empty);

	public bool Button(string text) => GUILayout.Button(text ?? string.Empty);

	public string TextField(string current, int maxLength = 64) =>
		GUILayout.TextField(current ?? string.Empty, Math.Max(1, maxLength));

	public void Separator() => GUILayout.Space(6f);
}
