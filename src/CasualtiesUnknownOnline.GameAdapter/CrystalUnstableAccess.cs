using System.Collections;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Reflection access to the internal CrystalUnstable effect (CrystalUnstable.cs):
/// the effect list lives in the private CrystalBehaviour.effects field
/// (CrystalBehaviour.cs:83-107) and the 5 s pre-explosion ticking latch is the
/// private bool timerStarted (CrystalUnstable.cs:70). The field is read UNTYPED
/// (the element type list is a game type the adapter deliberately does not bind
/// to); the unstable effect is found by its runtime type name, and timerStarted
/// is read with its exact bool type. The GameFieldContractTests rows lock the
/// member against a game update. Mirror of CrystalMimicAccess.
/// </summary>
internal static class CrystalUnstableAccess
{
	private const string UnstableTypeName = "CrystalUnstable";

	private const string TimerStartedFieldName = "timerStarted";

	/// <summary>The CrystalUnstable effect on this crystal, or null when the crystal has none (a non-unstable effect set — the position-keyed replay then reports the mismatch).</summary>
	internal static object? Find(CrystalBehaviour crystal)
	{
		var effectsField = Traverse.Create(crystal).Field("effects");
		if (!effectsField.FieldExists() || effectsField.GetValue() is not IEnumerable effects)
		{
			return null;
		}

		foreach (var effect in effects)
		{
			if (effect != null && effect.GetType().Name == UnstableTypeName)
			{
				return effect;
			}
		}

		return null;
	}

	/// <summary>The unstable crystal's timerStarted latch (false when the crystal
	/// carries no unstable effect): true means THIS side's copy already started
	/// its own 5 s natural countdown (its local player touched/hit it) — the
	/// ticking visual is already running natively, a replay must not double it.</summary>
	internal static bool IsTimerStarted(CrystalBehaviour crystal)
	{
		var unstable = Find(crystal);
		if (unstable is null)
		{
			return false;
		}

		var timerStarted = Traverse.Create(unstable).Field(TimerStartedFieldName);
		return timerStarted.FieldExists() && timerStarted.GetValue<bool>();
	}
}
