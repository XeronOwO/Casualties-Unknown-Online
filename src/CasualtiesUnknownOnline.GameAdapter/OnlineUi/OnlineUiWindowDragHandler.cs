using UnityEngine;
using UnityEngine.EventSystems;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// Drags the Online UI window by its title bar (ticket online-ui-art-and-controls-overhaul, S2b). The
/// IMGUI window was draggable through <c>GUI.Window</c>; on the game's own canvas the window is a
/// <see cref="RectTransform"/>, so moving it is this component's whole job.
///
/// <para>
/// The delta arrives in screen pixels and the window is positioned in its parent canvas' units, so the
/// canvas scale is divided out — the same scale the game's own UI uses. The window may be moved almost
/// anywhere, as the IMGUI one could, but not entirely off the canvas: the frame grew with the user's
/// acceptance pass, and a title bar dragged past the edge is a window nobody can grab again, so a strip of
/// it always stays reachable.
/// </para>
/// </summary>
internal sealed class OnlineUiWindowDragHandler : MonoBehaviour, IDragHandler
{
	/// <summary>How much of the window stays inside the canvas on each axis, so its title bar can always be
	/// grabbed again (ticket online-ui-layout-and-input-detail-pass, a detail found while re-cutting the
	/// frame).</summary>
	internal const float Reach = 48f;

	private RectTransform? _target;

	internal void Bind(RectTransform target) => _target = target;

	public void OnDrag(PointerEventData eventData)
	{
		// Unity object — == (the window dies with the surface)
		if (_target == null)
		{
			return;
		}

		var canvas = _target.GetComponentInParent<Canvas>();
		var scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
		_target.anchoredPosition += eventData.delta / scale;
		ClampToCanvas(canvas);
	}

	/// <summary>Keeps <see cref="Reach"/> units of the window inside the canvas on both axes. The window is
	/// anchored at the canvas's centre, so its position is symmetric about it.</summary>
	private void ClampToCanvas(Canvas? canvas)
	{
		if (_target is null || canvas is null || canvas.transform is not RectTransform bounds || bounds.rect.width <= 0f || bounds.rect.height <= 0f)
		{
			return;
		}

		var half = _target.rect.size * 0.5f;
		var limitX = Mathf.Max(0f, (bounds.rect.width * 0.5f) + half.x - Reach);
		var limitY = Mathf.Max(0f, (bounds.rect.height * 0.5f) + half.y - Reach);
		var position = _target.anchoredPosition;
		_target.anchoredPosition = new Vector2(
			Mathf.Clamp(position.x, -limitX, limitX),
			Mathf.Clamp(position.y, -limitY, limitY));
	}
}
