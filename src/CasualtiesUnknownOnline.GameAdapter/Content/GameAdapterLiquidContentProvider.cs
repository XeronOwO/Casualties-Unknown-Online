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
///
/// <para>
/// The delegates the GAME calls are still installed, because it calls them with
/// no null check at all: <c>WaterContainerItem.Drink</c> runs <c>onDrink</c> for
/// whatever liquid a container holds (no flag gates it), and
/// <c>WaterContainerItem.ApplyToLimb</c> / <c>Inject</c> run <c>onHealthUse</c>
/// behind the liquid's own <c>healthUsable</c> / <c>injectable</c> flag. Every
/// liquid built here therefore carries both, and the effect the API cannot author
/// yet is a no-op that names the content instead of a null the game would throw
/// on.
/// </para>
///
/// <para>
/// It is also a <see cref="ICraftingQualitySource"/>: the crafting-quality labels
/// its accepted definitions declare are written into <c>LiquidType.qualities</c>
/// and reported to the recipe provider, on the same rule the item side uses.
/// </para>
/// </summary>
public sealed class GameAdapterLiquidContentProvider(
	ILogger<GameAdapterLiquidContentProvider> log) : IContentBindingProvider, ICuoService, ICraftingQualitySource
{
	private readonly ILogger<GameAdapterLiquidContentProvider> _log = log;
	private readonly Dictionary<string, ModLiquidDefinition> _definitions = [];
	private readonly HashSet<string> _injectedIds = [];
	private readonly CraftingQualityDeclarations _qualities = new();
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

		if (!CraftingQualityDeclarations.IsValid(definition.Qualities, out var rejectedQuality))
		{
			_log.LogWarning(
				"[LiquidContent] {ModId}/{Id} declares crafting quality '{Quality}' that is not a vanilla label or a canonical namespace:label id — refused.",
				registration.ModId, id, rejectedQuality);
			return false;
		}

		_definitions.Add(id, definition);
		_qualities.Accept(definition.Qualities);
		if (definition.Qualities.Count > 0)
		{
			_log.LogInformation(
				"[LiquidContent] {ModId}/{Id} provides crafting qualities {Qualities}.",
				registration.ModId, id, string.Join(", ", definition.Qualities.Select(quality => quality.Id)));
		}

		_log.LogInformation(
			"[LiquidContent] accepted {ModId}/{Id} (schema {SchemaVersion}); injection waits for the vanilla liquid registry.",
			registration.ModId, id, registration.Definition.SchemaVersion);

		// The two flags below are the only gates the native limb paths have before
		// they run the effect delegate, so a definition that sets one declares an
		// effect this API cannot carry yet. Said at load time, where the author reads.
		if (definition.HealthUsable || definition.Injectable)
		{
			_log.LogWarning(
				"[LiquidContent] {ModId}/{Id} declares HealthUsable/Injectable — the mod API cannot author an effect function, so applying or injecting this liquid consumes it with no effect.",
				registration.ModId, id);
		}

		return true;
	}

	/// <inheritdoc />
	public bool ProvidesQuality(string qualityId, float requiredAmount) =>
		_qualities.Provides(qualityId, requiredAmount);

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
				if (pair.Value.Qualities.Count > 0)
				{
					// Nothing materialized this definition, so a label it declared
					// is provided by nothing and a recipe requiring it can never be
					// crafted (the ticket records that as a limit).
					_log.LogWarning(
						"[LiquidContent] {Id} was not injected, so the crafting quality it declares ({Quality}) is not provided by it.",
						pair.Key, string.Join(", ", pair.Value.Qualities.Select(quality => quality.Id)));
				}

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

	private LiquidType BuildLiquid(string id, ModLiquidDefinition definition)
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
			qualities = CraftingQualityDeclarations.ToGameQualities(definition.Qualities),
			onDrink = (ml, body) => _log.LogWarning(
				"[LiquidContent] {Id} was drunk ({Ml:F1} ml) with no effect — the mod API cannot author a drink effect.",
				id, ml),
			onHealthUse = (ml, limb) => _log.LogWarning(
				"[LiquidContent] {Id} was used on a limb ({Ml:F1} ml) with no effect — the mod API cannot author an effect function.",
				id, ml)
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
