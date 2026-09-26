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
/// canvas scale is divided out — the same scale the game's own UI uses. The window is never clamped: the
/// player could move the IMGUI window off the screen edge too, and remembering where it was left is the
/// behaviour that existed.
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
		_target.anchoredPosition += eventData.delta / scale;
	}
}
