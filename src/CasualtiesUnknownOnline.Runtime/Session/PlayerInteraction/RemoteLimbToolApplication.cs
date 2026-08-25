using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Pure application of a <see cref="RemoteLimbToolProfile"/> to a character
/// snapshot. It applies immediate body/limb deltas and multiplicative factors,
/// and for the component-bearing subset also writes the neutral limb component
/// state that the Game Adapter later turns into a real game component
/// (SplintLimb/TourniquetScript/ChilledLimb). A required limb (e.g.
/// <c>chestdrain</c>) or an ineligible head/vital/already-applied component
/// returns false so the host can refuse before consuming the item. No game
/// assembly, no state, no I/O.
/// </summary>
public static class RemoteLimbToolApplication
{
	/// <summary>
	/// Apply one limb tool to the selected limb (or the profile's required limb)
	/// and body. Returns false when the required limb is missing or the
	/// component-bearing tool is ineligible.
	/// </summary>
	public static bool TryApply(
		CharacterHealthMsg health,
		IReadOnlyList<CharacterLimbMsg> limbs,
		RemoteLimbToolProfile profile,
		out int limbIndex,
		float itemCondition = 1f)
	{
		limbIndex = -1;
		if (health is null || limbs.Count == 0)
		{
			return false;
		}

		if (profile.RequiresShrapnel)
		{
			limbIndex = PickMostShrapnelLimb(limbs);
			if (limbIndex < 0)
			{
				return false;
			}
		}
		else if (profile.RequiredLimbIndex >= 0)
		{
			if (profile.RequiredLimbIndex >= limbs.Count)
			{
				return false;
			}

			limbIndex = profile.RequiredLimbIndex;
		}
		else
		{
			limbIndex = RemoteHealApplication.PickMostInjuredLimb(limbs);
			if (limbIndex < 0)
			{
				return false;
			}
		}

		var limb = limbs[limbIndex];
		if (!CanApplyComponent(limb, profile))
		{
			return false;
		}

		limb.SkinHealth = Clamp100(limb.SkinHealth + profile.SkinHealth);
		limb.MuscleHealth = Clamp100(limb.MuscleHealth + profile.MuscleHealth);
		limb.Pain = Math.Max(0f, limb.Pain + profile.Pain);
		limb.BleedAmount = Math.Max(0f, limb.BleedAmount * profile.BleedAmountMultiplier + profile.BleedAmount);
		limb.BoneHealTimer = Math.Max(0f, limb.BoneHealTimer * profile.BoneHealTimerMultiplier + profile.BoneHealTimer);
		limb.DislocationTimer = Math.Max(0f, limb.DislocationTimer + profile.DislocationTimer);
		limb.SkinHealAmount = Math.Max(0f, limb.SkinHealAmount + profile.SkinHealAmount);
		limb.BandageSlowAmount = Math.Max(0f, limb.BandageSlowAmount + profile.BandageSlowAmount);
		health.BloodViscosity += profile.BloodViscosity;
		health.Hemothorax = Math.Max(0f, health.Hemothorax + profile.Hemothorax);
		health.Temperature += profile.Temperature;

		ApplyComponent(limb, profile, itemCondition);
		if (profile.RequiresShrapnel)
		{
			limb.Shrapnel = 0;
		}

		return true;
	}

	private static int PickMostShrapnelLimb(IReadOnlyList<CharacterLimbMsg> limbs)
	{
		var best = -1;
		var bestShrapnel = 0;
		for (var i = 0; i < limbs.Count; i++)
		{
			var limb = limbs[i];
			if (limb.Dismembered || limb.Shrapnel <= bestShrapnel)
			{
				continue;
			}

			best = i;
			bestShrapnel = limb.Shrapnel;
		}

		return best;
	}

	private static bool CanApplyComponent(CharacterLimbMsg limb, RemoteLimbToolProfile profile)
	{
		if (profile.ComponentKind is RemoteLimbComponentKind.Splint or RemoteLimbComponentKind.Tourniquet)
		{
			if (limb.IsHead || limb.IsVital)
			{
				return false;
			}

			// Item.cs:402 — the native tourniquet explicitly refuses the
			// body's central limb (limbs[2]) even when it is not flagged vital.
			if (profile.ComponentKind == RemoteLimbComponentKind.Tourniquet && limb.Index == 2)
			{
				return false;
			}

			if (HasComponent(limb, ComponentTypeName(profile.ComponentKind)))
			{
				return false;
			}
		}

		return true;
	}

	private static void ApplyComponent(CharacterLimbMsg limb, RemoteLimbToolProfile profile, float itemCondition)
	{
		if (profile.ComponentKind == RemoteLimbComponentKind.None)
		{
			return;
		}

		limb.Components ??= [];
		var typeName = ComponentTypeName(profile.ComponentKind);
		limb.Components.RemoveAll(c => c.TypeName == typeName);
		limb.Components.Add(CreateComponentState(profile, typeName, itemCondition));

		if (profile.ComponentKind == RemoteLimbComponentKind.Splint)
		{
			limb.Splinted = true;
		}
		else if (profile.ComponentKind == RemoteLimbComponentKind.Tourniquet)
		{
			limb.BlockedBleeding = true;
		}
	}

	private static bool HasComponent(CharacterLimbMsg limb, string typeName)
	{
		foreach (var state in limb.Components ?? [])
		{
			if (state.TypeName == typeName)
			{
				return true;
			}
		}

		return false;
	}

	private static string ComponentTypeName(RemoteLimbComponentKind kind) => kind switch
	{
		RemoteLimbComponentKind.Splint => "SplintLimb",
		RemoteLimbComponentKind.Tourniquet => "TourniquetScript",
		RemoteLimbComponentKind.Icepack => "ChilledLimb",
		_ => "",
	};

	private static ComponentStateMsg CreateComponentState(
		RemoteLimbToolProfile profile,
		string typeName,
		float itemCondition)
	{
		var fields = new List<ComponentFieldMsg>();
		switch (profile.ComponentKind)
		{
			case RemoteLimbComponentKind.Splint:
				fields.Add(FloatField("condition", itemCondition));
				fields.Add(FloatField("conditionLossMinute", profile.ComponentConditionLossMinute));
				fields.Add(StringField("item", profile.ItemId));
				break;
			case RemoteLimbComponentKind.Tourniquet:
				fields.Add(FloatField("condition", itemCondition));
				fields.Add(FloatField("timeApplied", 0f));
				break;
			case RemoteLimbComponentKind.Icepack:
				fields.Add(FloatField("timeLeft", profile.ComponentTimeLeft));
				fields.Add(FloatField("maxTime", profile.ComponentMaxTime));
				break;
		}

		return new ComponentStateMsg { TypeName = typeName, Fields = fields };
	}

	private static ComponentFieldMsg FloatField(string name, float value) => new()
	{
		Name = name,
		Kind = SaveableFieldKind.Float,
		FloatValue = value,
	};

	private static ComponentFieldMsg StringField(string name, string value) => new()
	{
		Name = name,
		Kind = SaveableFieldKind.String,
		StringValue = value,
	};

	private static float Clamp100(float value) => Math.Max(0f, Math.Min(100f, value));
}
