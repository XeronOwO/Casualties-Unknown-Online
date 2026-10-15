using UnityEngine;
using UnityEngine.EventSystems;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// Clicking the page's scroll track (ticket online-ui-paired-controls-and-page-scroll, S4): the page jumps to
/// the part the click names, the conventional reading of a click on a track that has no arrow buttons. It
/// goes through the same one write a drag does (<see cref="OnlineUiPageScrollbar.MoveTo"/>), so the two
/// gestures cannot disagree about what a place on the track means.
/// </summary>
internal sealed class OnlineUiPageScrollClick : MonoBehaviour, IPointerClickHandler
{
	private OnlineUiPageScrollbar? _bar;

	internal void Bind(OnlineUiPageScrollbar bar) => _bar = bar;

	public void OnPointerClick(PointerEventData eventData)
	{
		if (_bar?.OffsetOf(eventData.position, eventData.pressEventCamera) is { } offset)
		{
			_bar.MoveTo(offset);
		}
	}
}
