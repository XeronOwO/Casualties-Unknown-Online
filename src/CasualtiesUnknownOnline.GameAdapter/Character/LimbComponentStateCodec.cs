using System;
using System.Collections.Generic;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The owner-side capture/apply of the dynamic <c>[Saveable]</c> limb
/// components that Mapster cannot see (SplintLimb, TourniquetScript,
/// ChilledLimb). The wire form reuses <see cref="ComponentStateMsg"/> so the
/// 1 Hz character snapshot, cross-player item-use results and reconnect
/// restores all carry the same component state. It owns no game state — the
/// body/limb remain the owner.
/// </summary>
internal static class LimbComponentStateCodec
{
	internal static List<ComponentStateMsg> Capture(Limb limb)
	{
		var states = new List<ComponentStateMsg>();

		var splint = limb.GetComponent<SplintLimb>();
		if (splint != null) // Unity object — ==
		{
			states.Add(new ComponentStateMsg
			{
				TypeName = nameof(SplintLimb),
				Fields =
				[
					FloatField("condition", splint.condition),
					FloatField("conditionLossMinute", splint.conditionLossMinute),
					StringField("item", splint.item),
				],
			});
		}

		var tourniquet = limb.GetComponent<TourniquetScript>();
		if (tourniquet != null) // Unity object — ==
		{
			states.Add(new ComponentStateMsg
			{
				TypeName = nameof(TourniquetScript),
				Fields =
				[
					FloatField("condition", tourniquet.condition),
					FloatField("timeApplied", tourniquet.timeApplied),
				],
			});
		}

		var chilled = limb.GetComponent<ChilledLimb>();
		if (chilled != null) // Unity object — ==
		{
			states.Add(new ComponentStateMsg
			{
				TypeName = nameof(ChilledLimb),
				Fields =
				[
					FloatField("timeLeft", chilled.timeLeft),
					FloatField("maxTime", chilled.maxTime),
				],
			});
		}

		return states;
	}

	internal static void Apply(Limb limb, List<ComponentStateMsg>? states)
	{
		if (states is null)
		{
			return;
		}

		foreach (var state in states)
		{
			switch (state.TypeName)
			{
				case nameof(SplintLimb):
					var splint = limb.GetComponent<SplintLimb>();
					if (splint == null) // Unity object — ==
					{
						splint = limb.gameObject.AddComponent<SplintLimb>();
					}

					RestoreFields(splint, state.Fields);
					break;
				case nameof(TourniquetScript):
					var tourniquet = limb.GetComponent<TourniquetScript>();
					if (tourniquet == null) // Unity object — ==
					{
						tourniquet = limb.gameObject.AddComponent<TourniquetScript>();
					}

					RestoreFields(tourniquet, state.Fields);
					break;
				case nameof(ChilledLimb):
					var chilled = limb.GetComponent<ChilledLimb>();
					if (chilled == null) // Unity object — ==
					{
						chilled = limb.gameObject.AddComponent<ChilledLimb>();
					}

					RestoreFields(chilled, state.Fields);
					break;
			}
		}
	}

	private static void RestoreFields(Component component, List<ComponentFieldMsg> fields)
	{
		foreach (var field in fields)
		{
			var target = component.GetType().GetField(
				field.Name,
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (target is null || target.IsStatic || target.IsInitOnly)
			{
				continue;
			}

			switch (field.Kind)
			{
				case SaveableFieldKind.Float:
					target.SetValue(component, field.FloatValue);
					break;
				case SaveableFieldKind.Int:
					target.SetValue(component, field.IntValue);
					break;
				case SaveableFieldKind.Bool:
					target.SetValue(component, field.BoolValue);
					break;
				case SaveableFieldKind.String:
					target.SetValue(component, field.StringValue);
					break;
				case SaveableFieldKind.StringList:
					target.SetValue(component, field.StringList);
					break;
				case SaveableFieldKind.Enum:
					target.SetValue(component, Enum.ToObject(target.FieldType, field.IntValue));
					break;
			}
		}
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
}
