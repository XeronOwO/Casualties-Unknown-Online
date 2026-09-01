using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Binds <see cref="ModLiquidDefinition"/> payloads from shared-content mods
/// into the vanilla liquid registry. Static fields (color, value, health/
/// injection flags, qualities and locale display text) are mapped into
/// <c>LiquidType</c>; behavior callbacks are intentionally not part of this
/// DTO because mods must not pass game delegates through Abstractions.
/// </summary>
public sealed class GameAdapterLiquidContentProvider(
	ILogger<GameAdapterLiquidContentProvider> log) : IContentBindingProvider, ICuoService
{
	private readonly ILogger<GameAdapterLiquidContentProvider> _log = log;
	private readonly Dictionary<string, ModLiquidDefinition> _definitions = [];
	private readonly HashSet<string> _injectedIds = [];
	private Dictionary<string, LiquidType>? _lastRegistry;

	/// <inheritdoc />
	public string Kind => ModContentKind.Liquid;

	/// <inheritdoc />
	public bool TryBind(ModContentRegistration registration)
	{
		if (!string.Equals(registration.Definition.Kind, Kind, StringComparison.Ordinal))
		{
			return false;
		}

		var definition = ModLiquidDefinition.FromPayload(registration.Definition.Data);
		if (definition is null)
		{
			_log.LogWarning(
				"[LiquidContent] {ModId}/{Id} payload is not a valid ModLiquidDefinition — refused.",
				registration.ModId, registration.Definition.Id);
			return false;
		}

		var id = registration.Definition.Id;
		if (string.IsNullOrWhiteSpace(id))
		{
			_log.LogWarning("[LiquidContent] {ModId} registered a liquid with an empty id — refused.", registration.ModId);
			return false;
		}

		if (_definitions.ContainsKey(id))
		{
			_log.LogWarning(
				"[LiquidContent] {ModId}/{Id} is already registered by another liquid-content provider/definition — refused.",
				registration.ModId, id);
			return false;
		}

		_definitions.Add(id, definition);
		_log.LogInformation(
			"[LiquidContent] accepted {ModId}/{Id} (schema {SchemaVersion}); injection waits for the vanilla liquid registry.",
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
		if (Liquids.Registry is null)
		{
			_lastRegistry = null;
			return;
		}

		if (!ReferenceEquals(_lastRegistry, Liquids.Registry))
		{
			_lastRegistry = Liquids.Registry;
			_injectedIds.Clear(); // a rebuilt registry gets the same definitions re-injected
		}

		foreach (var pair in _definitions.ToArray())
		{
			if (_injectedIds.Contains(pair.Key))
			{
				continue;
			}

			if (Liquids.Registry.ContainsKey(pair.Key))
			{
				_injectedIds.Add(pair.Key);
				_log.LogDebug("[LiquidContent] {Id} is already present in the vanilla liquid registry; no duplicate injected.", pair.Key);
				continue;
			}

			var liquid = BuildLiquid(pair.Key, pair.Value);
			Liquids.Registry.Add(pair.Key, liquid);
			ApplyLocale(pair.Key, pair.Value);
			_injectedIds.Add(pair.Key);
			_log.LogInformation(
				"[LiquidContent] injected {Id} (value {Value:F1}, qualities {QualityCount}) into Liquids.Registry.",
				pair.Key, pair.Value.ValuePerLiter, pair.Value.Qualities.Count);
		}
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
	}

	private static LiquidType BuildLiquid(string id, ModLiquidDefinition definition)
	{
		return new LiquidType
		{
			localeName = id,
			color = new Color(
				Mathf.Clamp01(definition.ColorR),
				Mathf.Clamp01(definition.ColorG),
				Mathf.Clamp01(definition.ColorB),
				Mathf.Clamp01(definition.ColorA)),
			valuePerLiter = definition.ValuePerLiter,
			healthUsable = definition.HealthUsable,
			injectable = definition.Injectable,
			injectionSickness = definition.InjectionSickness,
			localeFromItem = definition.LocaleFromItem,
			qualities = [.. definition.Qualities
				.Select(q => new CraftingQuality(q.Id, q.Amount <= 0f ? 1f : q.Amount))]
		};
	}

	private static void ApplyLocale(string id, ModLiquidDefinition definition)
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
}
