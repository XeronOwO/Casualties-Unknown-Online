using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Resolves Minecraft-style player selectors against CUO's entity table.
/// The console presents the same selector vocabulary as the argument provider
/// (<c>@a</c>, <c>@p</c>, <c>@s</c>, <c>@e</c>, <c>@r</c>), while the actual
/// expansion stays Unity-free and testable.
/// 
/// CUO's current entity domain only contains player bodies, so <c>@e</c> is a
/// player-entity alias. <c>@s</c> selects the local body; the other selectors
/// target remote peers because the co-op interaction commands act on the
/// player you are helping.
/// </summary>
public static class CommandSelectorResolver
{
	/// <summary>One selectable player entity in the resolver's input surface.</summary>
	public readonly record struct Target(ulong SteamId, bool IsLocal, NetVector2 Position);

	/// <summary>Expands a selector token to matching SteamIds, or an empty list for an unknown/no-match selector.</summary>
	public static IReadOnlyList<ulong> Resolve(string? selector, IReadOnlyList<Target> targets)
	{
		if (string.IsNullOrWhiteSpace(selector))
		{
			return [];
		}

		if (targets.Count == 0)
		{
			return [];
		}

		var name = selector!.Trim();
		if (name.Length < 2 || name[0] != '@')
		{
			return [];
		}

		var remote = targets.Where(t => !t.IsLocal).ToList();
		return name.Substring(1).ToLowerInvariant() switch
		{
			"a" => [.. remote.Select(t => t.SteamId)],
			"e" => [.. remote.Select(t => t.SteamId)],
			"s" => [.. targets.Where(t => t.IsLocal).Select(t => t.SteamId)],
			"p" => NearestRemote(remote, targets),
			"r" => RandomRemote(remote),
			_ => [],
		};
	}

	private static IReadOnlyList<ulong> NearestRemote(IReadOnlyList<Target> remote, IReadOnlyList<Target> all)
	{
		if (remote.Count == 0)
		{
			return [];
		}

		var local = all.FirstOrDefault(t => t.IsLocal);
		var origin = local.IsLocal ? local.Position : NetVector2.Zero;
		var nearest = remote
			.OrderBy(t => DistanceSquared(origin, t.Position))
			.First();
		return [nearest.SteamId];
	}

	private static IReadOnlyList<ulong> RandomRemote(IReadOnlyList<Target> remote)
	{
		if (remote.Count == 0)
		{
			return [];
		}

		var index = new Random().Next(remote.Count);
		return [remote[index].SteamId];
	}

	private static float DistanceSquared(NetVector2 a, NetVector2 b)
	{
		var dx = a.X - b.X;
		var dy = a.Y - b.Y;
		return (dx * dx) + (dy * dy);
	}
}
