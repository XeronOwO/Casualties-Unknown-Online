using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Captures the in-world middle-click gesture and turns it into a local
/// location-ping placement. UI/modal surfaces are excluded, the session must
/// be active and in-world, and the Runtime domain owns the double-click rule.
/// </summary>
internal sealed class LocationPingInputHandler(
	SessionService session,
	ILocationPingControl locationPings,
	OnlineUiOverlay overlay,
	ILogger<LocationPingInputHandler> log)
{
	private readonly SessionService _session = session;
	private readonly ILocationPingControl _locationPings = locationPings;
	private readonly OnlineUiOverlay _overlay = overlay;
	private readonly ILogger<LocationPingInputHandler> _log = log;

	internal bool TryHandle()
	{
		if (!Input.GetMouseButtonDown(2))
		{
			return false;
		}

		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return false;
		}

		if (_overlay.IsPointerOverUi(Input.mousePosition))
		{
			return false;
		}

		var camera = Camera.main;
		if (camera == null) // Unity object — ==
		{
			return false;
		}

		var world = camera.ScreenToWorldPoint(Input.mousePosition);
		var placed = _locationPings.TryPlace(world.x, world.y);
		if (placed)
		{
			_log.LogDebug("[LocationPing] middle-click captured at ({X:F1},{Y:F1}).", world.x, world.y);
		}

		return placed;
	}
}
