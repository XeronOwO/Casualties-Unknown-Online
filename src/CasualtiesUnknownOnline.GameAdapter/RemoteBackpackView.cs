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
		// A display-proxy drag may outlive the focused view (the user closed the
		// backpack while holding an item). It is NOT cancelled here: the drag can
		// legally continue into the Tab-switch transfer path (close remote view,
		// open the local backpack, release into it). The release patch cancels
		// any proxy that is not consumed by that transfer before the native
		// release can move it into an authoritative body.
		_focusedBody = null;
		_focusedSteamId = 0;
		_focusedName = "";

		// Closing the focus also exits the native radial-open state. This keeps
		// the remote backpack and remote medical views mutually exclusive: if the
		// user opens the medical panel while a remote backpack is open,
		// PlayerCamera.WoundViewButton can proceed without tripping the native
		// "radial menu already open" guard.
		if (PlayerCamera.main != null) // Unity object — ==
		{
			PlayerCamera.main.radialOpen = false;
		}
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
