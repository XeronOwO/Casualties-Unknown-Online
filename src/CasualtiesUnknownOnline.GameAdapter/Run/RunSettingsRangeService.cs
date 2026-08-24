using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// Host-side custom run-settings range broadener. The game's sliders are tuned
/// for a single player; when the host is running a co-op lobby and the
/// host-rule flag is enabled, this service widens the upper bound of the
/// scalable tuning sliders proportionally to the total player count (host +
/// guests). It owns the original native ranges, restores them when the session
/// ends or the flag is turned off, and refreshes the already-created menu
/// sliders on every transition. The chosen values still ride the existing
/// <c>WorldStartParams</c> path untouched — no protocol change.
/// </summary>
internal sealed class RunSettingsRangeService(
	ISessionControl session,
	IHostRules hostRules,
	ILogger<RunSettingsRangeService> log)
{
	private readonly ISessionControl _session = session;
	private readonly IHostRules _hostRules = hostRules;
	private readonly ILogger<RunSettingsRangeService> _log = log;

	/// <summary>Native slider limits captured before the first widening (restore target).</summary>
	private readonly Dictionary<string, (float Min, float Max)> _original = [];

	private bool _applied;
	private int _lastMemberCount;

	/// <summary>Pump: apply/refresh/restore the range policy as session/host-rule state changes.</summary>
	internal void Update()
	{
		var shouldApply = _hostRules.WidenRunSettings
			&& _session.Role == SessionRole.Host
			&& _session.SessionActive;

		if (!shouldApply)
		{
			Restore();
			return;
		}

		var memberCount = 1 + _session.Members.Count();
		if (_applied && _lastMemberCount == memberCount)
		{
			return;
		}

		Apply(memberCount);
	}

	private void Apply(int memberCount)
	{
		CaptureOriginals();
		RestoreToOriginals(); // always recompute from the native baseline, not from the current (possibly already widened) limits

		var changed = new List<string>();
		foreach (var setting in RunSettings.settingTypes)
		{
			if (setting is not RunSettingFloat runSettingFloat)
			{
				continue;
			}

			var original = _original[runSettingFloat.name];
			var limits = RunSettingsRange.ForCoOp(runSettingFloat.name, original.Min, original.Max, memberCount);
			if (runSettingFloat.limits.min != limits.Min || runSettingFloat.limits.max != limits.Max)
			{
				runSettingFloat.limits = new RangeF(limits.Min, limits.Max);
				changed.Add(runSettingFloat.name);
			}
		}

		_applied = true;
		_lastMemberCount = memberCount;
		RefreshExistingDisplays();
		_log.LogInformation("Widened custom run-settings ranges for {MemberCount} players ({ChangedCount} sliders: {Changed}).",
			memberCount, changed.Count, string.Join(", ", changed));
	}

	private void Restore()
	{
		if (!_applied)
		{
			return;
		}

		RestoreToOriginals();
		_applied = false;
		_lastMemberCount = 0;
		RefreshExistingDisplays();
		_log.LogInformation("Restored native custom run-settings slider ranges.");
	}

	private void CaptureOriginals()
	{
		foreach (var setting in RunSettings.settingTypes)
		{
			if (setting is RunSettingFloat runSettingFloat
				&& !_original.ContainsKey(runSettingFloat.name))
			{
				_original[runSettingFloat.name] = (runSettingFloat.limits.min, runSettingFloat.limits.max);
			}
		}
	}

	private void RestoreToOriginals()
	{
		foreach (var setting in RunSettings.settingTypes)
		{
			if (setting is not RunSettingFloat runSettingFloat
				|| !_original.TryGetValue(runSettingFloat.name, out var limits))
			{
				continue;
			}

			runSettingFloat.limits = new RangeF(limits.Min, limits.Max);
		}
	}

	/// <summary>
	/// The menu may already be built when the host creates/toggles the lobby, so
	/// the existing RunSettingDisplay sliders must be refreshed directly (the
	/// game only reads the limits once, inside its first-time display init).
	/// </summary>
	private void RefreshExistingDisplays()
	{
		var pre = PreRunScript.instance;
		if (pre == null || pre.runSettingObjects == null) // Unity object — ==
		{
			return;
		}

		foreach (var display in pre.runSettingObjects)
		{
			if (display == null || display.associated is not RunSettingFloat runSettingFloat) // Unity object — ==
			{
				continue;
			}

			var slider = display.transform.GetChild(1).GetComponent<Slider>();
			if (slider == null) // Unity object — ==
			{
				continue;
			}

			slider.minValue = runSettingFloat.limits.min;
			slider.maxValue = runSettingFloat.limits.max;
			var value = Mathf.Clamp(slider.value, runSettingFloat.limits.min, runSettingFloat.limits.max);
			if (Mathf.Abs(slider.value - value) > 0.0001f)
			{
				slider.SetValueWithoutNotify(value);
				if (pre.runSettings != null) // keep the menu dictionary on the same clamped value the slider now shows
				{
					pre.runSettings[runSettingFloat.name] = value;
				}
			}
		}
	}
}
