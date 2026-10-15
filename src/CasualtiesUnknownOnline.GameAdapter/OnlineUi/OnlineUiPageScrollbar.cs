using CasualtiesUnknownOnline.Runtime.OnlineUi;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// The bar on the right edge of a page that does not fit (ticket
/// online-ui-paired-controls-and-page-scroll, S4): a thin track with a handle that says how much of the page
/// is shown and where in it the player stands, and that the player can drag or click along.
///
/// <para>
/// It exists because the game ships no scrollable surface to read one from — its screens place their rows by
/// hand and its own settings screen does not scroll — so the drawing is CUO's: the game's own sprite for the
/// track and for the handle, in the page's own palette, and the arithmetic in
/// <see cref="OnlineUiPageScroll"/>. The door the ticket left open for the game's own art was checked and is
/// closed for the reason recorded there.
/// </para>
///
/// <para>
/// The bar is a sibling of the page's VIEWPORT rather than of its content, so it does not scroll away with
/// what it describes, and it is hidden outright when the page fits: the wheel and the drag are the same
/// scroll view's, and the bar only ever reports the position it was asked to show. What it does with a drag
/// or a click is <see cref="OnlineUiPageScrollDrag"/> and <see cref="OnlineUiPageScrollClick"/>, one file
/// each because one type is one file.
/// </para>
/// </summary>
internal sealed class OnlineUiPageScrollbar
{
	/// <summary>The bar's width: narrower than a control's own edge so it reads as a margin note rather than
	/// as another control, and wide enough for a pointer to land on.</summary>
	internal const float Width = 8f;

	/// <summary>The room the bar keeps from the frame's own edge.</summary>
	internal const float Inset = 2f;

	/// <summary>The bar's object name in the scene, so a log line or a dump can be tied to it.</summary>
	internal const string RootName = "CUO Online UI Scrollbar";

	private readonly RectTransform _root;
	private readonly RectTransform _handle;
	private readonly ScrollRect? _scroll;

	/// <summary>Whether the bar was shown the last time it was applied, so a page that starts or stops
	/// overflowing is not re-written every frame.</summary>
	private bool _shown;

	private OnlineUiPageScrollbar(RectTransform root, RectTransform handle, ScrollRect? scroll)
	{
		_root = root;
		_handle = handle;
		_scroll = scroll;
	}

	/// <summary>
	/// Builds the bar on the page's own band — a SIBLING of the viewport, so the page's mask does not clip it
	/// and the rows do not scroll it away — with the game's own sprite for its drawing. It starts hidden: a
	/// page that fits shows no bar.
	/// </summary>
	internal static OnlineUiPageScrollbar Create(RectTransform page, Sprite? sprite, Image.Type imageType, float pixelsPerUnit)
	{
		var root = OnlineUiSurfaceParts.CreateRect(RootName, page, typeof(Image));
		root.anchorMin = new Vector2(1f, 0f);
		root.anchorMax = new Vector2(1f, 1f);
		root.pivot = new Vector2(1f, 0.5f);
		root.offsetMin = new Vector2(-Width - Inset, Inset);
		root.offsetMax = new Vector2(-Inset, -Inset);

		var track = root.GetComponent<Image>();
		track.sprite = sprite;
		track.type = imageType;
		track.pixelsPerUnitMultiplier = pixelsPerUnit;
		track.color = TrackColor;
		// The track takes the pointer: a click on it is a jump to that part of the page, which is what a
		// scrollbar without arrow buttons is for.
		track.raycastTarget = true;

		var handle = OnlineUiSurfaceParts.CreateRect("Handle", root, typeof(Image));
		handle.anchorMin = new Vector2(0f, 1f);
		handle.anchorMax = new Vector2(1f, 1f);
		handle.pivot = new Vector2(0.5f, 1f);
		handle.offsetMin = new Vector2(0f, 0f);
		handle.offsetMax = new Vector2(0f, 0f);

		var handleImage = handle.GetComponent<Image>();
		handleImage.sprite = sprite;
		handleImage.type = imageType;
		handleImage.pixelsPerUnitMultiplier = pixelsPerUnit;
		handleImage.color = HandleColor;
		handleImage.raycastTarget = true;

		var scroll = page.GetComponent<ScrollRect>();
		var bar = new OnlineUiPageScrollbar(root, handle, scroll);
		handle.gameObject.AddComponent<OnlineUiPageScrollDrag>().Bind(bar);
		root.gameObject.AddComponent<OnlineUiPageScrollClick>().Bind(bar);
		return bar;
	}

	/// <summary>The track's own fill: the panel's dark value tone, so the empty part of the range is visible
	/// without competing with the handle.</summary>
	private static Color TrackColor => new(
		OnlineUiWindowLayout.ControlFillR,
		OnlineUiWindowLayout.ControlFillG,
		OnlineUiWindowLayout.ControlFillB,
		1f);

	/// <summary>The handle: the light tone the page's captions and the slider's handle use.</summary>
	private static Color HandleColor => new(0.92f, 0.93f, 0.94f, 0.95f);

	/// <summary>Whether the bar is on screen: the surface reports the page's own state, and a hidden bar has
	/// nothing to drag.</summary>
	internal bool IsShown => _shown;

	/// <summary>The page the bar describes, for the drag and the click to move.</summary>
	internal ScrollRect? Scroll => _scroll;

	/// <summary>
	/// Shows the bar for one frame of a page: hidden while the content fits the viewport, and otherwise the
	/// handle sized and placed from the page's own numbers. Every write is idempotent, and the visibility
	/// write is skipped unless it changed.
	/// </summary>
	internal void Apply()
	{
		if (_scroll is null || _scroll.content is null || _scroll.viewport is null)
		{
			SetShown(false);
			return;
		}

		var viewport = _scroll.viewport.rect.height;
		var content = _scroll.content.rect.height;
		var handle = OnlineUiPageScroll.For(viewport, content, _scroll.verticalNormalizedPosition);
		var needed = handle.Fraction < 1f;
		SetShown(needed);
		if (!needed)
		{
			return;
		}

		var track = _root.rect.height;
		var height = Mathf.Max(1f, track * handle.Fraction);
		var offset = (track - height) * handle.Offset;

		// Anchored to the top of the track: the offset is the room above the handle, which is what the rule's
		// own `Offset` means (0 at the top of the page, 1 at its end).
		_handle.sizeDelta = new Vector2(0f, height);
		_handle.anchoredPosition = new Vector2(0f, -offset);
	}

	/// <summary>Hides or shows the whole bar. It is called once per applied frame.</summary>
	internal void SetShown(bool shown)
	{
		if (_shown != shown)
		{
			_shown = shown;
			_root.gameObject.SetActive(shown);
		}
	}

	/// <summary>
	/// Where the pointer is along the track, as the 0-1 offset <see cref="OnlineUiPageScroll"/> speaks in (0
	/// at the top, 1 at the bottom). Null when the track has no height yet or the pointer cannot be mapped
	/// into it.
	/// </summary>
	internal float? OffsetOf(Vector2 screenPoint, Camera? camera)
	{
		var track = _root.rect.height;
		if (track <= 0f
			|| !RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screenPoint, camera, out var local))
		{
			return null;
		}

		// The track's own rect runs from -height/2 to +height/2 around its centre, and offset 0 is its TOP.
		var offset = (_root.rect.yMax - local.y) / track;
		return Mathf.Clamp01(offset);
	}

	/// <summary>
	/// Moves the page to the part one offset along the track names. It is the ONE write both gestures make, so
	/// a drag and a click cannot disagree with the bar that drew them, and it goes through the position the
	/// rule returns rather than through a delta of its own.
	/// </summary>
	internal void MoveTo(float offset)
	{
		if (_scroll != null)
		{
			_scroll.verticalNormalizedPosition = OnlineUiPageScroll.PositionFor(offset);
		}
	}
}
