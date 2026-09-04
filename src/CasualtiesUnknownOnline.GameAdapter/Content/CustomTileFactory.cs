using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.Tilemaps;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Builds the in-memory <see cref="TileBase"/> asset for a custom tile
/// definition. The Game Adapter creates a Unity <see cref="Tile"/> instead of
/// exposing Unity types to mods: the DTO either names a resource sprite path
/// or reuses a vanilla tile's sprite as the visual base. The tile is not
/// persisted — it lives only in the current <c>WorldGeneration.tiles</c>
/// palette and is rebuilt whenever a new world/tile array is created.
/// </summary>
internal static class CustomTileFactory
{
	internal static TileBase? Create(
		string id,
		ModTileDefinition definition,
		WorldGeneration world,
		ILogger log)
	{
		var sprite = ResolveSprite(id, definition, world, log);
		if (sprite is null) // Unity object — ==
		{
			return null;
		}

		var tile = ScriptableObject.CreateInstance<Tile>();
		tile.name = string.IsNullOrWhiteSpace(definition.TileName) ? id : definition.TileName.Trim();
		tile.sprite = sprite;
		tile.color = new Color(
			Mathf.Clamp01(definition.ColorR),
			Mathf.Clamp01(definition.ColorG),
			Mathf.Clamp01(definition.ColorB),
			Mathf.Clamp01(definition.ColorA));
		tile.colliderType = MapColliderType(definition.ColliderType);
		return tile;
	}

	private static Sprite? ResolveSprite(
		string id,
		ModTileDefinition definition,
		WorldGeneration world,
		ILogger log)
	{
		if (!string.IsNullOrWhiteSpace(definition.SpritePath))
		{
			var fromResources = Resources.Load<Sprite>(definition.SpritePath);
			if (fromResources != null) // Unity object — ==
			{
				return fromResources;
			}

			log.LogWarning(
				"[TileContent] {Id} sprite path {Path} did not resolve; falling back to the template tile.",
				id, definition.SpritePath);
		}

		if (definition.TemplateTileIndex is { } templateIndex
			&& templateIndex >= 0
			&& world.tiles is not null
			&& templateIndex < world.tiles.Length
			&& world.tiles[templateIndex] is Tile { sprite: not null } templateTile) // Unity object — ==
		{
			return templateTile.sprite;
		}

		log.LogWarning(
			"[TileContent] {Id} has no usable sprite source (SpritePath empty/unresolved and TemplateTileIndex invalid); tile was not built.",
			id);
		return null;
	}

	private static Tile.ColliderType MapColliderType(ModTileColliderType colliderType) =>
		colliderType switch
		{
			ModTileColliderType.None => Tile.ColliderType.None,
			ModTileColliderType.Sprite => Tile.ColliderType.Sprite,
			_ => Tile.ColliderType.Grid
		};
}
