using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.OnlineUi;
using CasualtiesUnknownOnline.Runtime.Session;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Suppresses background game UI input while a CUO surface that owns the screen is open: the standalone
/// command console overlay, or the modal Online UI window. The console is IMGUI, so Unity's UGUI
/// EventSystem does not know it is covering the screen; without this guard, clicks on its blank areas fall
/// through to the game menu/world behind it. This guard disables the game's custom
/// <see cref="AdaptiveButton"/> input and adds transparent UGUI raycast blockers on the game's active
/// screen-space canvases, then restores the original state when the modal closes.
///
/// <para>
/// Everything here is aimed at the GAME's UI, never at CUO's own (ticket
/// online-ui-art-and-controls-overhaul, S4): every CUO surface is a uGUI control on the game's canvas now
/// and blocks its own pixels, so a blocker over CUO's own canvas would cover the launcher, the window and
/// both panels and swallow every click the player makes on them. Every sweep therefore asks
/// <see cref="OnlineUiSurfaceMarker"/>, and the surface marks its root.
/// </para>
///
/// <para>
/// The rectangle-list member the IMGUI era needed for the non-modal panels is gone (S5): the quick panel
/// and the in-world player context menu are controls of that surface now, so they need no blocker of
/// their own — the game's own raycasts stop at them, and the pointer census keeps the two world input
/// paths out of them.
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
	/// launcher, the window and the panels and take every click meant for them.
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
}
