using CasualtiesUnknownOnline.Runtime.OnlineUi;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Renders the transient co-op location pings as an IMGUI overlay. A ping on
/// screen is drawn at its projected world position; an off-screen ping is
/// pinned to the screen edge with a direction arrow and the pinger's name.
/// The only state read is the Runtime <see cref="ILocationPingControl"/>; this
/// class never owns or mutates ping semantics.
/// </summary>
internal static class LocationPingOverlay
{
	private const float ScreenEdgeMargin = 52f;
	private const int MarkerFontSize = 28;
	private const int OffScreenArrowFontSize = 22;
	private const int LabelFontSize = 13;
	private const float FadeMs = 1_000f;

	internal static void Draw(OnlineUiContext ctx)
	{
		var pings = ctx.LocationPings.ActivePings;
		if (pings.Count == 0)
		{
			return;
		}

		var camera = Camera.main;
		if (camera == null) // Unity object — ==
		{
			return;
		}

		var now = ctx.Time.NowMs;
		foreach (var ping in pings)
		{
			var remaining = ping.ExpiresAtMs - now;
			if (remaining <= 0)
			{
				continue;
			}

			var color = ToColor(ctx.PlayerColor(ping.SenderSteamId));
			color.a *= Mathf.Clamp01((float)remaining / FadeMs);

			var projected = camera.WorldToScreenPoint(new Vector3(ping.X, ping.Y, 0f));
			var gui = new Vector2(projected.x, Screen.height - projected.y);
			var placement = OffScreenArrowGeometry.Place(gui.x, gui.y, Screen.width, Screen.height, ScreenEdgeMargin);

			var name = ctx.DisplayName(ping.SenderSteamId);
			if (placement.Direction == OffScreenArrowDirection.None)
			{
				DrawOnScreen(placement.X, placement.Y, ping.Kind, name, color);
			}
			else
			{
				DrawOffScreen(placement, ping.Kind, name, color);
			}
		}
	}

	private static void DrawOnScreen(float x, float y, LocationPingKind kind, string name, Color color)
	{
		var markerStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = MarkerFontSize,
			alignment = TextAnchor.MiddleCenter,
		};
		markerStyle.normal.textColor = color;
		GUI.Label(new Rect(x - 20f, y - 20f, 40f, 40f), kind == LocationPingKind.Exclamation ? "!" : "●", markerStyle);

		var nameStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = LabelFontSize,
			alignment = TextAnchor.MiddleCenter,
		};
		nameStyle.normal.textColor = color;
		GUI.Label(new Rect(x - 70f, y + 22f, 140f, 20f), name, nameStyle);
	}

	private static void DrawOffScreen(OffScreenArrowPlacement placement, LocationPingKind kind, string name, Color color)
	{
		var arrowStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = OffScreenArrowFontSize,
			alignment = TextAnchor.MiddleCenter,
		};
		arrowStyle.normal.textColor = color;

		const float arrowSize = 32f;
		var arrow = placement.Direction switch
		{
			OffScreenArrowDirection.Up => "\u25B2",   // ▲
			OffScreenArrowDirection.Down => "\u25BC", // ▼
			OffScreenArrowDirection.Left => "\u25C0", // ◄
			OffScreenArrowDirection.Right => "\u25B6", // ►
			_ => "\u2022",                            // •
		};
		GUI.Label(new Rect(placement.X - (arrowSize * 0.5f), placement.Y - (arrowSize * 0.5f), arrowSize, arrowSize), arrow, arrowStyle);

		var tag = kind == LocationPingKind.Exclamation ? "!" : "●";
		var nameStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = LabelFontSize,
			alignment = TextAnchor.MiddleCenter,
		};
		nameStyle.normal.textColor = color;
		GUI.Label(new Rect(placement.X - 80f, placement.Y + (arrowSize * 0.5f) + 4f, 160f, 20f), $"{name} {tag}", nameStyle);
	}

	private static Color ToColor(PlayerColorValue value) => new(value.R, value.G, value.B, value.A);
}
