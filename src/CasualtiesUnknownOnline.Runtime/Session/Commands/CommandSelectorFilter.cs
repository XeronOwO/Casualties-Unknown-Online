using System;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Parsed selector filters from a Minecraft-style bracketed selector
/// (<c>@a[type=player,name=John,distance=10..20,limit=1,sort=nearest]</c>).
/// Unknown keys fail the parse so a typo does not silently expand to everyone.
/// </summary>
internal sealed record CommandSelectorFilter(
	string? Type,
	string? Name,
	float? DistanceMin,
	float? DistanceMax,
	int? Limit,
	SelectorSort Sort)
{
	public static CommandSelectorFilter None { get; } = new(null, null, null, null, null, SelectorSort.None);

	public bool Matches(CommandSelectorResolver.Target target, NetVector2? origin)
	{
		if (Type is not null && !IsTypeMatch(Type))
		{
			return false;
		}

		if (Name is not null
			&& (target.DisplayName is null
				|| !string.Equals(target.DisplayName.Trim(), Name, StringComparison.OrdinalIgnoreCase)))
		{
			return false;
		}

		if (DistanceMin is not null || DistanceMax is not null)
		{
			var originPosition = origin ?? NetVector2.Zero;
			var distance = (float)Math.Sqrt(DistanceSquared(originPosition, target.Position));
			var min = DistanceMin ?? 0f;
			var max = DistanceMax ?? float.PositiveInfinity;
			if (distance < min || distance > max)
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsTypeMatch(string type) =>
		string.Equals(type, "player", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(type, "cuo:player", StringComparison.OrdinalIgnoreCase);

	private static float DistanceSquared(NetVector2 a, NetVector2 b)
	{
		var dx = a.X - b.X;
		var dy = a.Y - b.Y;
		return (dx * dx) + (dy * dy);
	}
}
