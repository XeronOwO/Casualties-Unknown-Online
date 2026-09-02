using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Binds <see cref="ModLiquidTileDefinition"/> payloads from shared-content mods
/// into the vanilla world-fluid grid. Each definition receives a deterministic
/// custom world-fluid byte (starting at 7, allocated in stable id order), is
/// mapped through <c>FluidManager.WorldFluidToLiquidID</c>, and serves the
/// GameAdapter projection surfaces (water info, display colour/name, body
/// touch, drink and custom rendering). Static content is local-only: no wire,
/// no JObject snapshot, and no game/Unity type crosses Abstractions.
/// </summary>
public sealed class GameAdapterLiquidTileContentProvider(
	ILogger<GameAdapterLiquidTileContentProvider> log) : IContentBindingProvider, ICuoService
{
	private const byte FirstCustomWorldByte = 7;

	private readonly ILogger<GameAdapterLiquidTileContentProvider> _log = log;
	private readonly Dictionary<string, ModLiquidTileDefinition> _definitions = [];
	private readonly Dictionary<string, byte> _worldBytesById = [];
	private readonly Dictionary<byte, string> _idsByWorldByte = [];
	private readonly HashSet<string> _failedIds = [];
	private bool _summaryLogged;

	/// <inheritdoc />
	public string Kind => ModContentKind.LiquidTile;

	/// <inheritdoc />
	public bool TryBind(ModContentRegistration registration)
	{
		if (!string.Equals(registration.Definition.Kind, Kind, StringComparison.Ordinal))
		{
			return false;
		}

		var definition = ModLiquidTileDefinition.FromPayload(registration.Definition.Data);
		if (definition is null)
		{
			_log.LogWarning(
				"[LiquidTileContent] {ModId}/{Id} payload is not a valid ModLiquidTileDefinition — refused.",
				registration.ModId, registration.Definition.Id);
			return false;
		}

		var id = registration.Definition.Id;
		if (string.IsNullOrWhiteSpace(id))
		{
			_log.LogWarning("[LiquidTileContent] {ModId} registered a liquid tile with an empty id — refused.", registration.ModId);
			return false;
		}

		if (_definitions.ContainsKey(id))
		{
			_log.LogWarning(
				"[LiquidTileContent] {ModId}/{Id} is already registered by another liquid-tile content provider/definition — refused.",
				registration.ModId, id);
			return false;
		}

		NormalizeDefaults(id, definition);
		if (!TryValidateDefinition(id, definition))
		{
			return false;
		}

		_definitions.Add(id, definition);
		_log.LogInformation(
			"[LiquidTileContent] accepted {ModId}/{Id} (schema {SchemaVersion}); world-byte allocation waits for the fluid manager.",
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
		var fluid = FluidManager.main;
		if (fluid == null) // Unity object — ==
		{
			return;
		}

		EnsureAllMappings();
		ApplyLocales();
		LogSummary();
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
	}

	/// <summary>True when at least one liquid-tile definition is bound.</summary>
	internal bool HasAny() => _definitions.Count > 0;

	/// <summary>Snapshot every accepted definition in stable id order for deterministic world generation.</summary>
	internal IReadOnlyList<KeyValuePair<string, ModLiquidTileDefinition>> GetDefinitionsForWorldGen() =>
		[.. _definitions.OrderBy(pair => pair.Key, StringComparer.Ordinal)];

	/// <summary>Resolve the stable content id to its allocated custom world-fluid byte.</summary>
	internal bool TryGetWorldByte(string id, out byte worldByte)
	{
		worldByte = 0;
		if (string.IsNullOrWhiteSpace(id) || !_worldBytesById.TryGetValue(id, out worldByte))
		{
			return false;
		}

		return true;
	}

	/// <summary>Resolve a custom world-fluid byte to its content id.</summary>
	internal bool TryGetTileId(byte worldByte, out string id)
	{
		id = "";
		return _idsByWorldByte.TryGetValue(worldByte, out id!);
	}

	/// <summary>Resolve the original typed definition by stable content id.</summary>
	internal bool TryGetDefinition(string id, out ModLiquidTileDefinition definition)
	{
		definition = null!;
		return _definitions.TryGetValue(id, out definition!);
	}

	/// <summary>Resolve a definition by its allocated custom world-fluid byte.</summary>
	internal bool TryGetDefinitionByWorldByte(byte worldByte, out ModLiquidTileDefinition definition)
	{
		definition = null!;
		return _idsByWorldByte.TryGetValue(worldByte, out var id)
			&& _definitions.TryGetValue(id, out definition!);
	}

	/// <summary>Ensure the world byte + fluid mapping exist for one definition. Returns false when no byte can be allocated.</summary>
	internal bool TryPrepareForWorldGen(string id, out byte worldByte)
	{
		worldByte = 0;
		if (!_definitions.TryGetValue(id, out var definition))
		{
			return false;
		}

		if (_failedIds.Contains(id))
		{
			return false;
		}

		if (!_worldBytesById.TryGetValue(id, out worldByte))
		{
			if (!TryAllocateWorldByte(id, definition, out worldByte))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>WaterInfo values for a custom world-fluid byte.</summary>
	internal bool TryGetWaterInfo(byte worldByte, out float buoyancy, out float drag, out int type)
	{
		buoyancy = 0f;
		drag = 0f;
		type = 0;
		if (!TryGetDefinitionByWorldByte(worldByte, out var definition))
		{
			return false;
		}

		buoyancy = definition.Buoyancy;
		drag = definition.Drag;
		type = worldByte;
		return true;
	}

	/// <summary>Display colour for a custom world-fluid byte (base liquid colour multiplied by the authored tint).</summary>
	internal bool TryGetDisplayColor(byte worldByte, out Color color)
	{
		color = Color.clear;
		if (!TryGetDefinitionByWorldByte(worldByte, out var definition))
		{
			return false;
		}

		var baseColor = Color.white;
		if (!string.IsNullOrWhiteSpace(definition.LiquidId)
			&& Liquids.Registry != null
			&& Liquids.Registry.TryGetValue(definition.LiquidId, out var liquid)
			&& liquid is not null)
		{
			baseColor = liquid.color;
		}

		color = new Color(
			Mathf.Clamp01(baseColor.r * Mathf.Clamp01(definition.TintR)),
			Mathf.Clamp01(baseColor.g * Mathf.Clamp01(definition.TintG)),
			Mathf.Clamp01(baseColor.b * Mathf.Clamp01(definition.TintB)),
			Mathf.Clamp01(baseColor.a * Mathf.Clamp01(definition.TintA)));
		return true;
	}

	/// <summary>Display name/description for a custom world-fluid byte.</summary>
	internal bool TryGetDisplayName(byte worldByte, out string name, out string description)
	{
		name = "";
		description = "";
		if (!TryGetDefinitionByWorldByte(worldByte, out var definition))
		{
			return false;
		}

		name = !string.IsNullOrWhiteSpace(definition.DisplayName)
			? definition.DisplayName
			: ResolveLiquidName(definition.LiquidId);
		description = !string.IsNullOrWhiteSpace(definition.Description)
			? definition.Description
			: ResolveLiquidDescription(definition.LiquidId);
		return true;
	}

	/// <summary>Resolve the vanilla particle-system index used to render a custom world-fluid byte.</summary>
	internal bool TryGetVisualIndex(byte worldByte, out int particleIndex)
	{
		particleIndex = 0;
		if (!TryGetDefinitionByWorldByte(worldByte, out var definition))
		{
			return false;
		}

		particleIndex = Mathf.Clamp(definition.VisualLiquidByte - 1, 0, 5);
		return true;
	}

	/// <summary>Resolve the authored drink liquid type for a custom world-fluid byte.</summary>
	internal bool TryGetDrinkLiquid(byte worldByte, out LiquidType liquidType)
	{
		liquidType = null!;
		if (!TryGetDefinitionByWorldByte(worldByte, out var definition))
		{
			return false;
		}

		if (string.IsNullOrWhiteSpace(definition.LiquidId) || Liquids.Registry == null)
		{
			return false;
		}

		return Liquids.Registry.TryGetValue(definition.LiquidId, out liquidType!);
	}

	private bool TryAllocateWorldByte(string id, ModLiquidTileDefinition definition, out byte worldByte)
	{
		for (var candidate = FirstCustomWorldByte; candidate < byte.MaxValue; candidate++)
		{
			if (_idsByWorldByte.ContainsKey(candidate))
			{
				continue;
			}

			_worldBytesById.Add(id, candidate);
			_idsByWorldByte.Add(candidate, id);
			FluidManager.WorldFluidToLiquidID[candidate] = definition.FillLiquidId;
			worldByte = candidate;
			_log.LogDebug("[LiquidTileContent] allocated world byte {WorldByte} for {Id}.", candidate, id);
			return true;
		}

		_failedIds.Add(id);
		worldByte = 0;
		_log.LogError("[LiquidTileContent] {Id} could not allocate a custom world-fluid byte.", id);
		return false;
	}

	private void EnsureAllMappings()
	{
		foreach (var pair in _definitions.OrderBy(p => p.Key, StringComparer.Ordinal))
		{
			if (_worldBytesById.ContainsKey(pair.Key))
			{
				continue;
			}

			if (!TryAllocateWorldByte(pair.Key, pair.Value, out _))
			{
				_log.LogWarning("[LiquidTileContent] {Id} could not be mapped — no world byte available.", pair.Key);
			}
		}
	}

	private void ApplyLocales()
	{
		if (Locale.currentLang is null)
		{
			Locale.LoadLanguage();
		}

		if (Locale.currentLang is null)
		{
			return;
		}

		foreach (var pair in _definitions)
		{
			if (!string.IsNullOrWhiteSpace(pair.Value.DisplayName))
			{
				Locale.currentLang.other[pair.Key] = pair.Value.DisplayName;
			}

			if (!string.IsNullOrWhiteSpace(pair.Value.Description))
			{
				Locale.currentLang.other[pair.Key + "dsc"] = pair.Value.Description;
			}
		}
	}

	private void LogSummary()
	{
		if (_summaryLogged || _definitions.Count == 0)
		{
			return;
		}

		_summaryLogged = true;
		_log.LogInformation("[LiquidTileContent] added {Count} liquid tile definition(s).", _definitions.Count);
	}

	private static void NormalizeDefaults(string id, ModLiquidTileDefinition definition)
	{
		if (string.IsNullOrWhiteSpace(definition.LiquidId))
		{
			definition.LiquidId = id;
		}

		if (string.IsNullOrWhiteSpace(definition.FillLiquidId))
		{
			definition.FillLiquidId = definition.LiquidId;
		}

		if (definition.MaxFloodFill <= 0)
		{
			definition.MaxFloodFill = 1;
		}

		if (definition.VisualLiquidByte < 1 || definition.VisualLiquidByte > 6)
		{
			definition.VisualLiquidByte = 1;
		}
	}

	private bool TryValidateDefinition(string id, ModLiquidTileDefinition definition)
	{
		if (float.IsNaN(definition.SpawnAmount) || float.IsInfinity(definition.SpawnAmount) || definition.SpawnAmount < 0f)
		{
			_log.LogWarning("[LiquidTileContent] {Id} has invalid SpawnAmount {SpawnAmount} — refused.", id, definition.SpawnAmount);
			return false;
		}

		if (float.IsNaN(definition.Buoyancy) || float.IsInfinity(definition.Buoyancy) || definition.Buoyancy < 0f)
		{
			_log.LogWarning("[LiquidTileContent] {Id} has invalid Buoyancy {Buoyancy} — refused.", id, definition.Buoyancy);
			return false;
		}

		if (float.IsNaN(definition.Drag) || float.IsInfinity(definition.Drag) || definition.Drag < 0f || definition.Drag > 1f)
		{
			_log.LogWarning("[LiquidTileContent] {Id} has invalid Drag {Drag} — refused.", id, definition.Drag);
			return false;
		}

		if (IsInvalidFloat(definition.WetnessPerSecond)
			|| IsInvalidFloat(definition.TemperaturePerSecond)
			|| IsInvalidFloat(definition.SicknessPerSecond)
			|| IsInvalidFloat(definition.DirtynessPerSecond)
			|| IsInvalidFloat(definition.DisinfectPerSecond)
			|| IsInvalidFloat(definition.SlipPerSecond)
			|| IsInvalidFloat(definition.RagdollBarDrainPerSecond)
			|| IsInvalidFloat(definition.TintR)
			|| IsInvalidFloat(definition.TintG)
			|| IsInvalidFloat(definition.TintB)
			|| IsInvalidFloat(definition.TintA))
		{
			_log.LogWarning("[LiquidTileContent] {Id} has a non-finite float field — refused.", id);
			return false;
		}

		if (definition.MaxFloodFill <= 0)
		{
			_log.LogWarning("[LiquidTileContent] {Id} has invalid MaxFloodFill {MaxFloodFill} — refused.", id, definition.MaxFloodFill);
			return false;
		}

		if (!definition.ConsumeOnDrink)
		{
			_log.LogWarning(
				"[LiquidTileContent] {Id} requested ConsumeOnDrink=false; CUO's FluidInteraction drink path always consumes the cell, so the value is normalized to true.",
				id);
			definition.ConsumeOnDrink = true;
		}

		return true;
	}

	private static bool IsInvalidFloat(float value) => float.IsNaN(value) || float.IsInfinity(value);

	private static string ResolveLiquidName(string liquidId)
	{
		if (string.IsNullOrWhiteSpace(liquidId))
		{
			return "";
		}

		return Locale.currentLang is null ? liquidId : Locale.GetOther(liquidId);
	}

	private static string ResolveLiquidDescription(string liquidId)
	{
		if (string.IsNullOrWhiteSpace(liquidId))
		{
			return "";
		}

		return Locale.currentLang is null ? "" : Locale.GetOther(liquidId + "dsc");
	}
}
