using UnityEngine;
using UnityEngine.EventSystems;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// Dragging the page's scroll handle (ticket online-ui-paired-controls-and-page-scroll, S4). The handle's
/// place along the track IS the page's position, so a drag converts the pointer's place into a position
/// through the one rule that drew it (<see cref="OnlineUiPageScrollbar.MoveTo"/>) — never by adding a delta
/// of its own, which is how a bar and the page it describes drift apart.
/// </summary>
internal sealed class OnlineUiPageScrollDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
{
	private OnlineUiPageScrollbar? _bar;

	internal void Bind(OnlineUiPageScrollbar bar) => _bar = bar;

	public void OnBeginDrag(PointerEventData eventData) => Apply(eventData);

	public void OnDrag(PointerEventData eventData) => Apply(eventData);

	private void Apply(PointerEventData eventData)
	{
		if (_bar?.OffsetOf(eventData.position, eventData.pressEventCamera) is { } offset)
		{
			_bar.MoveTo(offset);
		}
	}
}
