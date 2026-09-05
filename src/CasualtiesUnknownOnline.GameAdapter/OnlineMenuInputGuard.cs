using System.Collections.Generic;
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
/// </summary>
internal sealed class OnlineMenuInputGuard(
	ISessionControl session,
	ILogger<OnlineMenuInputGuard> log)
{
	private readonly ISessionControl _session = session;
	private readonly ILogger<OnlineMenuInputGuard> _log = log;
	private readonly List<AdaptiveButton> _buttons = [];
	private readonly List<GameObject> _blockers = [];
	private readonly List<GameObject> _scopedBlockers = [];
	private IReadOnlyList<OnlineUiBlockRect> _scopedBlocks = [];

	private bool _modal;
	private bool _nonModalEscapeSurfaceOpen;

	internal bool IsModal => _modal;

	internal bool IsNonModalEscapeSurfaceOpen => _nonModalEscapeSurfaceOpen;

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
			CreateScopedBlockers();
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
		foreach (var button in Object.FindObjectsOfType<AdaptiveButton>())
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
		foreach (var canvas in Object.FindObjectsOfType<Canvas>())
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
				Object.Destroy(blocker);
			}
		}

		_blockers.Clear();
	}

	private void CreateScopedBlockers()
	{
		foreach (var canvas in Object.FindObjectsOfType<Canvas>())
		{
			if (canvas == null || !canvas.gameObject.activeInHierarchy) // Unity object — ==
			{
				continue;
			}

			if (canvas.renderMode == RenderMode.WorldSpace)
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
		}
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
