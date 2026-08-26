using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The read-only native-backpack view focus. The game's own radial inventory
/// UI is hard-wired to <c>PlayerCamera.main.body</c>; to reuse that UI for a
/// remote player's inventory without hijacking the whole camera/body, the CUO
/// patches ask this static view which remote clone's slots should be rendered.
/// The focus is presentation-only: remote clones are display proxies, so the
/// native UI must never be allowed to mutate the focused clone.
/// </summary>
internal static class RemoteBackpackView
{
	private static Body? _focusedBody; // Unity object — ==
	private static ulong _focusedSteamId;
	private static string _focusedName = "";
	private static Vector2 _smoothPosition;

	internal static Body? FocusedBody
	{
		get
		{
			// Unity object — == (a scene reload destroys the clone and is null
			// in the operator).
			if (_focusedBody == null)
			{
				_focusedBody = null;
				return null;
			}

			return _focusedBody;
		}
	}

	internal static ulong FocusedSteamId => _focusedSteamId;

	internal static string FocusedName => _focusedName;

	internal static Vector2 SmoothPosition => _smoothPosition;

	internal static bool IsOpen => FocusedBody != null
		&& PlayerCamera.main != null // Unity object — ==
		&& PlayerCamera.main.radialOpen;

	internal static void Open(Body body, ulong steamId, string displayName)
	{
		_focusedBody = body;
		_focusedSteamId = steamId;
		_focusedName = string.IsNullOrWhiteSpace(displayName) ? $"player-{steamId:X}" : displayName;
		_smoothPosition = Camera.main != null
			? (Vector2)Camera.main.WorldToScreenPoint(body.transform.position)
			: Vector2.zero;
		PlayerCamera.main.radialOpen = true;
	}

	internal static void Close()
	{
		_focusedBody = null;
		_focusedSteamId = 0;
		_focusedName = "";
	}

	internal static void UpdateSmoothPosition(Vector2 target) =>
		_smoothPosition = Vector2.Lerp(_smoothPosition, target, 5f * Time.deltaTime);

	internal static void ClearIfStale()
	{
		if (!IsOpen)
		{
			Close();
		}
	}
}
