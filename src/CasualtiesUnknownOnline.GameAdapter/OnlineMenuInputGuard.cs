using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.OnlineUi;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Suppresses background game UI input while the CUO Online UI modal window is
/// open. The Online UI is IMGUI, so Unity's UGUI EventSystem does not know it
/// is covering the screen; without this guard, clicks on the window's blank
/// areas fall through to the game menu/world behind it. This guard disables
/// the game's custom <see cref="AdaptiveButton"/> input and adds transparent
/// UGUI raycast blockers on active screen-space canvases, then restores the
/// original state when the modal closes.
///
/// <para>
/// Everything here is aimed at the GAME's UI, never at CUO's own (ticket
/// online-ui-art-and-controls-overhaul, S4): the surfaces that migrated onto the
/// game's canvas are uGUI now and block their own pixels, so a blocker over CUO's
/// own canvas would cover the launcher and the window and swallow every click the
/// player makes on them. Every sweep therefore asks
/// <see cref="OnlineUiSurfaceMarker"/>, and the surface marks its root.
/// </para>
/// </summary>
internal sealed class OnlineMenuInputGuard(
	ISessionControl session,
	ILogger<OnlineMenuInputGuard> log) : ISessionSurfacePatchBridge
{
	private readonly ISessionControl _session = session;
	private readonly ILogger<OnlineMenuInputGuard> _log = log;
	private readonly List<AdaptiveButton> _buttons = [];
	private readonly List<GameObject> _blockers = [];
	private readonly List<GameObject> _scopedBlockers = [];
	private IReadOnlyList<OnlineUiBlockRect> _scopedBlocks = [];

	private bool _modal;
	private bool _nonModalEscapeSurfaceOpen;

	public bool IsOnlineUiModalOpen => _modal;

	public bool IsNonModalEscapeSurfaceOpen => _nonModalEscapeSurfaceOpen;

	internal void SetNonModalEscapeSurfaceVisible(bool visible) => _nonModalEscapeSurfaceOpen = visible;
	internal void SetModal(bool modal)
	{
		if (_modal == modal)
		{
			return;
		}

		_modal = modal;
		if (modal)
		{
			BeginModal();
		}
		else
		{
			EndModal();
		}
	}

	/// <summary>Sets the non-modal CUO Online UI rectangles that should block
	/// background UGUI raycasts. Empty clears all scoped blockers.</summary>
	internal void SetScopedBlocks(IReadOnlyList<OnlineUiBlockRect> blocks)
	{
		if (ScopedBlocksEqual(_scopedBlocks, blocks))
		{
			return;
		}

		_scopedBlocks = [.. blocks];
		DestroyScopedBlockers();
		if (_scopedBlocks.Count > 0)
		{
			var canvasCount = CreateScopedBlockers();
			_log.LogDebug(
				"Online UI scoped blocks set: {RectCount} rectangle(s) blocked on {CanvasCount} screen-space canvas(es).",
				_scopedBlocks.Count,
				canvasCount);
		}
	}

	/// <summary>Pump: pick up menu buttons created after the modal opened.</summary>
	internal void Update()
	{
		if (_modal)
		{
			CaptureAdaptiveButtons();
		}
	}

	private void BeginModal()
	{
		var buttonCount = CaptureAdaptiveButtons();
		var canvasCount = CreateRaycastBlockers();
		_log.LogInformation(
			"Online UI modal open — background UI input blocked: {ButtonCount} game button(s) disabled, {CanvasCount} screen-space canvas(es) covered; CUO's own surface is never blocked by its own guard.",
			buttonCount,
			canvasCount);
	}

	private void EndModal()
	{
		RestoreAdaptiveButtons();
		DestroyRaycastBlockers();
		_log.LogInformation("Online UI modal closed — background UI input restored.");
	}

	/// <summary>
	/// The game's custom menu buttons, CUO's own surface excepted. A control CUO built (or instantiated
	/// from a game prefab) is CUO's to keep usable: disabling it would make the very surface the player
	/// is looking at unresponsive.
	/// </summary>
	private int CaptureAdaptiveButtons()
	{
		var captured = 0;
		foreach (var button in Object.FindObjectsOfType<AdaptiveButton>())
		{
			if (button == null || !button.enabled || OnlineUiSurfaceMarker.IsInside(button)) // Unity object — ==
			{
				continue;
			}

			_buttons.Add(button);
			button.enabled = false;
			captured++;
		}

		return captured;
	}

	private void RestoreAdaptiveButtons()
	{
		foreach (var button in _buttons)
		{
			if (button == null) // Unity object — ==
			{
				continue;
			}

			button.enabled = ShouldBeEnabled(button);
		}

		_buttons.Clear();
	}

	private bool ShouldBeEnabled(AdaptiveButton button)
	{
		var guestBlocked = _session.Role == SessionRole.Guest
			&& _session.HostSteamId != 0
			&& button.action is AdaptiveButton.MenuAction.Play or AdaptiveButton.MenuAction.Tutorial;
		return !guestBlocked;
	}

	/// <summary>
	/// Whether CUO's own guard may put a blocker on this canvas: an active screen-space canvas that is
	/// NOT part of CUO's own surface. The ownership clause is the S4 retirement — CUO's surface is uGUI
	/// and answers the EventSystem itself, so a blocker laid over its canvas would sit above the
	/// launcher and the window and take every click meant for them.
	/// </summary>
	private static bool IsBlockable(Canvas canvas)
	{
		// Unity object — ==
		return canvas != null
			&& canvas.gameObject.activeInHierarchy
			&& canvas.renderMode != RenderMode.WorldSpace
			&& !OnlineUiSurfaceMarker.IsInside(canvas);
	}

	private int CreateRaycastBlockers()
	{
		var created = 0;
		foreach (var canvas in Object.FindObjectsOfType<Canvas>())
		{
			if (!IsBlockable(canvas))
			{
				continue;
			}

			var blocker = new GameObject("CUO Online Input Blocker")
			{
				layer = canvas.gameObject.layer,
			};
			var rect = blocker.AddComponent<RectTransform>();
			rect.SetParent(canvas.transform, false);
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			blocker.transform.SetAsLastSibling();

			var image = blocker.AddComponent<Image>();
			image.raycastTarget = true;
			image.color = new Color(0f, 0f, 0f, 0f);
			_blockers.Add(blocker);
			created++;
		}

		return created;
	}

	private void DestroyRaycastBlockers()
	{
		foreach (var blocker in _blockers)
		{
			if (blocker != null) // Unity object — ==
			{
				Object.Destroy(blocker);
			}
		}

		_blockers.Clear();
	}

	private int CreateScopedBlockers()
	{
		var created = 0;
		foreach (var canvas in Object.FindObjectsOfType<Canvas>())
		{
			if (!IsBlockable(canvas))
			{
				continue;
			}

			var blocker = new GameObject("CUO Online Scoped Input Blocker")
			{
				layer = canvas.gameObject.layer,
			};
			var rect = blocker.AddComponent<RectTransform>();
			rect.SetParent(canvas.transform, false);
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			blocker.transform.SetAsLastSibling();

			var image = blocker.AddComponent<Image>();
			image.raycastTarget = true;
			image.color = new Color(0f, 0f, 0f, 0f);
			var filter = blocker.AddComponent<OnlineScopedRaycastFilter>();
			filter.SetBlocks(_scopedBlocks);
			_scopedBlockers.Add(blocker);
			created++;
		}

		return created;
	}

	private void DestroyScopedBlockers()
	{
		foreach (var blocker in _scopedBlockers)
		{
			if (blocker != null) // Unity object — ==
			{
				Object.Destroy(blocker);
			}
		}

		_scopedBlockers.Clear();
	}

	private static bool ScopedBlocksEqual(
		IReadOnlyList<OnlineUiBlockRect> current,
		IReadOnlyList<OnlineUiBlockRect> next)
	{
		if (current.Count != next.Count)
		{
			return false;
		}

		for (var i = 0; i < current.Count; i++)
		{
			if (current[i] != next[i])
			{
				return false;
			}
		}

		return true;
	}
}
