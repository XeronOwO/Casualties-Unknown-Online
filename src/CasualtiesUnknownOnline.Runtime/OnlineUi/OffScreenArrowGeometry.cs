using System;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Pure geometry for nameplate/off-screen-marker UI. Kept in the Runtime (no
/// UnityEngine dependency) so the Online UI's edge math is covered by L0 unit
/// tests instead of requiring a live camera or game window.
/// </summary>
public static class OffScreenArrowGeometry
{
	/// <summary>
	/// Map a world-projected screen point (GUI coordinates) to either its
	/// on-screen position or a clamped position on the edge rectangle plus the
	/// dominant arrow direction. A point inside the margin rectangle is
	/// considered on-screen (nameplate territory); outside it becomes an
	/// off-screen arrow pinned to the nearest edge.
	/// </summary>
	public static OffScreenArrowPlacement Place(float x, float y, float screenWidth, float screenHeight, float margin)
	{
		if (screenWidth <= 0f || screenHeight <= 0f || margin < 0f)
		{
			return new OffScreenArrowPlacement(x, y, OffScreenArrowDirection.None);
		}

		var minX = margin;
		var maxX = screenWidth - margin;
		var minY = margin;
		var maxY = screenHeight - margin;

		if (x >= minX && x <= maxX && y >= minY && y <= maxY)
		{
			return new OffScreenArrowPlacement(x, y, OffScreenArrowDirection.None);
		}

		var centerX = screenWidth * 0.5f;
		var centerY = screenHeight * 0.5f;
		var dx = x - centerX;
		var dy = y - centerY;

		// The rectangle the arrow is pinned to — the on-screen area minus the
		// margin around it, centered on the screen center.
		var halfWidth = Math.Max(0f, (screenWidth * 0.5f) - margin);
		var halfHeight = Math.Max(0f, (screenHeight * 0.5f) - margin);

		// Clamp the ray from the center to the edge rectangle. The smallest
		// positive scale that brings the point onto the rectangle keeps
		// direction intact.
		float scale;
		if (dx == 0f && dy == 0f)
		{
			scale = 1f;
		}
		else
		{
			var sx = Math.Abs(dx) > 0f ? halfWidth / Math.Abs(dx) : float.PositiveInfinity;
			var sy = Math.Abs(dy) > 0f ? halfHeight / Math.Abs(dy) : float.PositiveInfinity;
			scale = Math.Min(sx, sy);
		}

		var edgeX = centerX + (dx * scale);
		var edgeY = centerY + (dy * scale);

		var direction = edgeY <= minY + 0.001f
			? OffScreenArrowDirection.Up
			: edgeY >= maxY - 0.001f
				? OffScreenArrowDirection.Down
				: edgeX <= minX + 0.001f
					? OffScreenArrowDirection.Left
					: OffScreenArrowDirection.Right;

		return new OffScreenArrowPlacement(edgeX, edgeY, direction);
	}

	/// <summary>True when the point is inside the guarded on-screen rectangle
	/// (nameplate territory), false when it needs an off-screen arrow.</summary>
	public static bool IsOnScreen(float x, float y, float screenWidth, float screenHeight, float margin) =>
		x >= margin && x <= screenWidth - margin && y >= margin && y <= screenHeight - margin;
}
