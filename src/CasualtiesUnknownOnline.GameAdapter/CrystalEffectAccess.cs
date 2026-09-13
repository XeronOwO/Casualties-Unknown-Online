using System.Collections;
using CasualtiesUnknownOnline.Runtime.Session.World;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Reflection access to the crystal EFFECT list, shared by every per-effect accessor:
/// the effects live in the private <c>CrystalBehaviour.effects</c> list
/// (CrystalBehaviour.cs:83-107) and each one-shot effect carries its own private
/// <c>bool activated</c> latch. The list is read UNTYPED (its element type is a game
/// type the adapter deliberately does not bind to) and an effect is found by its
/// RUNTIME TYPE NAME; the latch is read and written with its exact bool type.
///
/// This is the ONE lookup and the ONE latch RULE: every per-effect accessor and every
/// crystal state action reaches the list and the latch through here, so a game update that
/// moves the list fails in one place instead of in every copy. (The trigger patches read
/// the same `activated` member through their own Traverse calls — they observe its rise on
/// effects they already hold — and the same <c>GameFieldContractTests</c> rows lock the
/// member for both.)
/// </summary>
internal static class CrystalEffectAccess
{
	/// <summary>The latch member every one-shot crystal effect carries.</summary>
	internal const string ActivatedFieldName = "activated";

	/// <summary>The named effect on this crystal, or null when the crystal carries none
	/// of that kind (the effect list is rolled per crystal, so a crystal of the expected
	/// kind at the expected position can legitimately carry no such effect — the
	/// position-keyed replay then reports the mismatch instead of inventing the fact).</summary>
	internal static object? Find(CrystalBehaviour crystal, string effectTypeName)
	{
		var effectsField = Traverse.Create(crystal).Field("effects");
		if (!effectsField.FieldExists() || effectsField.GetValue() is not IEnumerable effects)
		{
			return null;
		}

		foreach (var effect in effects)
		{
			if (effect != null && effect.GetType().Name == effectTypeName)
			{
				return effect;
			}
		}

		return null;
	}

	/// <summary>The latch as this copy holds it (false when the crystal carries no such
	/// effect or the member is gone — the caller decides what that means).</summary>
	internal static bool IsActivated(CrystalBehaviour crystal, string effectTypeName)
	{
		var effect = Find(crystal, effectTypeName);
		return effect is not null && TryReadBool(effect, ActivatedFieldName, out var activated) && activated;
	}

	/// <summary>Set the named effect's <c>activated</c> latch exactly once, answering
	/// WHICH way it did not apply: <see cref="TrapActionOutcome.Applied"/> when the latch
	/// was written, <see cref="TrapActionOutcome.AlreadyInState"/> when it was already
	/// consumed (the duplicate case — the state the row names IS in the world), and
	/// <see cref="TrapActionOutcome.NotApplicable"/> when this crystal carries no such
	/// effect at all or the latch member is missing — the two reasons a bool used to
	/// conflate, and the divergence a restore must count as a refused row.</summary>
	internal static TrapActionOutcome TryActivate(CrystalBehaviour crystal, string effectTypeName)
	{
		var effect = Find(crystal, effectTypeName);
		if (effect is null)
		{
			return TrapActionOutcome.NotApplicable; // no such effect on this crystal
		}

		var activated = Traverse.Create(effect).Field(ActivatedFieldName);
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

	/// <summary>A typed read of one bool member on an effect — the shape the unstable
	/// crystal's READ-ONLY <c>timerStarted</c> latch needs (it must never be written: a
	/// written latch would make the local Update count down and explode the crystal
	/// naturally, double-applying what the exploded event already replays).</summary>
	internal static bool TryReadBool(object effect, string fieldName, out bool value)
	{
		var field = Traverse.Create(effect).Field(fieldName);
		if (!field.FieldExists())
		{
			value = false;
			return false;
		}

		value = field.GetValue<bool>();
		return true;
	}
}
