using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Binds <see cref="ModTileDefinition"/> payloads from shared-content mods into
/// the vanilla world palette. Each definition receives a deterministic custom
/// block index (never a vanilla index), gets a Unity <see cref="Tile"/> built by
/// <see cref="CustomTileFactory"/>, and is served through the
/// <c>WorldGeneration.GetBlockInfo</c> patch as the matching vanilla
/// <see cref="BlockInfo"/>. Static content is local-only: no wire, no random
/// world generation.
/// </summary>
public sealed class GameAdapterTileContentProvider(
	ILogger<GameAdapterTileContentProvider> log) : IContentBindingProvider, ICuoService
{
	private const ushort FirstCustomTileIndex = 36;
	private const int CustomTileIndexCount = ushort.MaxValue - FirstCustomTileIndex + 1;

	private readonly ILogger<GameAdapterTileContentProvider> _log = log;
	private readonly Dictionary<string, ModTileDefinition> _definitions = [];
	private readonly Dictionary<string, ushort> _indicesById = [];
	private readonly Dictionary<ushort, string> _idsByIndex = [];
	private readonly HashSet<string> _injectedIds = [];
	private readonly HashSet<string> _failedIds = [];
	private WorldGeneration? _lastWorld;
	private TileBase[]? _lastTiles;

	/// <inheritdoc />
	public string Kind => ModContentKind.Tile;

	/// <inheritdoc />
	public bool TryBind(ModContentRegistration registration)
	{
		if (!string.Equals(registration.Definition.Kind, Kind, StringComparison.Ordinal))
		{
			return false;
		}

		var definition = ModTileDefinition.FromPayload(registration.Definition.Data);
		if (definition is null)
		{
			_log.LogWarning(
				"[TileContent] {ModId}/{Id} payload is not a valid ModTileDefinition — refused.",
				registration.ModId, registration.Definition.Id);
			return false;
		}

		var id = registration.Definition.Id;
		if (string.IsNullOrWhiteSpace(id))
		{
			_log.LogWarning("[TileContent] {ModId} registered a tile with an empty id — refused.", registration.ModId);
			return false;
		}

		if (string.IsNullOrWhiteSpace(definition.SpritePath) && definition.TemplateTileIndex is not (> 0 and <= ushort.MaxValue))
		{
			_log.LogWarning(
				"[TileContent] {ModId}/{Id} has neither SpritePath nor a valid TemplateTileIndex — refused (a static tile needs a visual source).",
				registration.ModId, id);
			return false;
		}

		if (definition.TemplateTileIndex is { } templateIndex && templateIndex <= 0)
		{
			_log.LogWarning(
				"[TileContent] {ModId}/{Id} TemplateTileIndex must be a positive vanilla block index — refused.",
				registration.ModId, id);
			return false;
		}

		if (_definitions.ContainsKey(id))
		{
			_log.LogWarning(
				"[TileContent] {ModId}/{Id} is already registered by another tile-content provider/definition — refused.",
				registration.ModId, id);
			return false;
		}

		if (!TryValidateDefinition(id, definition))
		{
			return false;
		}

		_definitions.Add(id, definition);
		_log.LogInformation(
			"[TileContent] accepted {ModId}/{Id} (schema {SchemaVersion}); injection waits for the world tile palette.",
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
		var world = WorldGeneration.world;
		if (world is null) // Unity object — ==
		{
			_lastWorld = null;
			_lastTiles = null;
			return;
		}

		if (!ReferenceEquals(_lastWorld, world))
		{
			_lastWorld = world;
			_lastTiles = null;
			_injectedIds.Clear(); // a new world gets a fresh tile palette; re-inject every accepted definition
		}

		if (world.tiles is null)
		{
			return;
		}

		if (!ReferenceEquals(_lastTiles, world.tiles))
		{
			_lastTiles = world.tiles;
			_injectedIds.Clear();
		}

		foreach (var pair in _definitions.ToArray())
		{
			EnsureInjected(pair.Key, pair.Value, world);
		}
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
	}

	/// <summary>
	/// Returns the custom <see cref="BlockInfo"/> for a registered custom tile
	/// index, or null for vanilla indices. Harmony calls this from the
	/// <c>WorldGeneration.GetBlockInfo</c> prefix and lets the original switch
	/// handle every vanilla block.
	/// </summary>
	internal BlockInfo? TryGetBlockInfo(ushort block)
	{
		if (!_idsByIndex.TryGetValue(block, out var id)
			|| !_definitions.TryGetValue(id, out var definition))
		{
			return null;
		}

		return BuildBlockInfo(id, definition);
	}

	/// <summary>Resolve the stable content id to its allocated custom block index.</summary>
	internal bool TryGetTileIndex(string id, out ushort index) =>
		_indicesById.TryGetValue(id, out index);

	/// <summary>Resolve the original typed definition by stable content id.</summary>
	internal bool TryGetDefinition(string id, out ModTileDefinition definition) =>
		_definitions.TryGetValue(id, out definition!);

	/// <summary>Resolve a bound definition by its allocated custom block index.</summary>
	internal bool TryGetDefinitionByIndex(ushort block, out ModTileDefinition definition)
	{
		definition = null!;
		return _idsByIndex.TryGetValue(block, out var id) && _definitions.TryGetValue(id, out definition!);
	}

	/// <summary>
	/// Snapshot every accepted tile definition in stable id order for deterministic
	/// world-generation distribution. Both sides must iterate the same set in the
	/// same order when consuming the shared generation random stream.
	/// </summary>
	internal IReadOnlyList<KeyValuePair<string, ModTileDefinition>> GetDefinitionsForWorldGen() =>
		[.. _definitions.OrderBy(pair => pair.Key, StringComparer.Ordinal)];

	/// <summary>
	/// Spawn the authored custom drops for a broken custom tile. Returns true when
	/// at least one drop was created. The caller is expected to be inside the
	/// <c>DamageBlockOrigin</c> scope so the newly created items fold into the
	/// pending block-break report like vanilla block drops.
	/// </summary>
	internal bool TrySpawnDrops(WorldGeneration world, Vector2Int cell, ushort block)
	{
		if (world is null) // Unity object — ==
		{
			return false;
		}

		if (!_idsByIndex.TryGetValue(block, out var id) || !_definitions.TryGetValue(id, out var definition))
		{
			return false;
		}

		if (definition.Drops is not { Count: > 0 })
		{
			return false;
		}

		var worldPosition = world.BlockToWorldPos(cell);
		var spawned = 0;
		foreach (var drop in definition.Drops)
		{
			if (drop is null || string.IsNullOrWhiteSpace(drop.ItemId))
			{
				_log.LogWarning("[TileContent] {Id} contains an invalid empty drop entry — skipped.", id);
				continue;
			}

			if (Random.Range(0f, 1f) >= Mathf.Clamp01(drop.Chance))
			{
				continue;
			}

			try
			{
				var created = Utils.Create(drop.ItemId, worldPosition, 0f);
				if (created is null) // Unity object — ==
				{
					_log.LogWarning("[TileContent] {Id} drop {DropId} could not be created — skipped.", id, drop.ItemId);
					continue;
				}

				var item = created.GetComponent<Item>();
				if (item != null) // Unity object — ==
				{
					item.condition = drop.RollCondition(Random.Range(0f, 1f));
				}

				spawned++;
			}
			catch (Exception ex)
			{
				_log.LogWarning(ex, "[TileContent] {Id} drop {DropId} failed to spawn — skipped.", id, drop.ItemId);
			}
		}

		if (spawned > 0)
		{
			_log.LogInformation("[TileContent] {Id} spawned {Count} drop(s) at ({X},{Y}).", id, spawned, cell.x, cell.y);
		}

		return spawned > 0;
	}

	/// <summary>
	/// Resolve a custom tile id for runtime placement and ensure it is injected
	/// into the current world palette before the caller writes it with
	/// <c>SetBlock</c>. Returns false for unknown/failed definitions or when the
	/// world/tile palette is not ready.
	/// </summary>
	internal bool TryPrepareForPlacement(string id, WorldGeneration world, out ushort index)
	{
		index = 0;
		if (world is null || world.tiles is null || !_definitions.TryGetValue(id, out var definition))
		{
			return false;
		}

		if (_failedIds.Contains(id))
		{
			return false;
		}

		if (!_injectedIds.Contains(id))
		{
			EnsureInjected(id, definition, world);
		}

		return _injectedIds.Contains(id) && _indicesById.TryGetValue(id, out index);
	}

	private void EnsureInjected(string id, ModTileDefinition definition, WorldGeneration world)
	{
		if (_injectedIds.Contains(id) || _failedIds.Contains(id))
		{
			return;
		}

		if (!TryEnsureIndex(id, out var index))
		{
			return;
		}

		var tile = CustomTileFactory.Create(id, definition, world, _log);
		if (tile is null) // Unity object — ==
		{
			_failedIds.Add(id);
			return;
		}

		var requiredLength = (int)index + 1;
		if (world.tiles.Length < requiredLength)
		{
			Array.Resize(ref world.tiles, requiredLength);
		}

		world.tiles[index] = tile;
		_injectedIds.Add(id);
		ApplyLocale(id, definition);
		_log.LogInformation(
			"[TileContent] injected {Id} at block index {Index} (health {Health}, collider {Collider}).",
			id, index, definition.Health, definition.ColliderType);
	}

	private bool TryEnsureIndex(string id, out ushort index)
	{
		if (_indicesById.TryGetValue(id, out index))
		{
			return true;
		}

		if (_failedIds.Contains(id))
		{
			index = 0;
			return false;
		}

		var start = (int)(FirstCustomTileIndex + StableHash(id) % CustomTileIndexCount);
		for (var offset = 0; offset < CustomTileIndexCount; offset++)
		{
			var candidate = (ushort)(FirstCustomTileIndex + (start - FirstCustomTileIndex + offset) % CustomTileIndexCount);
			if (_idsByIndex.ContainsKey(candidate))
			{
				continue;
			}

			_idsByIndex.Add(candidate, id);
			_indicesById.Add(id, candidate);
			index = candidate;
			return true;
		}

		_failedIds.Add(id);
		index = 0;
		_log.LogError("[TileContent] {Id} could not allocate a custom block index.", id);
		return false;
	}

	private bool TryValidateDefinition(string id, ModTileDefinition definition)
	{
		if (float.IsNaN(definition.SpawnAmount) || float.IsInfinity(definition.SpawnAmount) || definition.SpawnAmount < 0f)
		{
			_log.LogWarning("[TileContent] {Id} has invalid SpawnAmount {SpawnAmount} — refused.", id, definition.SpawnAmount);
			return false;
		}

		foreach (var drop in definition.Drops ?? [])
		{
			if (drop is null || string.IsNullOrWhiteSpace(drop.ItemId))
			{
				_log.LogWarning("[TileContent] {Id} has an empty tile-drop item id — refused.", id);
				return false;
			}

			if (float.IsNaN(drop.Chance) || float.IsInfinity(drop.Chance) || drop.Chance < 0f || drop.Chance > 1f)
			{
				_log.LogWarning("[TileContent] {Id} drop {DropId} has invalid Chance {Chance} — refused.", id, drop.ItemId, drop.Chance);
				return false;
			}

			if (float.IsNaN(drop.MinCondition) || float.IsInfinity(drop.MinCondition)
				|| float.IsNaN(drop.MaxCondition) || float.IsInfinity(drop.MaxCondition)
				|| drop.MinCondition < 0f || drop.MinCondition > 1f
				|| drop.MaxCondition < 0f || drop.MaxCondition > 1f
				|| drop.MaxCondition < drop.MinCondition)
			{
				_log.LogWarning(
					"[TileContent] {Id} drop {DropId} has invalid condition range [{Min}..{Max}] — refused.",
					id, drop.ItemId, drop.MinCondition, drop.MaxCondition);
				return false;
			}
		}

		return true;
	}

	private static BlockInfo BuildBlockInfo(string id, ModTileDefinition definition)
	{
		return new BlockInfo
		{
			name = Locale.GetOther(id),
			health = definition.Health,
			hitsound = definition.HitSound ?? string.Empty,
			stepsound = definition.StepSound ?? string.Empty,
			sleep = (Body.SleepQuality)definition.SleepQuality,
			noVariation = definition.NoVariation,
			metallic = definition.Metallic,
			toxicity = definition.Toxicity,
			slippery = definition.Slippery
		};
	}

	private static void ApplyLocale(string id, ModTileDefinition definition)
	{
		if (Locale.currentLang is null)
		{
			Locale.LoadLanguage();
		}

		if (Locale.currentLang is null)
		{
			return;
		}

		if (!string.IsNullOrWhiteSpace(definition.DisplayName))
		{
			Locale.currentLang.other[id] = definition.DisplayName;
		}

		if (!string.IsNullOrWhiteSpace(definition.Description))
		{
			Locale.currentLang.other[id + "dsc"] = definition.Description;
		}
	}

	private static uint StableHash(string id)
	{
		var hash = 2166136261u;
		foreach (var character in id.ToUpperInvariant())
		{
			hash ^= character;
			hash *= 16777619u;
		}

		return hash;
	}
}
