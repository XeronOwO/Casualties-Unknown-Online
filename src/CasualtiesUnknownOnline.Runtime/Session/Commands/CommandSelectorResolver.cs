using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Resolves Minecraft-style player selectors against CUO's entity table.
/// Supports the base selectors (<c>@a</c>, <c>@p</c>, <c>@s</c>, <c>@e</c>,
/// <c>@r</c>) and bracketed filters (<c>type</c>, <c>name</c>, <c>distance</c>,
/// <c>limit</c>, <c>sort</c>). The console presents the same vocabulary as the
/// argument provider, while the actual expansion stays Unity-free and testable.
/// 
/// CUO's current entity domain only contains player bodies, so <c>@e</c> is a
/// player-entity alias. <c>@s</c> selects the local body; the other selectors
/// target remote peers because the co-op interaction commands act on the
/// player you are helping.
/// </summary>
public static class CommandSelectorResolver
{
	/// <summary>One selectable player entity in the resolver's input surface.</summary>
	public readonly record struct Target(ulong SteamId, bool IsLocal, NetVector2 Position, string? DisplayName = null);

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

		var open = name.IndexOf('[');
		if (open >= 0 && !name.EndsWith("]", StringComparison.Ordinal))
		{
			return [];
		}

		var typeToken = open < 0
			? name.Substring(1)
			: name.Substring(1, open - 1);
		var filterText = open < 0
			? string.Empty
			: name.Substring(open + 1, name.Length - open - 2);

		if (!CommandSelectorFilterParser.TryParse(filterText, out var filter))
		{
			return [];
		}

		var candidates = SelectBase(typeToken, targets);
		if (candidates.Count == 0)
		{
			return [];
		}

		var local = targets.FirstOrDefault(t => t.IsLocal);
		var origin = local.IsLocal ? (NetVector2?)local.Position : null;
		var filtered = candidates.Where(t => filter.Matches(t, origin)).ToList();
		var sorted = ApplySort(filter.Sort, filtered, origin ?? NetVector2.Zero);

		if (filter.Limit is { } limit && sorted.Count > limit)
		{
			sorted = [.. sorted.Take(limit)];
		}

		return [.. sorted.Select(t => t.SteamId)];
	}

	private static IReadOnlyList<Target> SelectBase(string typeToken, IReadOnlyList<Target> targets)
	{
		var remote = targets.Where(t => !t.IsLocal).ToList();
		return typeToken.ToLowerInvariant() switch
		{
			"a" => remote,
			"e" => remote,
			"s" => [.. targets.Where(t => t.IsLocal)],
			"p" => NearestRemote(remote, targets),
			"r" => RandomRemote(remote),
			_ => [],
		};
	}

	private static IReadOnlyList<Target> NearestRemote(IReadOnlyList<Target> remote, IReadOnlyList<Target> all)
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
		return [nearest];
	}

	private static IReadOnlyList<Target> RandomRemote(IReadOnlyList<Target> remote)
	{
		if (remote.Count == 0)
		{
			return [];
		}

		var index = new Random().Next(remote.Count);
		return [remote[index]];
	}

	private static IReadOnlyList<Target> ApplySort(SelectorSort sort, IReadOnlyList<Target> targets, NetVector2 origin) =>
		sort switch
		{
			SelectorSort.Nearest => [.. targets.OrderBy(t => DistanceSquared(origin, t.Position))],
			SelectorSort.Furthest => [.. targets.OrderByDescending(t => DistanceSquared(origin, t.Position))],
			SelectorSort.Random => Shuffle(targets),
			_ => targets,
		};

	private static IReadOnlyList<Target> Shuffle(IReadOnlyList<Target> targets)
	{
		var list = targets.ToList();
		var random = new Random();
		for (var i = list.Count - 1; i > 0; i--)
		{
			var j = random.Next(i + 1);
			(list[i], list[j]) = (list[j], list[i]);
		}

		return list;
	}

	private static float DistanceSquared(NetVector2 a, NetVector2 b)
	{
		var dx = a.X - b.X;
		var dy = a.Y - b.Y;
		return (dx * dx) + (dy * dy);
	}
}
