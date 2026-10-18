using CasualtiesUnknownOnline.Runtime.OnlineUi;
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
/// canvas scale is divided out — the same scale the game's own UI uses. Where the window may go is
/// <see cref="OnlineUiWindowDrag"/>'s rule: the frame may be moved almost anywhere, but the title bar — the
/// one place a drag can start — always stays reachable.
/// </para>
/// </summary>
internal sealed class OnlineUiWindowDragHandler : MonoBehaviour, IDragHandler
{
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
		var position = _target.anchoredPosition + (eventData.delta / scale);

		// A window with no canvas to hang on keeps the IMGUI behaviour and simply follows the pointer; the
		// clamp is the canvas's rule, so it only exists where there is a canvas rect.
		if (canvas?.transform is RectTransform bounds && bounds.rect.width > 0f && bounds.rect.height > 0f)
		{
			var clamped = OnlineUiWindowDrag.Clamp(
				bounds.rect.width,
				bounds.rect.height,
				_target.rect.width,
				_target.rect.height,
				position.x,
				position.y);
			position = new Vector2(clamped.X, clamped.Y);
		}

		_target.anchoredPosition = position;
	}
}
