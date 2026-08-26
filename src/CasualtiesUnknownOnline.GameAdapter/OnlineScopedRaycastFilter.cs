using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// UGUI raycast filter for the CUO Online UI's non-modal surfaces. The
/// blocker GameObject itself is a full-canvas transparent <see cref="Image"/>
/// with <c>raycastTarget = true</c>; this component makes the Graphic raycast
/// only when the pointer is inside one of the CUO screen-space rectangles
/// (IMGUI GUI coordinates, Y down). Outside those rectangles the ray passes
/// through to the normal game/menu UI.
/// </summary>
internal sealed class OnlineScopedRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
{
	private IReadOnlyList<OnlineUiBlockRect> _blocks = [];

	internal void SetBlocks(IReadOnlyList<OnlineUiBlockRect> blocks) => _blocks = blocks;

	public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
	{
		// UGUI screen points use Y-up; IMGUI/CUO rectangles use Y-down.
		var guiX = screenPoint.x;
		var guiY = Screen.height - screenPoint.y;
		foreach (var block in _blocks)
		{
			if (block.Contains(guiX, guiY))
			{
				return true;
			}
		}

		return false;
	}
}
