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
/// <para>
/// THE MEANINGS ARE MINECRAFT'S, the template the console family follows (<c>AGENTS.md</c> rule 17): <c>@a</c> is EVERY
/// player, the one who typed the command included; <c>@p</c> is the nearest player to the executor, and at distance zero
/// that is the executor; <c>@s</c> is the executor alone; <c>@r</c> is one random player. A selector set that silently
/// dropped the sender made the most convenient form of every cheat verb unusable on the client an operator debugs most —
/// the host — which is the reported "<c>/heal @a</c> does not heal me" (ticket
/// <c>selectors-exclude-the-executor</c>).
/// </para>
/// <para>
/// CUO's entity domain only contains player bodies, so <c>@e</c> is a player-entity alias and answers like <c>@a</c>.
/// </para>
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

		var local = targets.FirstOrDefault(t => t.IsLocal);
		var origin = local.IsLocal ? (NetVector2?)local.Position : null;

		// THE FILTER COMES FIRST, then the base selector — Minecraft's own order: a bracketed filter narrows the candidate
		// set the selector then picks from, so `@p[distance=1..]` is "the nearest player who is NOT standing on me" rather
		// than "the nearest player, if it happens to be far enough". The old order made every filtered @p/@r answer empty as
		// soon as the executor stood nearest, which is the shape that hid the executor in the first place.
		var eligible = targets.Where(t => filter.Matches(t, origin)).ToList();
		var candidates = SelectBase(typeToken, eligible);
		if (candidates.Count == 0)
		{
			return [];
		}

		var sorted = ApplySort(filter.Sort, candidates, origin ?? NetVector2.Zero);

		if (filter.Limit is { } limit && sorted.Count > limit)
		{
			sorted = [.. sorted.Take(limit)];
		}

		return [.. sorted.Select(t => t.SteamId)];
	}

	private static IReadOnlyList<Target> SelectBase(string typeToken, IReadOnlyList<Target> targets)
	{
		return typeToken.ToLowerInvariant() switch
		{
			// Minecraft's own meanings: @a is every player INCLUDING the executor, @e is the entity alias for the same set
			// (this project's entity domain is players), @s is the executor alone, @p is the nearest — which at distance zero
			// is the executor — and @r is one of everyone.
			"a" => targets,
			"e" => targets,
			"s" => [.. targets.Where(t => t.IsLocal)],
			"p" => Nearest(targets),
			"r" => RandomOf(targets),
			_ => [],
		};
	}

	private static IReadOnlyList<Target> Nearest(IReadOnlyList<Target> targets)
	{
		if (targets.Count == 0)
		{
			return [];
		}

		var local = targets.FirstOrDefault(t => t.IsLocal);
		var origin = local.IsLocal ? local.Position : NetVector2.Zero;
		var nearest = targets
			.OrderBy(t => DistanceSquared(origin, t.Position))
			.First();
		return [nearest];
	}

	private static IReadOnlyList<Target> RandomOf(IReadOnlyList<Target> targets)
	{
		if (targets.Count == 0)
		{
			return [];
		}

		var index = new Random().Next(targets.Count);
		return [targets[index]];
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
