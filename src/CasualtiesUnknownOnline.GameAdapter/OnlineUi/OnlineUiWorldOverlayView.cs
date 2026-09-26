using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// The world-space overlays on CUO's own canvas (ticket online-ui-art-and-controls-overhaul, S6): the
/// nameplates and off-screen arrows that follow the remote players, the location pings, and the network
/// readout. They are labels of the game's own font on the canvas the launcher, the window and the panels
/// already stand on, instead of IMGUI text drawn with the skin's built-in font.
///
/// <para>
/// The projection is the reason this type exists at all: a marker's position is a world point
/// (<see cref="OnlineUiWorldMarker"/>), and only the adapter may reach the camera. Everything after the
/// projection is the Runtime's own geometry — <see cref="OffScreenArrowGeometry"/> decides on-screen or
/// edge and where the edge is, <see cref="NameplateLayout"/> the box above a head,
/// <see cref="OffScreenArrowText"/> the arrow's mark — so what this view adds is the conversion into the
/// canvas and the label that then sits there.
/// </para>
///
/// <para>
/// The whole overlay works in the CANVAS's own units, never in screen pixels: the projected point is
/// converted into the canvas once, and the geometry rule is asked with the canvas's own rect, its own
/// margin and its own size. The surface is scaled by the game's UI scale, so a rule asked with
/// <c>Screen.width</c> beside a canvas-unit font size would only hold at a canvas scale of 1 — the defect
/// the S5 review found in the panel clamp, applied here before it can happen (a mutation of the REAL
/// source is what holds it, in <c>OnlineUiWorldOverlayPinTests</c>).
/// </para>
///
/// <para>
/// These are labels, not drawings, so their FIT is stated rather than left to TMP's defaults: one line (a
/// wrapped name would move the marker's own box), no auto-sizing (the size is the game's, and it must not
/// shrink and grow as names come and go), and overflow rather than clipping or an ellipsis — a name the
/// player cannot read is worse than one that spills past its box. The marks themselves come from the
/// Geometric Shapes block, which a font asset is free not to carry, so the surface asks the game's font
/// for each mark and falls back to ASCII (<c>^ v &lt; &gt;</c> arrows and <c>*</c>), warning once per
/// mark: which of them the game's own asset carries is a run fact, and the fallback is what keeps a
/// marker readable either way.
/// </para>
///
/// <para>
/// The overlay takes NO input, and that is a property of its shape rather than a setting: the layer is a
/// bare <see cref="RectTransform"/> with no graphic to hit, every label it builds is
/// <c>raycastTarget = false</c>, and the view reports nothing back — the two world input paths and the
/// pointer census are exactly as S4 and S5 left them. It also draws BEHIND CUO's own panels (the layer is
/// the canvas's first child), because a nameplate belongs over the world and not over the window the
/// player has open.
/// </para>
/// </summary>
internal sealed class OnlineUiWorldOverlayView
{
	/// <summary>The layer's object name in the scene.</summary>
	internal const string LayerName = "CUO Online UI World Overlay";

	/// <summary>The margin markers keep from the screen's edge, in the canvas's own units — the IMGUI
	/// overlay's own 52 px, which the rule and the arrow box need to be different for (the arrow keeps an
	/// inner padding from the edge, where the game's UI can sit).</summary>
	internal const float ScreenEdgeMargin = 52f;

	/// <summary>The arrow's own box and font size: a symbol, not text, so it does not come from the game's
	/// type ladder — the size the IMGUI markers drew at.</summary>
	internal const float ArrowGlyphFontSize = 22f;

	internal const float ArrowBoxSize = 32f;

	/// <summary>The gap between the arrow's box and the label under it.</summary>
	internal const float ArrowLabelGap = 4f;

	internal const float ArrowLabelWidth = 160f;

	internal const float ArrowLabelHeight = 20f;

	/// <summary>The location ping's mark: bigger than an arrow, because it is the thing the player looks
	/// for, and its label sits under the mark itself.</summary>
	internal const float PingGlyphFontSize = 28f;

	internal const float PingGlyphBox = 40f;

	internal const float PingLabelWidth = 140f;

	internal const float PingLabelHeight = 20f;

	/// <summary>The gap between the ping's mark and the pinger's name under it.</summary>
	internal const float PingLabelOffsetY = 22f;

	/// <summary>The network readout: the canvas's top-left corner, one line per row, with no background of
	/// its own (the game shows the hand-held item there — the reason the IMGUI HUD drew text only).</summary>
	internal const float HudMargin = 8f;

	internal const float HudRowHeight = 20f;

	internal const float HudWidth = 220f;

	private readonly ILogger _log;
	private readonly GameObject _root;
	private readonly RectTransform _rect;
	private readonly Canvas? _canvas;
	private readonly OnlineUiControlView.Typography _typography;
	private readonly TextMeshProUGUI _hudRtt;
	private readonly TextMeshProUGUI _hudStatus;
	private readonly List<Marker> _markers = [];
	private readonly HashSet<string> _reportedMissingGlyphs = [];

	private bool _reportedMissingCamera;
	private bool _reportedRefusedProjection;

	private bool _hudShown;
	private bool _statusShown;

	private OnlineUiWorldOverlayView(
		ILogger log,
		GameObject root,
		RectTransform rect,
		Canvas? canvas,
		OnlineUiControlView.Typography typography,
		TextMeshProUGUI hudRtt,
		TextMeshProUGUI hudStatus)
	{
		_log = log;
		_root = root;
		_rect = rect;
		_canvas = canvas;
		_typography = typography;
		_hudRtt = hudRtt;
		_hudStatus = hudStatus;
	}

	/// <summary>
	/// Builds the overlay layer under CUO's canvas and puts it FIRST among the canvas's children, so the
	/// marker labels draw behind the launcher, the window and the two panels. Its font — and the size ladder
	/// every label on it uses — come from the game's own row prefab, read through the same
	/// <see cref="OnlineUiControlFactory.ReadRowTemplate"/> the window and the panels read.
	/// </summary>
	internal static OnlineUiWorldOverlayView Create(Transform parent, ILogger log)
	{
		var root = new GameObject(LayerName, typeof(RectTransform));
		root.transform.SetParent(parent, worldPositionStays: false);
		var rect = (RectTransform)root.transform;
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;

		OnlineUiControlFactory.ReadRowTemplate(root.transform, out var typography, out _, out _, out _);

		var hud = CreateHudLabel("Network HUD", root.transform, typography, new Vector2(HudMargin, -HudMargin));
		var status = CreateHudLabel("Session Status", root.transform, typography, new Vector2(HudMargin, -(HudMargin + HudRowHeight)));
		status.gameObject.SetActive(false);

		var view = new OnlineUiWorldOverlayView(
			log,
			root,
			rect,
			root.GetComponentInParent<Canvas>()?.rootCanvas,
			typography,
			hud,
			status);
		// Behind CUO's own panels: the canvas's other children are built after this one, and a nameplate
		// over an open window would be the wrong way round.
		root.transform.SetAsFirstSibling();
		return view;
	}

	/// <summary>Applies one frame's overlay: the readout and the markers, each written only where it
	/// differs from what is already shown.</summary>
	internal void Apply(OnlineUiWorldOverlay overlay)
	{
		ApplyHud(overlay.Hud);
		ApplyMarkers(overlay.Markers);
	}

	internal void Destroy() => Object.Destroy(_root);

	private void ApplyHud(OnlineUiNetworkHud? hud)
	{
		var shown = hud is not null;
		if (_hudShown != shown)
		{
			_hudShown = shown;
			_hudRtt.gameObject.SetActive(shown);
		}

		if (hud is not { } model)
		{
			if (_statusShown)
			{
				_statusShown = false;
				_hudStatus.gameObject.SetActive(false);
			}

			return;
		}

		SetText(_hudRtt, model.RttText);
		_hudRtt.color = ToColor(model.RttColor);

		// The status line exists only while the plugin's own delayed-notification window says so: the model
		// says "nothing to show" with a null text, and the label waits instead of being rebuilt.
		var statusShown = !string.IsNullOrEmpty(model.StatusText);
		if (_statusShown != statusShown)
		{
			_statusShown = statusShown;
			_hudStatus.gameObject.SetActive(statusShown);
		}

		if (statusShown)
		{
			SetText(_hudStatus, model.StatusText!);
			_hudStatus.color = ToColor(model.StatusColor);
		}
	}

	/// <summary>
	/// Places one marker per model entry, in order: the ones this view already has are reused (a marker
	/// moves every frame, so its labels are what is kept and its text is written only when it changed), the
	/// rest are built, and anything the model dropped is hidden rather than destroyed — a player walking out
	/// of the roster and back must not rebuild labels every time.
	///
	/// <para>
	/// A marker the canvas cannot answer for is SKIPPED, not the end of the pass: the pool is filled by its
	/// own cursor, so one unprojectable world point hides that one marker and leaves every other one — the
	/// nameplates and the pings alike — where it belongs. The IMGUI pass projected the two families in two
	/// passes and could not couple them; this keeps the same independence.
	/// </para>
	/// </summary>
	private void ApplyMarkers(IReadOnlyList<OnlineUiWorldMarker> markers)
	{
		// A frame the surface never built (the struct's default value) carries no list at all: nothing to
		// show, which is the state an empty list is in.
		if (markers is null)
		{
			HideMarkersFrom(0);
			return;
		}

		// Unity object — == (a destroyed camera is not null by the C# rule, and a marker placed with a dead
		// camera would land wherever the transform happens to be). The lookup is asked per frame rather than
		// cached: the surface outlives the scene its camera belongs to, so a destroyed one must be
		// re-resolved instead of held.
		var camera = Camera.main;
		var bounds = _rect.rect;
		var placed = 0;
		if (camera != null)
		{
			for (var index = 0; index < markers.Count; index++)
			{
				if (!TryProject(camera, bounds, markers[index], out var point))
				{
					continue;
				}

				var marker = placed < _markers.Count ? _markers[placed] : AddMarker();
				Place(marker, markers[index], point, bounds);
				placed++;
			}
		}
		else
		{
			ReportMissingCamera();
		}

		HideMarkersFrom(placed);
	}

	/// <summary>Hides every pooled marker from <paramref name="first"/> on — the tail a frame does not
	/// fill.</summary>
	private void HideMarkersFrom(int first)
	{
		for (var index = first; index < _markers.Count; index++)
		{
			_markers[index].Hide();
		}
	}

	/// <summary>One line per surface, at Debug: the game has no main camera while a menu is up, which is the
	/// ordinary state before a run loads, so this must be neither a warning nor a line per frame.</summary>
	private void ReportMissingCamera()
	{
		if (_reportedMissingCamera)
		{
			return;
		}

		_reportedMissingCamera = true;
		_log.LogDebug("Online UI world overlay: the game has no main camera yet — the markers wait for one.");
	}

	/// <summary>
	/// One warning per surface: the canvas refused a world point, so that marker is hidden rather than
	/// placed. A warning, once, because this is the one branch that makes a marker vanish while the game
	/// runs, and the likely reason (a canvas mode whose camera cannot see the point) is worth a line.
	/// </summary>
	private void ReportRefusedProjection()
	{
		if (_reportedRefusedProjection)
		{
			return;
		}

		_reportedRefusedProjection = true;
		_log.LogWarning(
			"Online UI world overlay: CUO's canvas could not map a world point onto itself — that marker is hidden for this frame; check the canvas's render mode against the camera the game is using.");
	}

	/// <summary>
	/// A world position as a point in the canvas, in the canvas's OWN units and in the top-left-origin
	/// convention <see cref="OffScreenArrowGeometry"/> and <see cref="NameplateLayout"/> are written in.
	/// False means the canvas cannot answer for this point (its transform is not on a plane the screen
	/// point maps onto), which is a marker that has nowhere to go this frame.
	/// </summary>
	private bool TryProject(Camera camera, Rect bounds, in OnlineUiWorldMarker marker, out Vector2 point)
	{
		var screen = camera.WorldToScreenPoint(new Vector3(marker.X, marker.Y, 0f));
		var cameraOfCanvas = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, new Vector2(screen.x, screen.y), cameraOfCanvas, out var local))
		{
			point = default;
			ReportRefusedProjection();
			return false;
		}

		// The layer's own rect IS the screen (CUO's canvas covers the game's), so its bounds are the screen
		// in canvas units and the Y flip is the difference between a canvas (Y up, origin at the pivot) and
		// the geometry rules (Y down, origin at the top-left).
		point = new Vector2(local.x - bounds.xMin, bounds.yMax - local.y);
		return true;
	}

	private void Place(Marker item, in OnlineUiWorldMarker marker, Vector2 point, Rect bounds)
	{
		var placement = OffScreenArrowGeometry.Place(point.x, point.y, bounds.width, bounds.height, ScreenEdgeMargin);
		var color = ToColor(marker.Color);
		if (placement.Direction == OffScreenArrowDirection.None)
		{
			if (marker.Kind == OnlineUiWorldMarkerKind.Nameplate)
			{
				// Above the head: the Runtime's own box, so the gap between the name and the head stays the
				// rule's rather than this view's.
				var box = NameplateLayout.AboveHead(placement.X, placement.Y);
				item.HideGlyph();
				item.Label(marker.OnScreenText, color, _typography.SizeOf(OnlineUiTextStyle.Default));
				item.PlaceLabel(Centre(box.X, box.Y, box.Width, box.Height), new Vector2(box.Width, box.Height), bounds);
				return;
			}

			// A ping draws its own mark at the point and the pinger's name under it.
			item.Glyph(Drawable(marker.Glyph), color, PingGlyphFontSize);
			item.PlaceGlyph(new Vector2(placement.X, placement.Y), new Vector2(PingGlyphBox, PingGlyphBox), bounds);
			item.Label(marker.OnScreenText, color, _typography.SizeOf(OnlineUiTextStyle.Muted));
			item.PlaceLabel(
				new Vector2(placement.X, placement.Y + PingLabelOffsetY + (PingLabelHeight * 0.5f)),
				new Vector2(PingLabelWidth, PingLabelHeight),
				bounds);
			return;
		}

		// Off screen: the Runtime's arrow, then the label that belongs to that form of the marker.
		item.Glyph(Drawable(OffScreenArrowText.Glyph(placement.Direction)), color, ArrowGlyphFontSize);
		item.PlaceGlyph(new Vector2(placement.X, placement.Y), new Vector2(ArrowBoxSize, ArrowBoxSize), bounds);
		item.Label(marker.OffScreenText, color, _typography.SizeOf(OnlineUiTextStyle.Muted));
		item.PlaceLabel(
			new Vector2(placement.X, placement.Y + (ArrowBoxSize * 0.5f) + ArrowLabelGap + (ArrowLabelHeight * 0.5f)),
			new Vector2(ArrowLabelWidth, ArrowLabelHeight),
			bounds);
	}

	private Marker AddMarker()
	{
		var marker = new Marker(_root.transform, _typography);
		_markers.Add(marker);
		return marker;
	}

	/// <summary>One marker's two labels: the mark drawn at the point (an arrow, or a ping's own glyph) and
	/// the text beside it. Either may be unused for a frame — a nameplate on screen draws no mark at all —
	/// so each is hidden rather than destroyed, and nothing is written that did not change.</summary>
	private sealed class Marker
	{
		private readonly TextMeshProUGUI _glyph;
		private readonly TextMeshProUGUI _label;
		private string _glyphText = "\0";
		private string _labelText = "\0";
		private float _glyphSize = float.NaN;
		private float _labelSize = float.NaN;
		private bool _glyphShown;

		internal Marker(Transform parent, OnlineUiControlView.Typography typography)
		{
			_glyph = CreateLabel("Marker Glyph", parent, typography, TextAlignmentOptions.Center);
			_label = CreateLabel("Marker Label", parent, typography, TextAlignmentOptions.Center);
			Hide();
		}

		internal void Hide()
		{
			_glyphShown = false;
			if (_glyph.gameObject.activeSelf)
			{
				_glyph.gameObject.SetActive(false);
			}

			if (_label.gameObject.activeSelf)
			{
				_label.gameObject.SetActive(false);
			}
		}

		internal void Glyph(string text, Color color, float size)
		{
			_glyphShown = true;
			if (!_glyph.gameObject.activeSelf)
			{
				_glyph.gameObject.SetActive(true);
			}

			if (_glyphText != text)
			{
				_glyphText = text;
				_glyph.text = text;
			}

			if (!_glyphSize.Equals(size))
			{
				_glyphSize = size;
				_glyph.fontSize = size;
			}

			_glyph.color = color;
		}

		internal void HideGlyph()
		{
			if (_glyphShown)
			{
				_glyphShown = false;
				_glyph.gameObject.SetActive(false);
			}
		}

		internal void Label(string text, Color color, float size)
		{
			if (!_label.gameObject.activeSelf)
			{
				_label.gameObject.SetActive(true);
			}

			if (_labelText != text)
			{
				_labelText = text;
				_label.text = text;
			}

			if (!_labelSize.Equals(size))
			{
				_labelSize = size;
				_label.fontSize = size;
			}

			_label.color = color;
		}

		internal void PlaceGlyph(Vector2 centre, Vector2 size, Rect bounds) => Place(_glyph, centre, size, bounds);

		internal void PlaceLabel(Vector2 centre, Vector2 size, Rect bounds) => Place(_label, centre, size, bounds);

		/// <summary>A label's own box: the centre it is centered on, its size, and where that centre is in
		/// the layer's local space (the bounds' Y grows UP, the geometry rules' Y grows down).</summary>
		private static void Place(TextMeshProUGUI label, Vector2 centre, Vector2 size, Rect bounds)
		{
			var rect = (RectTransform)label.transform;
			rect.sizeDelta = size;
			rect.localPosition = new Vector3(bounds.xMin + centre.x, bounds.yMax - centre.y, 0f);
		}
	}

	/// <summary>The centre of a box given by its top-left corner, the way
	/// <see cref="NameplateLayout.AboveHead"/> hands it over.</summary>
	private static Vector2 Centre(float x, float y, float width, float height) =>
		new(x + (width * 0.5f), y + (height * 0.5f));

	/// <summary>The readout's own labels: anchored to the top-left corner of the canvas, left-aligned, and
	/// never a raycast target — the overlay takes no input.</summary>
	private static TextMeshProUGUI CreateHudLabel(
		string name,
		Transform parent,
		OnlineUiControlView.Typography typography,
		Vector2 anchoredPosition)
	{
		var label = CreateLabel(name, parent, typography, TextAlignmentOptions.MidlineLeft);
		var rect = (RectTransform)label.transform;
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(0f, 1f);
		rect.pivot = new Vector2(0f, 1f);
		rect.anchoredPosition = anchoredPosition;
		rect.sizeDelta = new Vector2(HudWidth, HudRowHeight);
		label.fontSize = typography.SizeOf(OnlineUiTextStyle.Muted);
		label.gameObject.SetActive(false);
		return label;
	}

	/// <summary>
	/// The mark as the game's own font can draw it: a mark the asset cannot render would come out as a
	/// missing glyph, so the font is asked once per mark and an ASCII stand-in is drawn instead, with one
	/// warning naming both. The font asset's fallback chain is searched, because a glyph a fallback carries
	/// renders fine.
	/// </summary>
	private string Drawable(string glyph)
	{
		if (glyph.Length == 0 || _typography.Font is not { } font || font.HasCharacter(glyph[0], searchFallbacks: true))
		{
			return glyph;
		}

		var fallback = AsciiFallback(glyph);
		if (_reportedMissingGlyphs.Add(glyph))
		{
			_log.LogWarning(
				"Online UI world overlay: the game's own font asset has no glyph for `{Glyph}` — drawing `{Fallback}` in its place.",
				glyph,
				fallback);
		}

		return fallback;
	}

	/// <summary>The ASCII stand-in for each mark this overlay draws: the four arrows, and one star for
	/// everything else (the ping's dot, or a mark nobody expects).</summary>
	private static string AsciiFallback(string glyph) => glyph switch
	{
		OffScreenArrowText.Up => "^",
		OffScreenArrowText.Down => "v",
		OffScreenArrowText.Left => "<",
		OffScreenArrowText.Right => ">",
		_ => "*",
	};

	/// <summary>A label on the game's own font, at the size the game's row prefab implies for the style —
	/// the same construction the window's and the panels' labels use.</summary>
	private static TextMeshProUGUI CreateLabel(
		string name,
		Transform parent,
		OnlineUiControlView.Typography typography,
		TextAlignmentOptions alignment)
	{
		var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
		go.transform.SetParent(parent, worldPositionStays: false);
		var text = go.GetComponent<TextMeshProUGUI>();
		text.alignment = alignment;
		text.fontSize = typography.SizeOf(OnlineUiTextStyle.Muted);
		text.fontStyle = OnlineUiControlView.Typography.WeightOf(OnlineUiTextStyle.Muted);
		// The fit policy, stated rather than inherited: one line, no auto-sizing, and overflow instead of a
		// clip or an ellipsis (see the class doc).
		text.enableWordWrapping = false;
		text.enableAutoSizing = false;
		text.overflowMode = TextOverflowModes.Overflow;
		// The overlay takes no input: a marker must never swallow a click meant for the world.
		text.raycastTarget = false;
		if (typography.Font != null)
		{
			text.font = typography.Font;
		}

		return text;
	}

	private static void SetText(TextMeshProUGUI label, string text)
	{
		if (!string.Equals(label.text, text, StringComparison.Ordinal))
		{
			label.text = text;
		}
	}

	private static Color ToColor(OnlineUiNativeRgba color) => new(color.R, color.G, color.B, color.A);
}
