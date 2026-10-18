using System;
using System.Collections.Generic;
using System.Globalization;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The host's minimal reconciliation (the 2026-10-18 ruling, item 5): every 1 Hz report is compared
/// with the host model's current values BEFORE the report is filtered, and each disagreement is
/// named with player, field, model value, reported value and difference. This is the only way a
/// wrong host model becomes visible; without it a bad model is silent.
///
/// <para>
/// The model value is the stored snapshot at the moment the report arrives — the host clone writes
/// its three values into that snapshot after every fixed step, so it is the model's latest write,
/// not the report's previous copy. The tolerances absorb the sampling skew between a value captured
/// on the client (up to a second old, plus latency) and the host's current value; a disagreement
/// beyond them is a real divergence and is logged, while the host's value stands.
/// </para>
/// </summary>
internal static class VitalsReconciliation
{
	/// <summary>The blood-volume difference that counts as a divergence (the panel's own resolution is one unit).</summary>
	internal const float BloodTolerance = 0.5f;

	/// <summary>The bleeding-speed difference that counts as a divergence, for the body and per-limb values.</summary>
	internal const float BleedTolerance = 0.05f;

	/// <summary>
	/// The disagreements between the host's stored values and the raw report, one human-readable line
	/// each; empty when the report agrees within tolerance. A snapshot missing health or a limb the
	/// other side carries is not a value disagreement and is skipped.
	/// </summary>
	internal static List<string> Compare(CharacterDataMsg model, CharacterDataMsg reported)
	{
		var differences = new List<string>();
		if (model.Health is not { } modelHealth || reported.Health is not { } reportedHealth)
		{
			return differences;
		}

		AddIfBeyond(differences, "blood volume", modelHealth.BloodVolume, reportedHealth.BloodVolume, BloodTolerance);
		AddIfBeyond(differences, "total bleeding speed", modelHealth.TotalBleedSpeed, reportedHealth.TotalBleedSpeed, BleedTolerance);

		foreach (var reportedLimb in reported.Limbs)
		{
			foreach (var modelLimb in model.Limbs)
			{
				if (modelLimb.Index == reportedLimb.Index)
				{
					AddIfBeyond(differences, $"limb {reportedLimb.Index} bleed", modelLimb.BleedAmount, reportedLimb.BleedAmount, BleedTolerance);
					break;
				}
			}
		}

		return differences;
	}

	private static void AddIfBeyond(List<string> differences, string field, float modelValue, float reportedValue, float tolerance)
	{
		var difference = reportedValue - modelValue;
		if (Math.Abs(difference) <= tolerance)
		{
			return;
		}

		differences.Add(string.Format(CultureInfo.InvariantCulture,
			"{0} model {1:F2} reported {2:F2} ({3:+0.##;-0.##;0})", field, modelValue, reportedValue, difference));
	}
}
