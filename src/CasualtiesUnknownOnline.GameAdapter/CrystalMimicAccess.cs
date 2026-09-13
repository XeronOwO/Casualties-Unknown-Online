using System.Collections;
using CasualtiesUnknownOnline.Runtime.Session.World;
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

	/// <summary>Set the latch exactly once, reporting WHICH way it did not apply:
	/// <see cref="TrapActionOutcome.Applied"/> when the latch was written,
	/// <see cref="TrapActionOutcome.AlreadyInState"/> when it was already consumed
	/// (the duplicate case — the state the row names IS in the world), and
	/// <see cref="TrapActionOutcome.NotApplicable"/> when this crystal carries no
	/// mimic effect at all or the latch member is missing — the two reasons a bool
	/// used to conflate, and the divergence a restore must count as a refused row
	/// (CrystalBehaviour.SetUpEffects rolls the list per crystal — the mimic is one
	/// of seventeen weighted effects, and about seven crystals in ten are destroyed
	/// before any effect is set, CrystalBehaviour.cs:83-102).</summary>
	internal static TrapActionOutcome TryActivate(CrystalBehaviour crystal)
	{
		var mimic = Find(crystal);
		if (mimic is null)
		{
			return TrapActionOutcome.NotApplicable; // no mimic effect on this crystal
		}

		var activated = Traverse.Create(mimic).Field(ActivatedFieldName);
		if (!activated.FieldExists())
		{
			return TrapActionOutcome.NotApplicable; // the latch member the contract expects is gone
		}

		if (activated.GetValue<bool>())
		{
			return TrapActionOutcome.AlreadyInState; // already consumed — a duplicate event
		}

		activated.SetValue(true);
		return TrapActionOutcome.Applied;
	}
}
