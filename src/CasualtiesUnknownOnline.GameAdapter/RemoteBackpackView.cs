using CasualtiesUnknownOnline.GameAdapter.Character;
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
	private static bool _containerRefreshPending;
	private static int _containerRefreshDueFrame = -1;
	private static ulong _openContainerInstanceId;
	private static bool _readOnly;
	private static bool _readOnlyDrag;

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

	/// <summary>
	/// Whether the open focus is VIEW ONLY: the host's access rule opens a conscious
	/// owner's backpack for looking but refuses every operation on it. The native
	/// inventory buttons already draw the same state (red) for an unconscious body.
	/// </summary>
	internal static bool IsReadOnly => _readOnly;

	/// <summary>
	/// The read-only state the current proxy drag started under. It outlives the
	/// focus because the double-Tab transfer closes the remote view BEFORE the release:
	/// without it a drag that began on a view-only pack could release into the local
	/// backpack as a cross-player take. Set on every proxy pickup, cleared when the drag
	/// ends or the next one starts.
	/// </summary>
	internal static bool ReadOnlyDrag => _readOnlyDrag;

	internal static void SetReadOnly(bool readOnly) => _readOnly = readOnly;

	/// <summary>A display proxy was picked up: remember whether the view it came from was view only.</summary>
	internal static void BeginProxyDrag() => _readOnlyDrag = _readOnly;

	/// <summary>The proxy drag ended (released, refused or reset): a later gesture starts clean.</summary>
	internal static void EndProxyDrag() => _readOnlyDrag = false;

	internal static void Open(Body body, ulong steamId, string displayName, bool readOnly)
	{
		_focusedBody = body;
		_focusedSteamId = steamId;
		_focusedName = string.IsNullOrWhiteSpace(displayName) ? $"player-{steamId:X}" : displayName;
		_smoothPosition = Camera.main != null
			? (Vector2)Camera.main.WorldToScreenPoint(body.transform.position)
			: Vector2.zero;
		_readOnly = readOnly;
		_readOnlyDrag = false;
		PlayerCamera.main.radialOpen = true;
	}

	internal static void Close()
	{
		// Without any remote focus this is a no-op. This method is also called
		// from the per-frame stale check and from InvButtonBodyPatch while the
		// player's OWN native radial backpack is open; if it unconditionally
		// cleared radialOpen there, a normal Tab press would open the backpack
		// and then immediately close it (the reported instant-close bug).
		if (_focusedBody == null && _focusedSteamId == 0)
		{
			return;
		}

		// A display-proxy drag may outlive the focused view (the user closed the
		// backpack while holding an item). It is NOT cancelled here: the drag can
		// legally continue into the Tab-switch transfer path (close remote view,
		// open the local backpack, release into it). The release patch cancels
		// any proxy that is not consumed by that transfer before the native
		// release can move it into an authoritative body.
		_focusedBody = null;
		_focusedSteamId = 0;
		_focusedName = "";
		_openContainerInstanceId = 0;
		_containerRefreshPending = false;
		_containerRefreshDueFrame = -1;
		_readOnly = false;

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

	/// <summary>
	/// The remote clone's open container contents were re-rendered by the
	/// snapshot/event path. The native container window populates its buttons
	/// only when opened, so the next frame must rebuild them from the container's
	/// current children; deferred Unity destroys are then already processed.
	/// </summary>
	internal static void RequestOpenContainerRefresh()
	{
		if (IsOpen && PlayerCamera.main != null && PlayerCamera.main.currentContainer != null) // Unity objects — ==
		{
			_containerRefreshPending = true;
			_containerRefreshDueFrame = Time.frameCount + 1;
		}
	}

	internal static void TrackOpenRemoteContainer(Container container)
	{
		if (container == null) // Unity object — ==
		{
			return;
		}

		var marker = container.GetComponent<RemoteInventoryItemId>();
		_openContainerInstanceId = marker != null ? marker.Id : 0; // Unity object — ==
	}

	/// <summary>
	/// A remote display-proxy container that the native window may be showing is
	/// being removed/replaced by the clone renderer. Mark the open-window refresh
	/// as pending even when the new proxy is not yet the current container, so
	/// the next frame can re-bind by the tracked instance id and repopulate.
	/// </summary>
	internal static void NotifyOpenContainerProxyRemoved(Item removedItem)
	{
		if (removedItem == null) // Unity object — ==
		{
			return;
		}

		var container = removedItem.GetComponent<Container>();
		if (container == null) // Unity object — ==
		{
			return;
		}

		var marker = removedItem.GetComponent<RemoteInventoryItemId>();
		var isTracked = _openContainerInstanceId != 0
			&& marker != null
			&& marker.Id == _openContainerInstanceId; // Unity object — ==
		var isCurrent = PlayerCamera.main != null
			&& PlayerCamera.main.currentContainer != null
			&& PlayerCamera.main.currentContainer == container; // Unity objects — ==
		var containsOpen = ContainsTrackedOpenContainer(removedItem);

		if (isTracked || isCurrent || containsOpen)
		{
			_containerRefreshPending = true;
			_containerRefreshDueFrame = Time.frameCount + 1;
		}
	}

	private static bool ContainsTrackedOpenContainer(Item removedItem)
	{
		if (_openContainerInstanceId != 0)
		{
			foreach (var candidate in removedItem.GetComponentsInChildren<Container>(true))
			{
				if (candidate == null) // Unity object — ==
				{
					continue;
				}

				var marker = candidate.GetComponent<RemoteInventoryItemId>();
				if (marker != null && marker.Id == _openContainerInstanceId) // Unity object — ==
				{
					return true;
				}
			}
		}

		if (PlayerCamera.main != null && PlayerCamera.main.currentContainer != null) // Unity objects — ==
		{
			return PlayerCamera.main.currentContainer.transform.IsChildOf(removedItem.transform);
		}

		return false;
	}

	internal static void UpdatePendingContainerRefresh()
	{
		if (!_containerRefreshPending || Time.frameCount < _containerRefreshDueFrame)
		{
			return;
		}

		_containerRefreshPending = false;
		_containerRefreshDueFrame = -1;
		if (!IsOpen || PlayerCamera.main == null) // Unity objects — ==
		{
			return;
		}

		// A periodic render may have destroyed and recreated the whole container
		// proxy (a slot move/rehome). The native window still points at the old
		// destroyed object; use the tracked authoritative instance id to re-bind
		// it to the clone's current container proxy before repopulating.
		var focused = FocusedBody;
		if (focused == null) // Unity object — ==
		{
			return;
		}

		var container = PlayerCamera.main.currentContainer;
		if (container == null || !container.transform.IsChildOf(focused.transform)) // Unity objects — ==
		{
			container = FindTrackedRemoteContainer();
			if (container != null) // Unity object — ==
			{
				PlayerCamera.main.currentContainer = container;
			}
		}

		if (container == null) // Unity object — ==
		{
			return;
		}

		PlayerCamera.main.RepopulateContainer();
	}

	private static Container? FindTrackedRemoteContainer()
	{
		if (FocusedBody == null || _openContainerInstanceId == 0) // Unity object — ==
		{
			return null;
		}

		foreach (var container in FocusedBody.GetComponentsInChildren<Container>(true))
		{
			if (container == null) // Unity object — ==
			{
				continue;
			}

			var marker = container.GetComponent<RemoteInventoryItemId>();
			if (marker != null && marker.Id == _openContainerInstanceId) // Unity object — ==
			{
				return container;
			}
		}

		return null;
	}

	internal static void ClearIfStale()
	{
		if (!IsOpen)
		{
			Close();
		}
	}
}
