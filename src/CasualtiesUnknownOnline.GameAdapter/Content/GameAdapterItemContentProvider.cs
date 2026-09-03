using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The first concrete content binding provider: it turns
/// <see cref="ModItemDefinition"/> payloads from shared-content mods into vanilla
/// <c>ItemInfo</c> entries and, when the DTO supplies a <c>TemplateId</c>, into
/// a runtime <c>GameObject</c> template so CUO's restore/spawn paths can
/// materialize the custom item without exposing game types to mods. Static
/// registration waits for <c>Item.GlobalItems</c>; template construction uses
/// <see cref="CustomItemTemplateFactory"/> and the resolved templates are
/// served through <see cref="TryResolveTemplate"/>.
/// </summary>
public sealed class GameAdapterItemContentProvider(
	ILogger<GameAdapterItemContentProvider> log) : IContentBindingProvider, ICuoService
{
	private readonly ILogger<GameAdapterItemContentProvider> _log = log;
	private readonly Dictionary<string, ModItemDefinition> _definitions = [];
	private readonly Dictionary<string, GameObject> _templates = [];
	private readonly HashSet<string> _templateFailures = [];
	private readonly HashSet<string> _lootPoolIds = [];
	private readonly Dictionary<ModItemDropSource, HashSet<string>> _dropSourceSeeded = [];
	private Dictionary<string, List<string>>? _lastLootPool;

	private static readonly ModItemDropSource[] SingleDropSources =
	[
		ModItemDropSource.Corpse,
		ModItemDropSource.MedicalCrate,
		ModItemDropSource.FoodCrate,
		ModItemDropSource.ContainerCrate,
		ModItemDropSource.Trader1,
		ModItemDropSource.Trader2,
		ModItemDropSource.Trader3,
		ModItemDropSource.DropCapsule,
		ModItemDropSource.CapsuleContainer
	];

	/// <inheritdoc />
	public string Kind => ModContentKind.Item;

	/// <inheritdoc />
	public bool TryBind(ModContentRegistration registration)
	{
		if (!string.Equals(registration.Definition.Kind, Kind, StringComparison.Ordinal))
		{
			return false;
		}

		var definition = ModItemDefinition.FromPayload(registration.Definition.Data);
		if (definition is null)
		{
			_log.LogWarning(
				"[ItemContent] {ModId}/{Id} payload is not a valid ModItemDefinition — refused.",
				registration.ModId, registration.Definition.Id);
			return false;
		}

		var id = registration.Definition.Id;
		if (string.IsNullOrWhiteSpace(id))
		{
			_log.LogWarning("[ItemContent] {ModId} registered an item with an empty id — refused.", registration.ModId);
			return false;
		}

		if (_definitions.ContainsKey(id))
		{
			_log.LogWarning(
				"[ItemContent] {ModId}/{Id} is already registered by another item-content provider/definition — refused.",
				registration.ModId, id);
			return false;
		}

		if (definition.WorldSpawnPerChunk is { } perChunk
			&& (float.IsNaN(perChunk) || float.IsInfinity(perChunk) || perChunk < 0f))
		{
			_log.LogWarning(
				"[ItemContent] {ModId}/{Id} has invalid WorldSpawnPerChunk {PerChunk} — refused.",
				registration.ModId, id, perChunk);
			return false;
		}

		_definitions.Add(id, definition);
		_log.LogInformation(
			"[ItemContent] accepted {ModId}/{Id} (schema {SchemaVersion}); injection waits for the vanilla item table.",
			registration.ModId, id, registration.Definition.SchemaVersion);
		return true;
	}

	public void Initialize()
	{
	}

	public void Start()
	{
	}

	public void Update()
	{
		if (Item.GlobalItems is null) // static game table not ready yet
		{
			return;
		}

		foreach (var pair in _definitions.ToArray())
		{
			if (!Item.GlobalItems.ContainsKey(pair.Key))
			{
				Item.GlobalItems.Add(pair.Key, BuildItemInfo(pair.Key, pair.Value));
				_log.LogInformation("[ItemContent] injected {Id} into Item.GlobalItems.", pair.Key);
			}

			EnsureTemplate(pair.Key, pair.Value);
			EnsureLootPool(pair.Key, pair.Value);
			EnsureDropSources(pair.Key, pair.Value);
		}
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
	}

	/// <summary>
	/// Resolve a custom runtime item template. Returns false when the id has no
	/// template (either the DTO did not request one or the build failed).
	/// </summary>
	internal bool TryResolveTemplate(string id, out GameObject? template)
	{
		if (_templates.TryGetValue(id, out var cached) && cached != null) // Unity object — ==
		{
			template = cached;
			return true;
		}

		if (_templates.Remove(id))
		{
			_log.LogDebug("[ItemContent] removed a destroyed runtime template for {Id}.", id);
		}

		template = null;
		return false;
	}

	/// <summary>
	/// Snapshot every accepted item definition with a positive
	/// <c>WorldSpawnPerChunk</c> in stable id order for deterministic world-gen
	/// distribution. Both sides must iterate the same set in the same order when
	/// consuming the shared generation random stream.
	/// </summary>
	internal IReadOnlyList<KeyValuePair<string, ModItemDefinition>> GetDefinitionsForWorldSpawn() =>
		[.. _definitions
			.Where(pair => pair.Value.WorldSpawnPerChunk is > 0f)
			.OrderBy(pair => pair.Key, StringComparer.Ordinal)];

	/// <summary>
	/// Add a bound custom item to the vanilla category loot pool so corpses,
	/// building-entity guaranteed drops, traders and dev-console spawners see it
	/// the same way they see vanilla items. The game builds the pool once from
	/// <c>Item.GlobalItems</c>, so items bound after that call must be injected
	/// here; re-injection is idempotent and a replaced pool is re-seeded. A
	/// positive <c>WorldSpawnPerChunk</c> opts the item out of the generic
	/// category pool (it appears only as a world spawn), matching CUCoreLib's
	/// fallback rule.
	/// </summary>
	private void EnsureLootPool(string id, ModItemDefinition definition)
	{
		var pool = ItemLootPool.pool;
		if (!ReferenceEquals(_lastLootPool, pool))
		{
			_lastLootPool = pool;
			_lootPoolIds.Clear();
			_dropSourceSeeded.Clear();
		}

		if (pool is null)
		{
			return;
		}

		if (definition.WorldSpawnPerChunk is > 0f || definition.DropSources is not null)
		{
			// The game's own ItemLootPool.InitializePool rebuilds from
			// Item.GlobalItems and would otherwise place an explicit-source or
			// world-spawn-only item into its vanilla category. Remove that
			// generic entry so the authored source selections stay authoritative.
			RemoveFromLootCategory(pool, definition.Category ?? string.Empty, id);
			return;
		}

		if (_lootPoolIds.Contains(id))
		{
			return;
		}

		var category = definition.Category ?? string.Empty;
		if (string.IsNullOrWhiteSpace(category) || string.Equals(category, "nospawn", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		if (!pool.TryGetValue(category, out var entries))
		{
			entries = [];
			pool.Add(category, entries);
		}

		if (entries.Contains(id))
		{
			_lootPoolIds.Add(id);
			return;
		}

		var frequency = Math.Max(0, definition.SpawnFrequency);
		for (var i = 0; i < frequency; i++)
		{
			entries.Add(id);
		}

		_lootPoolIds.Add(id);
		_log.LogInformation("[ItemContent] added {Id} to loot category {Category} (frequency {Frequency}).",
			id, category, frequency);
	}

	/// <summary>
	/// Add a bound custom item to the explicit fixed drop-source pools selected
	/// by the mod. Each active source gets its own synthetic <c>ItemLootPool</c>
	/// category (not a vanilla category), so corpse/crate/trader patches can opt
	/// that source into the existing vanilla loot machinery without exposing a
	/// game type to mods. The item is deliberately NOT added to its generic
	/// category pool when a mod explicitly chooses fixed sources.
	/// </summary>
	private void EnsureDropSources(string id, ModItemDefinition definition)
	{
		if (definition.DropSources is not { } sources)
		{
			return;
		}

		var pool = ItemLootPool.pool;
		if (pool is null)
		{
			return;
		}

		var frequency = Math.Max(0, definition.SpawnFrequency);
		if (frequency <= 0 || sources == ModItemDropSource.None)
		{
			return;
		}

		foreach (var source in SingleDropSources)
		{
			if ((sources & source) == 0)
			{
				continue;
			}

			var category = GetDropSourceCategory(source);
			if (string.IsNullOrEmpty(category))
			{
				continue;
			}

			if (!_dropSourceSeeded.TryGetValue(source, out var seeded))
			{
				seeded = [];
				_dropSourceSeeded.Add(source, seeded);
			}

			if (seeded.Contains(id))
			{
				continue;
			}

			if (!pool.TryGetValue(category, out var entries))
			{
				entries = [];
				pool.Add(category, entries);
			}

			for (var i = 0; i < frequency; i++)
			{
				entries.Add(id);
			}

			seeded.Add(id);
			_log.LogInformation("[ItemContent] added {Id} to fixed drop source {Source} (frequency {Frequency}).",
				id, source, frequency);
		}
	}

	/// <summary>
	/// Resolve the synthetic loot-pool category that holds items for a single
	/// fixed drop source. Returns false when no items have been registered for
	/// that source (or the pool is not ready).
	/// </summary>
	internal bool TryGetDropSourceCategory(ModItemDropSource source, out string category)
	{
		category = GetDropSourceCategory(source);
		if (string.IsNullOrEmpty(category))
		{
			return false;
		}

		var pool = ItemLootPool.pool;
		if (pool is null || !pool.TryGetValue(category, out var entries) || entries.Count == 0)
		{
			return false;
		}

		return true;
	}

	private static void RemoveFromLootCategory(
		Dictionary<string, List<string>> pool,
		string category,
		string id)
	{
		if (string.IsNullOrWhiteSpace(category) || !pool.TryGetValue(category, out var entries))
		{
			return;
		}

		entries.RemoveAll(entry => string.Equals(entry, id, StringComparison.Ordinal));
	}

	private static string GetDropSourceCategory(ModItemDropSource source) =>
		source switch
		{
			ModItemDropSource.Corpse => "cuo_drop_corpse",
			ModItemDropSource.MedicalCrate => "cuo_drop_medical_crate",
			ModItemDropSource.FoodCrate => "cuo_drop_food_crate",
			ModItemDropSource.ContainerCrate => "cuo_drop_container_crate",
			ModItemDropSource.Trader1 => "cuo_drop_trader1",
			ModItemDropSource.Trader2 => "cuo_drop_trader2",
			ModItemDropSource.Trader3 => "cuo_drop_trader3",
			ModItemDropSource.DropCapsule => "cuo_drop_capsule",
			ModItemDropSource.CapsuleContainer => "cuo_drop_capsule_container",
			_ => string.Empty
		};

	private void EnsureTemplate(string id, ModItemDefinition definition)
	{
		if (string.IsNullOrWhiteSpace(definition.TemplateId) || _templates.ContainsKey(id))
		{
			return;
		}

		if (_templateFailures.Contains(id))
		{
			return;
		}

		if (Resources.Load<GameObject>(id) != null) // Unity object — ==; a vanilla prefab already exists, never shadow it
		{
			_log.LogDebug("[ItemContent] {Id} resolves as a vanilla prefab; no runtime template is built.", id);
			return;
		}

		var template = CustomItemTemplateFactory.Create(id, definition, _log);
		if (template is null)
		{
			_templateFailures.Add(id);
			_log.LogWarning("[ItemContent] no runtime template was built for {Id}.", id);
			return;
		}

		_templates.Add(id, template);
		_log.LogInformation(
			"[ItemContent] built runtime template for {Id} (base {TemplateId}, components {ComponentCount}).",
			id, definition.TemplateId, definition.SpawnComponents.Count);
	}

	private static ItemInfo BuildItemInfo(string id, ModItemDefinition definition)
	{
		var info = new ItemInfo
		{
			fullName = string.IsNullOrWhiteSpace(definition.DisplayName) ? id : definition.DisplayName,
			description = definition.Description ?? string.Empty,
			category = string.IsNullOrWhiteSpace(definition.Category) ? "nospawn" : definition.Category,
			weight = definition.Weight,
			value = definition.Value,
			usable = definition.Usable,
			usableWithLMB = definition.UsableWithLmb,
			wearable = definition.Wearable,
			destroyAtZeroCondition = definition.DestroyAtZeroCondition,
			tags = definition.Tags ?? string.Empty
		};

		if (!string.IsNullOrWhiteSpace(definition.Tags))
		{
			info.SetTags();
		}

		return info;
	}
}
