using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Presentation state for the native remote medical (WoundView) focus. The
/// game's own health panel reads <c>WoundView.view.body</c>; while a remote
/// medical view is open the adapter points it at a dedicated display-only
/// body copy populated from the remote player's 1 Hz character snapshot. The
/// actual live remote render clone is never mutated, and no authority or
/// remote inventory surface is affected.
/// </summary>
internal static class RemoteMedicalView
{
	private static Body? _displayBody; // Unity object — ==
	private static ulong _targetSteamId;
	private static string _displayName = "";

	internal static Body? DisplayBody
	{
		get
		{
			// Unity object — == (a scene reload destroys the display body and
			// reference-comparison would miss it).
			if (_displayBody == null)
			{
				_displayBody = null;
				return null;
			}

			return _displayBody;
		}
	}

	internal static ulong TargetSteamId => _targetSteamId;

	internal static string DisplayName => _displayName;

	internal static bool IsOpen => DisplayBody != null;

	internal static bool IsNativeWoundViewOpen()
	{
		if (PlayerCamera.main == null) // Unity object — ==
		{
			return false;
		}

		var woundView = PlayerCamera.main.woundView;
		return woundView != null && woundView.activeSelf; // Unity object — ==
	}

	internal static void Open(Body displayBody, ulong steamId, string displayName)
	{
		Close();
		_displayBody = displayBody;
		_targetSteamId = steamId;
		_displayName = string.IsNullOrWhiteSpace(displayName) ? $"player-{steamId:X}" : displayName;
	}

	/// <summary>
	/// Closes the remote focus and releases the display-only body. If the
	/// native WoundView is still open it is toggled off first; if it is already
	/// closed (user pressed the panel key/ESC) this only cleans the temporary
	/// body and redirects the native panel back to the local body.
	/// </summary>
	internal static void Close()
	{
		var display = DisplayBody;
		var wasOpen = IsOpen;

		// Clear the static state BEFORE toggling the native panel: the toggle
		// postfix observes RemoteMedicalView.IsOpen and would otherwise re-enter
		// Close while this instance is still half-destroyed.
		_displayBody = null;
		_targetSteamId = 0;
		_displayName = "";

		if (!wasOpen)
		{
			return;
		}

		if (WoundView.view != null && PlayerCamera.main != null) // Unity object — ==
		{
			WoundView.view.body = PlayerCamera.main.body;
			PlayerCamera.main.selectedLimb = null;
		}

		if (IsNativeWoundViewOpen() && PlayerCamera.main != null) // Unity object — ==
		{
			PlayerCamera.main.ToggleWoundView(false);
		}

		if (display != null) // Unity object — ==
		{
			Object.Destroy(display.gameObject);
		}
	}
}
