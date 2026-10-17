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

	internal static void Draw(string targetName)
	{
		if (string.IsNullOrEmpty(targetName))
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

		var text = $"Use on {targetName}";
		var size = style.CalcSize(new GUIContent(text));
		var mouse = Input.mousePosition;

		// The cursor is measured from the TOP-left and IMGUI from the top-left too, so the
		// label follows the cursor directly; it is pulled back inside the window when the
		// cursor is near an edge, so the name is never the part that goes off screen.
		var x = Mathf.Min(mouse.x + CursorOffset, Screen.width - size.x);
		var y = Mathf.Max(mouse.y - CursorOffset - size.y, 0f);
		var rect = new Rect(x, y, size.x, size.y);

		var previous = GUI.color;
		GUI.color = new Color(0f, 0f, 0f, 0.72f);
		GUI.Box(rect, string.Empty);
		GUI.color = previous;
		GUI.Label(rect, text, style);
	}
}
