using System;
using System.Collections.Generic;
using UnityEngine;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.ModStatus;

/// <summary>
/// The GameAdapter vanilla body/limb projection for mod statuses (mod-status
/// domain phase 3). It listens to <see cref="ModStatusStore"/> changes, decodes
/// only the well-known typed projection payloads declared by
/// <see cref="ModStatusProjectionKind"/>, and applies additive overlays to the
/// local player's vanilla <c>Body</c>/<c>Limb</c> after their native updates.
///
/// This class is the ONLY layer that may turn a runtime status value into a
/// game behavior. It never exposes a game/Unity type through Abstractions and
/// it never touches arbitrary opaque status payloads. The projection
/// covers body values that are recomputed from scratch (encumbrance,
/// immunity, jump speed, average pain), circulation offsets wrapped around
/// Body.HandleCirculation, and limb physiology values that are modified
/// additively by the native limb update.
/// </summary>
internal sealed class ModStatusVanillaProjection
{
	private enum BodyField
	{
		MaxEncumbrance,
		TotalEncumbrance,
		Immunity,
		JumpSpeed,
		AveragePain,
	}

	private enum CirculationField
	{
		HeartRate,
		RespiratoryRate,
		BloodPressure,
	}

	private enum LimbField
	{
		BleedAmount,
		SkinHealth,
		MuscleHealth,
		InfectionAmount,
	}

	private sealed record ActiveBodyProjection(string ModId, string StatusId, ModBodyFormulaProjection Projection);

	private sealed record ActiveLimbProjection(string ModId, string StatusId, int LimbSlot, ModLimbProjection Projection);

	private readonly ModStatusStore _statusStore;
	private readonly ISessionControl _session;
	private readonly ILogger _log;
	private readonly List<ActiveBodyProjection> _bodyProjections = [];
	private readonly List<ActiveLimbProjection> _limbProjections = [];
	private readonly Dictionary<BodyField, float> _appliedBody = [];
	private readonly Dictionary<(int LimbSlot, LimbField Field), float> _appliedLimb = [];
	private readonly Dictionary<CirculationField, float> _appliedCirculation = [];
	private Body? _appliedBodyOwner; // Unity object — ==
	private bool _dirty = true;

	public ModStatusVanillaProjection(ModStatusStore statusStore, ISessionControl session, ILogger<ModStatusVanillaProjection> log)
	{
		_statusStore = statusStore;
		_session = session;
		_log = log;
		_statusStore.StatusChanged += MarkDirty;
	}

	internal void ApplyBody(Body body)
	{
		if (body == null) // Unity object — ==
		{
			return;
		}

		if (_appliedBodyOwner != body) // Unity object — ==
		{
			_appliedBody.Clear();
			_appliedLimb.Clear();
			_appliedCirculation.Clear();
			_appliedBodyOwner = body;
		}

		EnsureFresh();
		if (_bodyProjections.Count == 0 && _appliedBody.Count == 0)
		{
			return;
		}

		var maxEncumbrance = 0f;
		var totalEncumbrance = 0f;
		var immunity = 0f;
		var jumpSpeed = 0f;
		var averagePain = 0f;
		foreach (var active in _bodyProjections)
		{
			var projection = active.Projection;
			maxEncumbrance += projection.MaxEncumbrance;
			totalEncumbrance += projection.TotalEncumbrance;
			immunity += projection.Immunity;
			jumpSpeed += projection.JumpSpeed;
			averagePain += projection.AveragePain;
		}

		ApplyBodyField(ref body.maxEncumberance, maxEncumbrance, BodyField.MaxEncumbrance);
		ApplyBodyField(ref body.totalEncumberance, totalEncumbrance, BodyField.TotalEncumbrance);
		ApplyBodyField(ref body.immunity, immunity, BodyField.Immunity);
		ApplyBodyField(ref body.jumpSpeed, jumpSpeed, BodyField.JumpSpeed);
		ApplyBodyField(ref body.averagePain, averagePain, BodyField.AveragePain);
	}

	internal void ApplyLimb(Body body, Limb limb)
	{
		if (body == null || limb == null) // Unity objects — ==
		{
			return;
		}

		var limbSlot = Array.IndexOf(body.limbs, limb);
		if (limbSlot < 0)
		{
			return;
		}

		if (_appliedBodyOwner != body) // Unity object — ==
		{
			_appliedBody.Clear();
			_appliedLimb.Clear();
			_appliedCirculation.Clear();
			_appliedBodyOwner = body;
		}

		EnsureFresh();
		if (_limbProjections.Count == 0 && _appliedLimb.Count == 0)
		{
			return;
		}

		var bleedAmount = 0f;
		var skinHealth = 0f;
		var muscleHealth = 0f;
		var infectionAmount = 0f;
		foreach (var active in _limbProjections)
		{
			if (active.LimbSlot != limbSlot)
			{
				continue;
			}

			var projection = active.Projection;
			bleedAmount += projection.BleedAmount ?? 0f;
			skinHealth += projection.SkinHealth ?? 0f;
			muscleHealth += projection.MuscleHealth ?? 0f;
			infectionAmount += projection.InfectionAmount ?? 0f;
		}

		ApplyLimbField(ref limb.bleedAmount, bleedAmount, limbSlot, LimbField.BleedAmount);
		ApplyLimbField(ref limb.skinHealth, skinHealth, limbSlot, LimbField.SkinHealth);
		ApplyLimbField(ref limb.muscleHealth, muscleHealth, limbSlot, LimbField.MuscleHealth);
		ApplyLimbField(ref limb.infectionAmount, infectionAmount, limbSlot, LimbField.InfectionAmount);
	}

	internal void ApplyCirculationPrefix(Body body)
	{
		if (body == null) // Unity object — ==
		{
			return;
		}

		if (_appliedBodyOwner != body) // Unity object — ==
		{
			_appliedBody.Clear();
			_appliedLimb.Clear();
			_appliedCirculation.Clear();
			_appliedBodyOwner = body;
		}

		// Remove the mod circulation overlay before the native formula runs.
		// The native method recomputes these fields from their unmodified base
		// every frame, so a post-update-only overlay would be erased or would
		// drift. Removing the previous offset here and reapplying it in the
		// postfix keeps the exposed value = native base + stable mod offset.
		body.heartRate -= GetAppliedCirculation(CirculationField.HeartRate);
		body.respiratoryRate -= GetAppliedCirculation(CirculationField.RespiratoryRate);
		body.bloodPressure -= GetAppliedCirculation(CirculationField.BloodPressure);
		_appliedCirculation.Clear();
	}

	internal void ApplyCirculationPostfix(Body body)
	{
		if (body == null) // Unity object — ==
		{
			return;
		}

		if (_appliedBodyOwner != body) // Unity object — ==
		{
			_appliedBody.Clear();
			_appliedLimb.Clear();
			_appliedCirculation.Clear();
			_appliedBodyOwner = body;
		}

		EnsureFresh();
		var heartRateOffset = 0f;
		var respiratoryRateOffset = 0f;
		var bloodPressureOffset = 0f;
		foreach (var active in _bodyProjections)
		{
			var projection = active.Projection;
			heartRateOffset += projection.HeartRateOffset;
			respiratoryRateOffset += projection.RespiratoryRateOffset;
			bloodPressureOffset += projection.BloodPressureOffset;
		}

		ApplyCirculationField(ref body.heartRate, heartRateOffset, CirculationField.HeartRate);
		ApplyCirculationField(ref body.respiratoryRate, respiratoryRateOffset, CirculationField.RespiratoryRate);
		ApplyCirculationField(ref body.bloodPressure, bloodPressureOffset, CirculationField.BloodPressure);
		RefreshCirculationReadouts(body);
	}


	private void MarkDirty() => _dirty = true;

	private void EnsureFresh()
	{
		if (_dirty)
		{
			Refresh();
		}
	}

	private void Refresh()
	{
		_bodyProjections.Clear();
		_limbProjections.Clear();
		foreach (var snapshot in _statusStore.GetProjectionSnapshots(_session.LocalSteamId))
		{
			if (snapshot.RuntimeScope == ModDataScope.HostAuthoritative && _session.Role != SessionRole.Host)
			{
				continue;
			}

			if (snapshot.ProjectionKind == ModStatusProjectionKind.BodyFormula
				&& snapshot.Scope == ModStatusScope.Body)
			{
				var projection = ModBodyFormulaProjection.FromPayload(snapshot.Value);
				if (projection is null)
				{
					_log.LogWarning("[StatusProjection] {ModId}/{StatusId} is declared BodyFormula but its payload is not a valid ModBodyFormulaProjection — skipped.",
						snapshot.ModId, snapshot.StatusId);
					continue;
				}

				_bodyProjections.Add(new ActiveBodyProjection(snapshot.ModId, snapshot.StatusId, projection));
			}
			else if (snapshot.ProjectionKind == ModStatusProjectionKind.LimbPhysiology
				&& snapshot.Scope == ModStatusScope.Limb)
			{
				var projection = ModLimbProjection.FromPayload(snapshot.Value);
				if (projection is null)
				{
					_log.LogWarning("[StatusProjection] {ModId}/{StatusId} is declared LimbPhysiology but its payload is not a valid ModLimbProjection — skipped.",
						snapshot.ModId, snapshot.StatusId);
					continue;
				}

				_limbProjections.Add(new ActiveLimbProjection(snapshot.ModId, snapshot.StatusId, snapshot.LimbSlot, projection));
			}
		}

		_dirty = false;
	}

	private void ApplyBodyField(ref float current, float next, BodyField field)
	{
		var previous = _appliedBody.TryGetValue(field, out var old) ? old : 0f;
		if (previous != next)
		{
			current += next - previous;
		}

		_appliedBody[field] = next;
	}

	private void ApplyLimbField(ref float current, float next, int limbSlot, LimbField field)
	{
		var key = (limbSlot, field);
		var previous = _appliedLimb.TryGetValue(key, out var old) ? old : 0f;
		if (previous != next)
		{
			current += next - previous;
		}

		_appliedLimb[key] = next;
	}

	private float GetAppliedCirculation(CirculationField field) =>
		_appliedCirculation.TryGetValue(field, out var value) ? value : 0f;

	private void ApplyCirculationField(ref float current, float next, CirculationField field)
	{
		current += next;
		_appliedCirculation[field] = next;
	}

	private static void RefreshCirculationReadouts(Body body)
	{
		// Mirror the native readout lines (Body.cs HandleCirculation) after the
		// overlay is reapplied so the in-game vitals text includes the mod offset.
		body.bloodPressureReadout = Mathf.RoundToInt(body.bloodPressure).ToString() + "/" + Mathf.RoundToInt(body.bloodPressure * 0.66f).ToString();
		var respiratoryReadout = Mathf.RoundToInt(body.respiratoryRate * 0.25f).ToString() + "/m";
		HarmonyLib.Traverse.Create(body).Property("respiratoryRateReadout").SetValue(respiratoryReadout);
	}

}
