using CasualtiesUnknownOnline.GameAdapter.Character;
using System.Collections.Generic;

using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using Microsoft.Extensions.Logging;
using MapsterMapper;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Online-UI bridge to the game's native WoundView medical panel. It resolves
/// the remote player's latest 1 Hz character snapshot, creates a display-only
/// body copy from the "Experiment" template and opens the native medical UI
/// focused on that copy. The live remote render clone is never used/mutated by
/// this path; the display copy is presentation-only and destroyed on close.
/// </summary>
internal sealed class RemoteMedicalCoordinator(
	ISessionControl session,
	CharacterDataSync characterData,
	IMapper mapper,
	ILogger<RemoteMedicalCoordinator> log)
{
	private readonly ISessionControl _session = session;
	private readonly CharacterDataSync _characterData = characterData;
	private readonly IMapper _mapper = mapper;
	private readonly ILogger<RemoteMedicalCoordinator> _log = log;

	internal bool Open(ulong steamId, string displayName)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return false;
		}

		if (!_session.IsRemoteInWorld(steamId))
		{
			_log.LogWarning("[MedicalView] refused open for {SteamId}: not in-world.", steamId);
			return false;
		}

		if (!_characterData.CloneData.TryGetValue(steamId, out var data) || data.Health is null)
		{
			_log.LogWarning("[MedicalView] refused open for {SteamId}: no character snapshot with health yet.", steamId);
			return false;
		}

		// The remote backpack and remote medical views are distinct native
		// surfaces; only one can own the interaction focus at a time. If the
		// remote backpack is currently open, close it (including the native
		// radial-open state) before opening the WoundView.
		if (RemoteBackpackView.IsOpen)
		{
			_log.LogInformation("[MedicalView] closing remote backpack view before opening medical for {SteamId}.", steamId);
			RemoteBackpackView.Close();
		}

		if (!TryCreateDisplayBody(steamId, data, out var displayBody))
		{
			return false;
		}

		RemoteMedicalView.Open(displayBody, steamId, displayName);

		if (RemoteMedicalView.IsNativeWoundViewOpen())
		{
			// Already open: keep it open and just re-point the body; do not
			// toggle it twice.
			if (WoundView.view != null) // Unity object — ==
			{
				WoundView.view.body = displayBody;
			}

			_log.LogInformation("[MedicalView] opened native medical view for {SteamId} ({Name}) on an already-open panel.", steamId, displayName);
		}
		else if (PlayerCamera.main != null) // Unity object — ==
		{
			PlayerCamera.main.WoundViewButton();
			if (!RemoteMedicalView.IsNativeWoundViewOpen())
			{
				_log.LogWarning("[MedicalView] native WoundView did not open for {SteamId} — local body is not alive/available.", steamId);
				RemoteMedicalView.Close();
				return false;
			}

			if (WoundView.view != null) // Unity object — ==
			{
				WoundView.view.body = displayBody;
			}

			_log.LogInformation("[MedicalView] opened native medical view for {SteamId} ({Name}).", steamId, displayName);
		}
		else
		{
			_log.LogWarning("[MedicalView] cannot open native medical view for {SteamId}: no PlayerCamera.", steamId);
			RemoteMedicalView.Close();
			return false;
		}

		return true;
	}

	internal void Close()
	{
		if (!RemoteMedicalView.IsOpen)
		{
			return;
		}

		_log.LogInformation("[MedicalView] closed native medical view for {SteamId} ({Name}).",
			RemoteMedicalView.TargetSteamId, RemoteMedicalView.DisplayName);
		RemoteMedicalView.Close();
	}

	/// <summary>
	/// Pump: if the user closed the native WoundView by its own key/button, tear
	/// down the remote focus immediately so no stale display copy leaks.
	/// Also refreshes the display body when a newer snapshot arrives.
	/// </summary>
	internal void Update()
	{
		if (!RemoteMedicalView.IsOpen)
		{
			return;
		}

		if (!_session.SessionActive
			|| !_session.LocalInWorld
			|| !_session.IsRemoteInWorld(RemoteMedicalView.TargetSteamId))
		{
			Close();
			return;
		}

		if (!RemoteMedicalView.IsNativeWoundViewOpen())
		{
			Close();
			return;
		}

		if (_characterData.CloneData.TryGetValue(RemoteMedicalView.TargetSteamId, out var data)
			&& data.Health is not null
			&& RemoteMedicalView.DisplayBody is { } display) // Unity object — ==
		{
			ApplySnapshot(display, data);
		}
	}
	/// <summary>
	/// Apply an authoritative medical-operation progress/terminal snapshot to
	/// the active remote WoundView display (if it is focused on the same target).
	/// The display copy remains read-only; this only refreshes its presentation.
	/// </summary>
	internal void ApplyMedicalState(ulong targetSteamId, CharacterHealthMsg? health, IReadOnlyList<CharacterLimbMsg>? limbs)
	{
		if (!RemoteMedicalView.IsOpen
			|| RemoteMedicalView.TargetSteamId != targetSteamId
			|| RemoteMedicalView.DisplayBody is not { } display)
		{
			return;
		}

		if (health is not null)
		{
			_mapper.Map(health, display);
			CharacterComponentSync.Apply(display, health);
			ApplyDisplayDerived(display, health);
		}

		if (limbs is null)
		{
			return;
		}

		foreach (var limbData in limbs)
		{
			if (limbData.Index < 0 || limbData.Index >= display.limbs.Length)
			{
				continue;
			}

			var limb = display.limbs[limbData.Index]; // Unity object — ==
			if (limb == null)
			{
				continue;
			}

			_mapper.Map(limbData, limb);
			LimbComponentStateCodec.Apply(limb, limbData.Components);
		}

		display.averagePain = ComputeAveragePain(display);
		display.totalBleedSpeed = ComputeTotalBleedSpeed(display);
	}



	private bool TryCreateDisplayBody(ulong steamId, CharacterDataMsg data, out Body displayBody)
	{
		var template = GameObject.Find("Experiment");
		if (template == null) // Unity object — ==
		{
			displayBody = null!;
			_log.LogWarning("[MedicalView] cannot create display body for {SteamId}: no \"Experiment\" template.", steamId);
			return false;
		}

		var go = Object.Instantiate(template);
		go.name = $"MedicalDisplay_{steamId:X}";
		go.SetActive(false);

		var body = go.GetComponentInChildren<Body>();
		if (body == null) // Unity object — ==
		{
			Object.Destroy(go);
			displayBody = null!;
			_log.LogWarning("[MedicalView] cannot create display body for {SteamId}: no Body in template clone.", steamId);
			return false;
		}

		// The display copy is read by WoundView only; keep it out of the world
		// and out of all simulation/collision paths. Inactive hierarchy means
		// Unity never runs its Update/Physics while it is being inspected.
		body.transform.position = new Vector3(0f, -10000f, 0f);
		foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>())
		{
			rb.simulated = false;
		}

		foreach (var col in go.GetComponentsInChildren<Collider2D>())
		{
			col.enabled = false;
		}

		ApplySnapshot(body, data);
		displayBody = body;
		return true;
	}

	private void ApplySnapshot(Body body, CharacterDataMsg data)
	{
		if (data.Health is { } health)
		{
			_mapper.Map(health, body);
			CharacterComponentSync.Apply(body, health);
			ApplyDisplayDerived(body, health);
		}

		if (data.Skills is { } skills)
		{
			_mapper.Map(skills, body.skills);
			body.skills.UpdateExpBoundaries();
		}

		for (var i = 0; i < data.Limbs.Count; i++)
		{
			var limbData = data.Limbs[i];
			if (limbData.Index < 0 || limbData.Index >= body.limbs.Length)
			{
				continue;
			}

			var limb = body.limbs[limbData.Index]; // Unity object — ==
			if (limb == null)
			{
				continue;
			}

			_mapper.Map(limbData, limb);
			LimbComponentStateCodec.Apply(limb, limbData.Components);
		}

		// WoundView reads these two fields directly; they are normally produced
		// by the local Body.Update simulation. Rebuild them from the synced limb
		// facts so the read-only display matches the owner's own panel.
		body.averagePain = ComputeAveragePain(body);
		body.totalBleedSpeed = ComputeTotalBleedSpeed(body);
	}

	/// <summary>
	/// Fill the display-only body fields that are normally produced by the
	/// owner's live <c>Body.Update</c> / component updates. The display clone is
	/// deliberately inactive, so those Update methods never run; without this
	/// projection the remote WoundView would show a stale/default heart readout,
	/// miss opiate/antidepressant happiness, and leave the native mood/medical
	/// cross-checks inconsistent.
	/// </summary>
	private static void ApplyDisplayDerived(Body body, CharacterHealthMsg health)
	{
		// Explicitly carry the critical readouts too: Mapster covers matching
		// fields, but the inactive clone must never fall back to template
		// defaults for the values the native panel displays directly.
		body.heartRate = health.HeartRate;
		body.bloodPressure = health.BloodPressure;
		body.bloodPressureReadout = $"{Mathf.RoundToInt(health.BloodPressure)}/{Mathf.RoundToInt(health.BloodPressure * 0.66f)}";

		// Opiate happiness is produced by Painkillers.Update on a live body.
		var opiateReception = health.ActualOpiateReception;
		body.opiateHappiness = opiateReception > 0f
			? opiateReception
			: Mathf.Max(-80f, opiateReception * 1.66f);

		// Antidepressant happiness is produced by Antidepressants.Update; the
		// component only contributes while its amount is non-zero, and the
		// currentAmount determines the live ramp.
		body.antidepressantHappiness = health.AntidepressantsAmount > 0f
			? -body.happiness * 0.6f * Mathf.Clamp01(health.AntidepressantsCurrentAmount * 0.0166f)
			: 0f;

		// Mindwipe is assigned by Body.Update's half-second pass (Body.cs:3569).
		// The display clone may still carry a component from an earlier snapshot;
		// remove it when the authoritative snapshot says the state is gone.
		if (health.MindwipeScriptPresent)
		{
			body.mindWipe = body.GetComponent<MindwipeScript>();
		}
		else
		{
			body.mindWipe = null;
			var staleMindwipe = body.GetComponent<MindwipeScript>();
			if (staleMindwipe != null) // Unity object — ==
			{
				Object.Destroy(staleMindwipe);
			}
		}
	}

	private static float ComputeAveragePain(Body body)
	{
		var pain = 0f;
		foreach (var limb in body.limbs)
		{
			if (limb == null || limb.dismembered) // Unity object — ==
			{
				continue;
			}

			if (limb.pain > pain)
			{
				pain = limb.pain;
			}
		}

		return pain;
	}

	private static float ComputeTotalBleedSpeed(Body body)
	{
		var total = 0f;
		foreach (var limb in body.limbs)
		{
			if (limb == null || limb.dismembered) // Unity object — ==
			{
				continue;
			}

			total += limb.bleedAmount * (limb.blockedBleeding ? 0f : 1f);
		}

		return total;
	}
}
