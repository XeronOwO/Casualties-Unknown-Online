using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The framework-wide mod-authored item action registry: it OWNS the functions
/// every mod registered, and <see cref="ModItemActionsAdapter"/> is one mod's write
/// guard over its own slice (the permission gate, the ownership rail, the
/// reachability rail, the duplicate rail).
///
/// <para>
/// It is a leaf on purpose, exactly like <see cref="ModContentStore"/> and
/// <see cref="ModLiquidEffectStore"/> beside it: the Game Adapter's item provider is
/// its reader, and that provider must not resolve <see cref="ModService"/> —
/// <c>ModService</c>'s own native seams are replaced BY the adapter, so depending on
/// it would close the DI cycle <c>docs/backlog/done/mod-service-gameadapter-di-cycle.md</c>
/// removed. The store is therefore registered on its own and handed to both sides.
/// </para>
///
/// <para>
/// The reader resolves at CALL time and not at build time, for the same reason the
/// liquid store's does: the vanilla item table is injected when the game's own
/// <c>Item.GlobalItems</c> first exists, which is unrelated to when a mod registers,
/// so a captured delegate would make the registration order load-bearing for no
/// reason. A lookup per call has no such coupling: the action an item's delegate runs
/// is whatever is registered when the item is used.
/// </para>
///
/// <para>
/// Single-threaded by construction: mods register during discovery and <c>Bind</c>,
/// and the game's own use and limb-use call sites run on that same main thread.
/// </para>
/// </summary>
public sealed class ModItemEffectStore
{
	private readonly Dictionary<string, ModItemEffectRegistration> _use = [];
	private readonly Dictionary<string, ModItemEffectRegistration> _limbUse = [];

	/// <summary>How many registrations the store holds, both halves together — the registration cases' own census.</summary>
	internal int Count => _use.Count + _limbUse.Count;

	/// <summary>Every registration, both halves, in no particular order — the registration cases' view; the Game Adapter resolves through the per-half lookups above.</summary>
	internal IReadOnlyList<ModItemEffectRegistration> Registrations =>
		[.. _use.Values, .. _limbUse.Values];

	/// <summary>The use action registered for one item id, or null when there is none.</summary>
	internal ModItemEffectRegistration? FindUse(string itemId) =>
		_use.TryGetValue(itemId, out var registration) ? registration : null;

	/// <summary>The limb action registered for one item id, or null when there is none.</summary>
	internal ModItemEffectRegistration? FindLimbUse(string itemId) =>
		_limbUse.TryGetValue(itemId, out var registration) ? registration : null;

	/// <summary>
	/// True when a USE action is registered for this item id — the question the
	/// load-time diagnostic asks, because <c>Body.UseItem</c> runs <c>useAction</c>
	/// behind the item's own <c>usable</c> flag and nothing else. Like the liquid
	/// store's <see cref="ModLiquidEffectStore.HasLimbUse"/>, it is deliberately not
	/// "is this item registered at all": a definition that declares usability while
	/// its mod registered only the LIMB half would then pass the check and the player
	/// would use an item that does nothing with no line saying so.
	/// </summary>
	internal bool HasUse(string itemId) => _use.ContainsKey(itemId);

	/// <summary>The limb half of the same question, asked by the same diagnostic for <c>usableOnLimb</c>.</summary>
	internal bool HasLimbUse(string itemId) => _limbUse.ContainsKey(itemId);

	/// <summary>
	/// Files one accepted registration under its item id. False when that half is
	/// already taken for the id — the rails that decide acceptance stay in the mod's
	/// own adapter, and the duplicate rail lives with the table so two callers can
	/// never disagree about which function runs.
	/// </summary>
	internal bool TryAdd(string modId, string itemId, ModItemAction action, bool limbUse)
	{
		var table = limbUse ? _limbUse : _use;
		if (table.ContainsKey(itemId))
		{
			return false;
		}

		table.Add(itemId, new ModItemEffectRegistration(modId, itemId, action, limbUse));
		return true;
	}

	/// <summary>
	/// Retires every registration one mod made, and returns how many were withdrawn.
	/// A mod's action can be registered before the mod fails to load — a registration
	/// happens inside <c>Bind</c> and the load may throw after it — so a failed load
	/// has to take its slice back for the same reason
	/// <see cref="ModContentStore.RemoveMod"/> exists: the framework-wide view is
	/// what the Game Adapter's resolver reads, and an entry left behind would run the
	/// code of a mod the process never loaded.
	/// </summary>
	internal int RemoveMod(string modId)
	{
		var withdrawn = 0;
		foreach (var table in new[] { _use, _limbUse })
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

			foreach (var itemId in owned)
			{
				table.Remove(itemId);
				withdrawn++;
			}
		}

		return withdrawn;
	}
}
