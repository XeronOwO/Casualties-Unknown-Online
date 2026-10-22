using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The drag gesture's own label: WHO the item in hand would be used on, drawn next to the
/// cursor while the cursor is over a player the release would use it on.
/// <para>
/// The NAME is the requirement, not the decoration (user ruling 2026-10-06): two players
/// standing on each other cannot be told apart by a highlight, so a label naming the target
/// is what turns a release into a decision instead of a guess. It is drawn beside the
/// cursor rather than on the target because the target's own rendering belongs to the game
/// (the backpack projection reads those clones too), and it is a second IMGUI surface
/// beside the start-gate panel so neither owns the other's lifecycle.
/// </para>
/// </summary>
internal static class DragUseCueOverlay
{
	private const float CursorOffset = 18f;

	internal static void Draw(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}

		var style = new GUIStyle(GUI.skin.label)
		{
			fontSize = 16,
			alignment = TextAnchor.MiddleLeft,
			padding = new RectOffset(10, 10, 4, 4),
		};
		style.normal.textColor = Color.white;

		var size = style.CalcSize(new GUIContent(text));

		// Input.mousePosition is measured from the BOTTOM-left and IMGUI from the TOP-left, so the y is
		// flipped before the label is placed: passing the cursor's y straight through (as the first version
		// did) put the label a mirror-image distance away from the pointer, and off the screen for a cursor
		// in the lower half. Both axes are clamped, so a pointer outside the window (the drag continues while
		// the game is in the background) still leaves the label inside the frame.
		var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
		var x = Mathf.Clamp(mouse.x + CursorOffset, 0f, Mathf.Max(0f, Screen.width - size.x));
		var y = Mathf.Clamp(mouse.y - CursorOffset - size.y, 0f, Mathf.Max(0f, Screen.height - size.y));
		var rect = new Rect(x, y, size.x, size.y);

		var previous = GUI.color;
		GUI.color = new Color(0f, 0f, 0f, 0.72f);
		GUI.Box(rect, string.Empty);
		GUI.color = previous;
		GUI.Label(rect, text, style);
	}
}
