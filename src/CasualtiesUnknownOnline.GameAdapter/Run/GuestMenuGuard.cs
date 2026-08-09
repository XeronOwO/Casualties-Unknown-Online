using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// Guest menu lock: while a guest is bound to a lobby the start screen is
/// host-only — the run-start entries (StartRun/LoadRun/StartTutorial) refuse
/// unauthorised calls and the menu's entry buttons are disabled, so a guest
/// can only enter a world on the host's WorldJoin instruction.
/// </summary>
internal sealed class GuestMenuGuard(
	SessionService session,
	ILogger<GuestMenuGuard> log)
{
	private readonly SessionService _session = session;
	private readonly ILogger<GuestMenuGuard> _log = log;

	/// <summary>Set right before the WorldJoin-triggered StartRun, consumed by the run-start gate.</summary>
	private bool _startRunAuthorized;

	/// <summary>Guest-in-session: menu buttons that open the start screen / enter a world.</summary>
	private readonly List<Button> _blockedButtons = [];

	/// <summary>Gate for every run-start entry (StartRun/LoadRun/StartTutorial) — returns false to block. A guest may only enter the world on the host's instruction (WorldJoin): starting on its own would create a world the host does not know.</summary>
	internal bool OnGuestStartAttempt()
	{
		if (_startRunAuthorized)
		{
			_startRunAuthorized = false;
			return true;
		}

		if (_session.Role == SessionRole.Guest && _session.HostSteamId != 0)
		{
			_log.LogWarning("A guest cannot start a run on its own — wait for the host to enter the world.");
			return false;
		}

		return true;
	}

	/// <summary>Authorise the next run-start entry (the WorldJoin path calls this right before its own StartRun).</summary>
	internal void AuthorizeNextStart() => _startRunAuthorized = true;

	/// <summary>Pump: disable the menu's start-screen/entry buttons for a lobby-bound guest, restore them otherwise.</summary>
	internal void Update()
	{
		var blocking = _session.Role == SessionRole.Guest && _session.HostSteamId != 0;

		foreach (var ab in UnityEngine.Object.FindObjectsOfType<AdaptiveButton>())
		{
			if (ab == null) // Unity object — ==
			{
				continue;
			}

			if (ab.action is AdaptiveButton.MenuAction.Play or AdaptiveButton.MenuAction.Tutorial)
			{
				if (ab.enabled == blocking)
				{
					ab.enabled = !blocking;
				}
			}
		}

		if (!blocking)
		{
			// Lobby binding gone — restore anything we disabled (the menu may
			// be reused for solo play) and drop the scan cache.
			foreach (var btn in _blockedButtons)
			{
				if (btn != null && !btn.interactable) // Unity object — ==
				{
					btn.interactable = true;
				}
			}

			_blockedButtons.Clear();
			return;
		}

		var pre = PreRunScript.instance;
		if (pre == null) // Unity object — == (menu not loaded)
		{
			return;
		}

		EnsureBlockedButtons(pre);
		foreach (var btn in _blockedButtons)
		{
			if (btn != null && btn.interactable) // Unity object — ==
			{
				btn.interactable = false;
			}
		}

		if (pre.runSettingsScreen != null && pre.runSettingsScreen.activeSelf) // Unity object — ==
		{
			pre.runSettingsScreen.SetActive(false); // backstop: any non-button open path
		}
	}

	private void EnsureBlockedButtons(PreRunScript pre)
	{
		// Cache validity: the menu scene rebuilds the buttons on reload — the
		// cached list is dead once every entry is a destroyed object.
		if (_blockedButtons.Count > 0 && _blockedButtons.Any(b => b != null))
		{
			return;
		}

		_blockedButtons.Clear();
		foreach (var btn in UnityEngine.Object.FindObjectsOfType<Button>())
		{
			if (btn == null) // Unity object — ==
			{
				continue;
			}

			for (var i = 0; i < btn.onClick.GetPersistentEventCount(); i++)
			{
				var target = btn.onClick.GetPersistentTarget(i);
				var method = btn.onClick.GetPersistentMethodName(i);
				// The button that opens the start screen (scene-wired
				// SetActive on runSettingsScreen) and the world entries.
				if ((target is GameObject go && go == pre.runSettingsScreen)
					|| (target is PreRunScript && method is "StartRun" or "LoadRun" or "StartTutorial"))
				{
					_blockedButtons.Add(btn);
					break;
				}
			}
		}
	}
}
