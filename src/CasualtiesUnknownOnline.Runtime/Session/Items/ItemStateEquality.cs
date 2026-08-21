using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Pure item-state comparison shared by the evidence check
/// (<see cref="ItemArbitration"/>) and the periodic keyframe
/// reconcile (<see cref="ItemReconcile"/>, GameAdapter). Extracted so the
/// same tolerance rules (condition float, liquid amounts, component fields)
/// are one definition instead of two copies; the adapter only needs a
/// "does this world item's top-level state differ from the snapshot?" verdict
/// before applying the restore.
/// </summary>
internal static class ItemStateEquality
{
	/// <summary>Top-level state: condition (tolerance), favourited, liquid
	/// stacks and [Saveable] component states. Contents are compared
	/// separately by the evidence path (id sets) and are not part of this
	/// verdict.</summary>
	internal static bool TopLevelMatches(CharacterItemMsg a, CharacterItemMsg b, float conditionTolerance = 0.01f)
		=> Math.Abs(a.Condition - b.Condition) < conditionTolerance
			&& a.Favourited == b.Favourited
			&& LiquidsMatch(a.Liquids, b.Liquids)
			&& ComponentsMatch(a.Components, b.Components);

	internal static bool LiquidsMatch(List<LiquidStackMsg> a, List<LiquidStackMsg> b)
	{
		if (a.Count != b.Count)
		{
			return false;
		}

		foreach (var left in a)
		{
			var right = b.FirstOrDefault(l => l.LiquidId == left.LiquidId);
			if (right == null || Math.Abs(right.Amount - left.Amount) >= 0.01f)
			{
				return false;
			}
		}

		return true;
	}

	internal static bool ComponentsMatch(List<ComponentStateMsg> a, List<ComponentStateMsg> b)
	{
		if (a.Count != b.Count)
		{
			return false;
		}

		foreach (var left in a)
		{
			var right = b.FirstOrDefault(c => c.TypeName == left.TypeName);
			if (right == null || left.Fields.Count != right.Fields.Count)
			{
				return false;
			}

			foreach (var leftField in left.Fields)
			{
				var rightField = right.Fields.FirstOrDefault(f => f.Name == leftField.Name);
				if (rightField == null || !FieldEquals(leftField, rightField))
				{
					return false;
				}
			}
		}

		return true;
	}

	internal static bool FieldEquals(ComponentFieldMsg a, ComponentFieldMsg b) => a.Kind switch
	{
		1 => Math.Abs(a.FloatValue - b.FloatValue) < 0.01f,
		2 => a.IntValue == b.IntValue,
		3 => a.BoolValue == b.BoolValue,
		4 => a.StringValue == b.StringValue,
		5 => a.StringList.SequenceEqual(b.StringList),
		6 => a.IntValue == b.IntValue, // enum — stored as its underlying int (ItemStateCodec kind 6)
		_ => false,
	};
}
