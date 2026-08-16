using System.Collections;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Reflection access to the internal CrystalMimic effect (CrystalMimic.cs):
/// the effect list lives in the private CrystalBehaviour.effects field
/// (CrystalBehaviour.cs:83-107) and the one-shot latch is the private bool
/// activated (CrystalMimic.cs:52). The field is read UNTYPED (the element
/// type list is a game type the adapter deliberately does not bind to); the
/// mimic itself is found by its runtime type name, and activated is read and
/// written with its exact bool type. The GameFieldContractTests rows lock
/// both members against a game update.
/// </summary>
internal static class CrystalMimicAccess
{
	private const string MimicTypeName = "CrystalMimic";

	private const string ActivatedFieldName = "activated";

	/// <summary>The CrystalMimic effect on this crystal, or null when the crystal has none (a non-mimic effect set — the position-keyed replay then reports the mismatch).</summary>
	internal static object? Find(CrystalBehaviour crystal)
	{
		var effectsField = Traverse.Create(crystal).Field("effects");
		if (!effectsField.FieldExists() || effectsField.GetValue() is not IEnumerable effects)
		{
			return null;
		}

		foreach (var effect in effects)
		{
			if (effect != null && effect.GetType().Name == MimicTypeName)
			{
				return effect;
			}
		}

		return null;
	}

	/// <summary>The mimic's activated latch (false when the crystal carries no mimic).</summary>
	internal static bool IsActivated(CrystalBehaviour crystal)
	{
		var mimic = Find(crystal);
		if (mimic is null)
		{
			return false;
		}

		var activated = Traverse.Create(mimic).Field(ActivatedFieldName);
		return activated.FieldExists() && activated.GetValue<bool>();
	}

	/// <summary>Set the latch exactly once. False when there is no mimic or the latch was already consumed (the duplicate/divergence case).</summary>
	internal static bool TryActivate(CrystalBehaviour crystal)
	{
		var mimic = Find(crystal);
		if (mimic is null)
		{
			return false;
		}

		var activated = Traverse.Create(mimic).Field(ActivatedFieldName);
		if (!activated.FieldExists() || activated.GetValue<bool>())
		{
			return false;
		}

		activated.SetValue(true);
		return true;
	}
}
