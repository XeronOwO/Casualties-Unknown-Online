using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Binds <see cref="ModStructureDefinition"/> payloads from shared-content mods
/// into a small, validated structure registry. The provider does not perform
/// world-generation distribution; it compiles the authored grid into cells that
/// the Game Adapter can place through the existing <c>SetBlock</c> path. No wire
/// change is involved: static structure definitions are mod-local and the
/// placement writes ride the existing <c>BlockPlaced</c> relay.
/// </summary>
public sealed class GameAdapterStructureContentProvider(
	ILogger<GameAdapterStructureContentProvider> log) : IContentBindingProvider, ICuoService
{
	internal const int MaxWidth = 128;
	internal const int MaxHeight = 128;
	internal const int MaxCellCount = 4096;

	private readonly ILogger<GameAdapterStructureContentProvider> _log = log;
	private readonly Dictionary<string, ModStructureDefinition> _definitions = [];
	private readonly Dictionary<string, CompiledStructure> _compiled = [];

	/// <inheritdoc />
	public string Kind => ModContentKind.Structure;

	/// <inheritdoc />
	public bool TryBind(ModContentRegistration registration)
	{
		if (!string.Equals(registration.Definition.Kind, Kind, StringComparison.Ordinal))
		{
			return false;
		}

		var definition = ModStructureDefinition.FromPayload(registration.Definition.Data);
		if (definition is null)
		{
			_log.LogWarning(
				"[StructureContent] {ModId}/{Id} payload is not a valid ModStructureDefinition — refused.",
				registration.ModId, registration.Definition.Id);
			return false;
		}

		var id = registration.Definition.Id;
		if (string.IsNullOrWhiteSpace(id))
		{
			_log.LogWarning("[StructureContent] {ModId} registered a structure with an empty id — refused.", registration.ModId);
			return false;
		}

		if (_definitions.ContainsKey(id))
		{
			_log.LogWarning(
				"[StructureContent] {ModId}/{Id} is already registered by another structure-content provider/definition — refused.",
				registration.ModId, id);
			return false;
		}

		if (!TryCompile(id, definition, out var compiled))
		{
			return false;
		}

		_definitions.Add(id, definition);
		_compiled.Add(id, compiled);
		_log.LogInformation(
			"[StructureContent] accepted {ModId}/{Id} ({Width}x{Height}, {CellCount} placed cells; schema {SchemaVersion}); runtime placement waits for a world.",
			registration.ModId, id, compiled.Width, compiled.Height, compiled.Cells.Count, registration.Definition.SchemaVersion);
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
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
	}

	/// <summary>
	/// Resolve the compiled placement cells for a bound structure. Returns false
	/// when the id is unknown or the structure failed validation/binding.
	/// </summary>
	internal bool TryGetCompiled(string id, out CompiledStructure structure) =>
		_compiled.TryGetValue(id, out structure);

	/// <summary>Resolve the original typed definition (includes future worldgen spawn counts).</summary>
	internal bool TryGetDefinition(string id, out ModStructureDefinition definition) =>
		_definitions.TryGetValue(id, out definition!);

	private bool TryCompile(string id, ModStructureDefinition definition, out CompiledStructure compiled)
	{
		compiled = default;

		if (definition.Width <= 0 || definition.Height <= 0)
		{
			_log.LogWarning("[StructureContent] {Id} must have positive Width and Height — refused.", id);
			return false;
		}

		if (definition.Width > MaxWidth || definition.Height > MaxHeight)
		{
			_log.LogWarning(
				"[StructureContent] {Id} is {Width}x{Height}; the safe seam limit is {MaxWidth}x{MaxHeight} — refused.",
				id, definition.Width, definition.Height, MaxWidth, MaxHeight);
			return false;
		}

		if ((long)definition.Width * definition.Height > MaxCellCount)
		{
			_log.LogWarning(
				"[StructureContent] {Id} has {CellCount} cells; the safe seam limit is {MaxCellCount} — refused.",
				id, (long)definition.Width * definition.Height, MaxCellCount);
			return false;
		}

		var rows = definition.Rows ?? [];
		if (rows.Count != definition.Height)
		{
			_log.LogWarning(
				"[StructureContent] {Id} declares {Height} rows but supplied {Actual} — refused.",
				id, definition.Height, rows.Count);
			return false;
		}

		var vanillaBlocks = definition.VanillaBlocks ?? [];
		var tileIds = definition.TileIds ?? [];
		if (!TryValidateMarkerMaps(id, vanillaBlocks, tileIds, definition.SpawnCounts ?? []))
		{
			return false;
		}

		var cells = new List<StructureCell>();
		for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
		{
			var row = rows[rowIndex];
			if (row is null || row.Length != definition.Width)
			{
				_log.LogWarning(
					"[StructureContent] {Id} row {RowIndex} has length {Actual}; expected {Width} — refused.",
					id, rowIndex, row?.Length ?? 0, definition.Width);
				return false;
			}

			var y = definition.Height - 1 - rowIndex;
			for (var x = 0; x < row.Length; x++)
			{
				var marker = row[x];
				if (marker is '.' or ' ')
				{
					continue;
				}

				var markerText = marker.ToString();
				if (tileIds.TryGetValue(markerText, out var tileId) && !string.IsNullOrWhiteSpace(tileId))
				{
					cells.Add(new StructureCell(x, y, tileId, 0));
				}
				else if (vanillaBlocks.TryGetValue(markerText, out var blockIndex))
				{
					cells.Add(new StructureCell(x, y, null, blockIndex));
				}
				else
				{
					_log.LogWarning(
						"[StructureContent] {Id} row {RowIndex} uses unmapped marker '{Marker}' at x={X} — refused.",
						id, rowIndex, marker, x);
					return false;
				}
			}
		}

		if (cells.Count == 0)
		{
			_log.LogWarning("[StructureContent] {Id} has no non-air cells — refused.", id);
			return false;
		}

		compiled = new CompiledStructure(definition.Width, definition.Height, cells);
		return true;
	}

	private bool TryValidateMarkerMaps(
		string id,
		Dictionary<string, int> vanillaBlocks,
		Dictionary<string, string> tileIds,
		List<int> spawnCounts)
	{
		foreach (var pair in vanillaBlocks)
		{
			if (!IsValidMarker(pair.Key))
			{
				_log.LogWarning("[StructureContent] {Id} uses invalid vanilla marker '{Marker}' — refused.", id, pair.Key);
				return false;
			}

			if (pair.Value <= 0 || pair.Value > ushort.MaxValue)
			{
				_log.LogWarning(
					"[StructureContent] {Id} vanilla marker '{Marker}' maps to block {Block}; expected 1..{Max} — refused.",
					id, pair.Key, pair.Value, ushort.MaxValue);
				return false;
			}
		}

		foreach (var pair in tileIds)
		{
			if (!IsValidMarker(pair.Key))
			{
				_log.LogWarning("[StructureContent] {Id} uses invalid tile marker '{Marker}' — refused.", id, pair.Key);
				return false;
			}

			if (string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Length > 128)
			{
				_log.LogWarning("[StructureContent] {Id} tile marker '{Marker}' has an invalid tile id — refused.", id, pair.Key);
				return false;
			}
		}

		foreach (var pair in vanillaBlocks)
		{
			if (tileIds.ContainsKey(pair.Key))
			{
				_log.LogWarning(
					"[StructureContent] {Id} marker '{Marker}' is declared in both vanilla and tile maps — refused.",
					id, pair.Key);
				return false;
			}
		}

		foreach (var count in spawnCounts)
		{
			if (count < 0)
			{
				_log.LogWarning("[StructureContent] {Id} has a negative spawn count — refused.", id);
				return false;
			}
		}

		return true;
	}

	private static bool IsValidMarker(string marker) =>
		marker is not null && marker.Length == 1 && marker[0] is not '.' and not ' ';

	/// <summary>Compiled placement cells for a structure. Y is the bottom-based block offset.</summary>
	internal readonly record struct StructureCell(int X, int Y, string? TileId, int VanillaBlockIndex)
	{
		public bool IsCustomTile => TileId is not null;
	}

	/// <summary>Compiled structure grid shape plus its non-air cells.</summary>
	internal readonly record struct CompiledStructure(int Width, int Height, List<StructureCell> Cells);
}
