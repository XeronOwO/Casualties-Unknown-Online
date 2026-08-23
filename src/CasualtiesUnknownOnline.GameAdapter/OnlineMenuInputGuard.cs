using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Suppresses background game UI input while the CUO Online UI modal window is
/// open. The Online UI is IMGUI, so Unity's UGUI EventSystem does not know it
/// is covering the screen; without this guard, clicks on the window's blank
/// areas fall through to the game menu/world behind it. This guard disables
/// the game's custom <see cref="AdaptiveButton"/> input and adds transparent
/// UGUI raycast blockers on active screen-space canvases, then restores the
/// original state when the modal closes.
/// </summary>
internal sealed class OnlineMenuInputGuard(
	ISessionControl session,
	ILogger<OnlineMenuInputGuard> log)
{
	private readonly ISessionControl _session = session;
	private readonly ILogger<OnlineMenuInputGuard> _log = log;
	private readonly List<AdaptiveButton> _buttons = [];
	private readonly List<GameObject> _blockers = [];

	private bool _modal;

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
		CaptureAdaptiveButtons();
		CreateRaycastBlockers();
		_log.LogInformation("Online UI modal open — background UI input blocked.");
	}

	private void EndModal()
	{
		RestoreAdaptiveButtons();
		DestroyRaycastBlockers();
		_log.LogInformation("Online UI modal closed — background UI input restored.");
	}

	private void CaptureAdaptiveButtons()
	{
		foreach (var button in UnityEngine.Object.FindObjectsOfType<AdaptiveButton>())
		{
			if (button == null || !button.enabled) // Unity object — ==
			{
				continue;
			}

			_buttons.Add(button);
			button.enabled = false;
		}
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

	private void CreateRaycastBlockers()
	{
		foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
		{
			if (canvas == null || !canvas.gameObject.activeInHierarchy) // Unity object — ==
			{
				continue;
			}

			if (canvas.renderMode == RenderMode.WorldSpace)
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
		}
	}

	private void DestroyRaycastBlockers()
	{
		foreach (var blocker in _blockers)
		{
			if (blocker != null) // Unity object — ==
			{
				UnityEngine.Object.Destroy(blocker);
			}
		}

		_blockers.Clear();
	}
}
