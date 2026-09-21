using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Start-gate overlay: the waiting text in a translucent panel pinned to the
/// BOTTOM-RIGHT corner (the loading screen's own info slot, #87) over the LIVE
/// frozen world. No full-screen blackout: the gate freezes the world behind it
/// and the panel keeps the wait readable without turning the wait into "a black
/// screen" (the original black-texture attempt). It is its own IMGUI surface, so
/// it does not live inside the plugin's lifecycle class or the Online UI
/// overlay's composition; the plugin draws it at the same point in the frame the
/// HUD would have been drawn, with the start gate holding the HUD back.
/// </summary>
internal static class StartGateOverlay
{
	internal static void Draw(string waitingText)
	{
		const float margin = 24f;
		const float height = 64f;
		var width = Screen.width - (margin * 2f);
		if (width < 1f)
		{
			return;
		}

		width = Mathf.Min(width, 520f);
		var rect = new Rect(Screen.width - width - margin, Screen.height - height - margin, width, height);

		var previous = GUI.color;
		GUI.color = new Color(0f, 0f, 0f, 0.72f);
		GUI.Box(rect, string.Empty);
		GUI.color = previous;

		var style = new GUIStyle(GUI.skin.label)
		{
			fontSize = 20,
			alignment = TextAnchor.MiddleRight,
			padding = new RectOffset(0, 18, 0, 0),
		};
		style.normal.textColor = Color.white;
		GUI.Label(rect, waitingText, style);
	}
}
