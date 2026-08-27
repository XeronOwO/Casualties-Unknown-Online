using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Shared tolerance-aware comparison for item capability payloads. Kept
/// separate so every concrete capability uses the same equivalence semantics
/// instead of re-implementing float/component matching.
/// </summary>
internal static class ItemCapabilityStateComparer
{
	internal static bool SameTopLevel(CharacterItemMsg a, CharacterItemMsg b) =>
		Math.Abs(a.Condition - b.Condition) < 0.01f
		&& a.Favourited == b.Favourited
		&& SameLiquids(a.Liquids, b.Liquids)
		&& SameComponents(a.Components, b.Components);

	internal static bool SameLiquids(IReadOnlyList<LiquidStackMsg> a, IReadOnlyList<LiquidStackMsg> b)
	{
		if (a.Count != b.Count)
		{
			return false;
		}

		foreach (var left in a)
		{
			var right = b.FirstOrDefault(l => l.LiquidId == left.LiquidId);
			if (right is null || Math.Abs(right.Amount - left.Amount) >= 0.01f)
			{
				return false;
			}
		}

		return true;
	}

	internal static bool SameComponents(IReadOnlyList<ComponentStateMsg> a, IReadOnlyList<ComponentStateMsg> b)
	{
		if (a.Count != b.Count)
		{
			return false;
		}

		foreach (var left in a)
		{
			var right = b.FirstOrDefault(c => c.TypeName == left.TypeName);
			if (right is null || left.Fields.Count != right.Fields.Count)
			{
				return false;
			}

			foreach (var leftField in left.Fields)
			{
				var rightField = right.Fields.FirstOrDefault(f => f.Name == leftField.Name);
				if (rightField is null || !SameField(leftField, rightField))
				{
					return false;
				}
			}
		}

		return true;
	}

	private static bool SameField(ComponentFieldMsg a, ComponentFieldMsg b) => a.Kind switch
	{
		1 => Math.Abs(a.FloatValue - b.FloatValue) < 0.01f,
		2 => a.IntValue == b.IntValue,
		3 => a.BoolValue == b.BoolValue,
		4 => a.StringValue == b.StringValue,
		5 => a.StringList.SequenceEqual(b.StringList),
		6 => a.IntValue == b.IntValue,
		_ => false,
	};
}
