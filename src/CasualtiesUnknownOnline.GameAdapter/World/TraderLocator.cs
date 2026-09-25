using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Position-keyed trader lookup — the identity the trade domain keys its messages
/// by: both sides generated the same trader at the same place
/// (<c>WorldGeneration.cs:3438-3447</c>), so the transform position is the identity
/// and the matching tolerance is the one the trade domain's own lookups use.
/// </summary>
internal static class TraderLocator
{
	/// <summary>The matching tolerance of <see cref="FindAt"/> (a trader's transform is the position key).</summary>
	internal const float PositionTolerance = 2f;

	/// <summary>The trader standing at the wire position, or null when this scene has none there.</summary>
	internal static TraderScript? FindAt(NetVector2Msg position)
	{
		var target = new Vector2(position.X, position.Y);
		foreach (var trader in Object.FindObjectsOfType<TraderScript>()) // Unity object registry
		{
			if (Vector2.Distance(trader.transform.position, target) < PositionTolerance)
			{
				return trader;
			}
		}

		return null;
	}
}
