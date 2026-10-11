using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The framework-wide mod-authored liquid effect registry: it OWNS the functions
/// every mod registered, and <see cref="ModLiquidEffectsAdapter"/> is one mod's
/// write guard over its own slice (the permission gate, the ownership rail, the
/// duplicate rail).
///
/// <para>
/// It is a leaf on purpose, exactly like <see cref="ModContentStore"/> beside it:
/// the Game Adapter's liquid provider is its reader, and that provider must not
/// resolve <see cref="ModService"/> — <c>ModService</c>'s own native seams are
/// replaced BY the adapter, so depending on it would close the DI cycle
/// <c>docs/backlog/done/mod-service-gameadapter-di-cycle.md</c> removed. The store
/// is therefore registered on its own and handed to both sides.
/// </para>
///
/// <para>
/// The reader resolves at CALL time and not at build time. The vanilla registry is
/// injected when the game's own <c>Liquids.Registry</c> first exists, which is
/// unrelated to when a mod registers, so a captured delegate would make the
/// registration order load-bearing for no reason. A lookup per call has no such
/// coupling: the effect a liquid's delegate runs is whatever is registered when the
/// drink happens.
/// </para>
///
/// <para>
/// Single-threaded by construction: mods register during discovery and
/// <c>Bind</c>, and the game's own drink and limb-use call sites run on that same
/// main thread.
/// </para>
/// </summary>
public sealed class ModLiquidEffectStore
{
	private readonly Dictionary<string, ModLiquidEffectRegistration> _drink = [];
	private readonly Dictionary<string, ModLiquidEffectRegistration> _limbUse = [];

	/// <summary>How many registrations the store holds, both halves together (the binding gate's census floor).</summary>
	internal int Count => _drink.Count + _limbUse.Count;

	/// <summary>Every registration, both halves, in no particular order (the diagnostics/console view).</summary>
	internal IReadOnlyList<ModLiquidEffectRegistration> Registrations =>
		[.. _drink.Values, .. _limbUse.Values];

	/// <summary>The drink effect registered for one liquid id, or null when there is none.</summary>
	internal ModLiquidEffectRegistration? FindDrink(string liquidId) =>
		_drink.TryGetValue(liquidId, out var registration) ? registration : null;

	/// <summary>The limb-use effect registered for one liquid id, or null when there is none.</summary>
	internal ModLiquidEffectRegistration? FindLimbUse(string liquidId) =>
		_limbUse.TryGetValue(liquidId, out var registration) ? registration : null;

	/// <summary>
	/// Files one accepted registration under its liquid id. False when that half is
	/// already taken for the id — the rails that decide acceptance stay in the mod's
	/// own adapter, and the duplicate rail lives with the table so two callers can
	/// never disagree about which function runs.
	/// </summary>
	internal bool TryAdd(string modId, string liquidId, ModLiquidEffect effect, bool limbUse)
	{
		var table = limbUse ? _limbUse : _drink;
		if (table.ContainsKey(liquidId))
		{
			return false;
		}

		table.Add(liquidId, new ModLiquidEffectRegistration(modId, liquidId, effect, limbUse));
		return true;
	}

	/// <summary>
	/// True when a LIMB-USE effect is registered for this liquid id — the question the
	/// load-time diagnostic asks, because the two flags that gate the native limb paths
	/// (<c>healthUsable</c> / <c>injectable</c>) gate <c>onHealthUse</c> and nothing else.
	/// It is deliberately NOT "is this liquid registered at all": a definition that
	/// declares one of those flags while its mod registered only the DRINK half would
	/// then pass the check, and the player would apply or inject a liquid that does
	/// nothing with no line saying so.
	/// </summary>
	internal bool HasLimbUse(string liquidId) => _limbUse.ContainsKey(liquidId);

	/// <summary>
	/// Retires every registration one mod made, and returns how many were withdrawn.
	/// A mod's effect can be registered before the mod fails to load — a registration
	/// happens inside <c>Bind</c> and the load may throw after it — so a failed load
	/// has to take its slice back for the same reason
	/// <see cref="ModContentStore.RemoveMod"/> exists: the framework-wide view is
	/// what the Game Adapter's resolver reads, and an entry left behind would run the
	/// code of a mod the process never loaded.
	/// </summary>
	internal int RemoveMod(string modId)
	{
		var withdrawn = 0;
		foreach (var table in new[] { _drink, _limbUse })
		{
			// Collected first: a dictionary may not be mutated while it is enumerated.
			var owned = new List<string>();
			foreach (var entry in table)
			{
				if (string.Equals(entry.Value.ModId, modId, StringComparison.Ordinal))
				{
					owned.Add(entry.Key);
				}
			}

			foreach (var liquidId in owned)
			{
				table.Remove(liquidId);
				withdrawn++;
			}
		}

		return withdrawn;
	}
}
