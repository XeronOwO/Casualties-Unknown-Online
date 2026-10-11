using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameAdapter.ModEffects;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Binds <see cref="IModLiquidDefinition"/> definitions from shared-content mods
/// into the vanilla liquid registry. Static fields (color, value, health/
/// injection flags, qualities and locale display text) are mapped into
/// <c>LiquidType</c>; the two behaviour callbacks are NOT part of this DTO because
/// mods must not pass game delegates through Abstractions — they are registered in
/// code instead (see below), keyed by the same content id.
///
/// <para>
/// The delegates the GAME calls are installed on every liquid built here, because
/// it calls them with no null check at all: <c>WaterContainerItem.Drink</c> runs
/// <c>onDrink</c> for whatever liquid a container holds (no flag gates it), and
/// <c>WaterContainerItem.ApplyToLimb</c> / <c>Inject</c> run <c>onHealthUse</c>
/// behind the liquid's own <c>healthUsable</c> / <c>injectable</c> flag. Both
/// installed delegates are <see cref="LiquidEffectInvoker"/>: it resolves the
/// effect the liquid's OWNER registered through
/// <see cref="IModLiquidEffects"/> at call
/// time, and where none is registered the action still completes and says so —
/// the inert outcome <c>mod-declared-behaviour-with-no-function.md</c> settled,
/// which is now the "no effect registered" path rather than the only path.
/// </para>
///
/// <para>
/// It is also a <see cref="ICraftingQualitySource"/>: the crafting-quality labels
/// its accepted definitions declare are written into <c>LiquidType.qualities</c>
/// and reported to the recipe provider, on the same rule the item side uses.
/// </para>
/// </summary>
public sealed class GameAdapterLiquidContentProvider(
	ILogger<GameAdapterLiquidContentProvider> log,
	ModLiquidEffectStore effectStore,
	ISessionControl session) : IContentBindingProvider, ICuoService, ICraftingQualitySource
{
	private readonly ILogger<GameAdapterLiquidContentProvider> _log = log;
	private readonly ModLiquidEffectStore _effectStore = effectStore;
	private readonly LiquidEffectInvoker _effectInvoker = new(effectStore, session, log);
	private readonly Dictionary<string, IModLiquidDefinition> _definitions = [];
	private readonly HashSet<string> _injectedIds = [];
	private readonly CraftingQualityDeclarations _qualities = new();
	private Dictionary<string, LiquidType>? _lastRegistry;

	/// <inheritdoc />
	public string Kind => ModContentKind.Liquid;

	/// <inheritdoc />
	public bool TryBind(ModContentRegistration registration)
	{
		if (registration.Definition is not IModLiquidDefinition definition)
		{
			_log.LogWarning(
				"[LiquidContent] {ModId}/{Id} claims kind {Kind} but is a {Type}, not an {Expected} — refused.",
				registration.ModId, registration.Definition.Id, registration.Definition.Kind,
				registration.Definition.GetType().Name, nameof(IModLiquidDefinition));
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

		var qualities = ModDeclarationCollections.OrEmpty(definition.Qualities);
		if (!CraftingQualityDeclarations.IsValid(qualities, out var rejectedQuality))
		{
			_log.LogWarning(
				"[LiquidContent] {ModId}/{Id} declares crafting quality '{Quality}' that is not a vanilla label or a canonical namespace:label id — refused.",
				registration.ModId, id, rejectedQuality);
			return false;
		}

		_definitions.Add(id, definition);
		_qualities.Accept(qualities);
		if (qualities.Count > 0)
		{
			_log.LogInformation(
				"[LiquidContent] {ModId}/{Id} provides crafting qualities {Qualities}.",
				registration.ModId, id, string.Join(", ", qualities.Select(quality => quality.Id)));
		}

		_log.LogInformation(
			"[LiquidContent] accepted {ModId}/{Id} (schema {SchemaVersion}); injection waits for the vanilla liquid registry.",
			registration.ModId, id, registration.Definition.SchemaVersion);

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

			var qualities = ModDeclarationCollections.OrEmpty(pair.Value.Qualities);
			if (Liquids.Registry.ContainsKey(pair.Key))
			{
				_injectedIds.Add(pair.Key);
				_log.LogDebug("[LiquidContent] {Id} is already present in the vanilla liquid registry; no duplicate injected.", pair.Key);
				if (qualities.Count > 0)
				{
					// Nothing materialized this definition, so a label it declared
					// is provided by nothing and a recipe requiring it can never be
					// crafted (the ticket records that as a limit).
					_log.LogWarning(
						"[LiquidContent] {Id} was not injected, so the crafting quality it declares ({Quality}) is not provided by it.",
						pair.Key, string.Join(", ", qualities.Select(quality => quality.Id)));
				}

				continue;
			}

			var liquid = BuildLiquid(pair.Key, pair.Value);
			Liquids.Registry.Add(pair.Key, liquid);
			ApplyLocale(pair.Key, pair.Value);
			_injectedIds.Add(pair.Key);
			_log.LogInformation(
				"[LiquidContent] injected {Id} (value {Value:F1}, qualities {QualityCount}) into Liquids.Registry.",
				pair.Key, pair.Value.ValuePerLiter, qualities.Count);

			// The two native limb paths run their delegate behind one of these flags, so a
			// definition that sets one declares an effect the action will try to run.
			// Whether anything authored THAT half is knowable HERE and only here — the mods
			// have all registered by the frame the registry appears — so this is where the
			// author is told, next to the definition it is about. The question is the
			// limb-use half and not "is this liquid registered": a mod that registered only
			// the drink function would otherwise pass it, and the player would apply or
			// inject a liquid that does nothing in silence.
			if ((pair.Value.HealthUsable || pair.Value.Injectable) && !_effectStore.HasLimbUse(pair.Key))
			{
				_log.LogWarning(
					"[LiquidContent] {Id} declares HealthUsable/Injectable but no mod registered a limb-use effect for it — applying or injecting this liquid consumes it with no effect.",
					pair.Key);
			}
		}
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
	}

	private LiquidType BuildLiquid(string id, IModLiquidDefinition definition)
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
			qualities = CraftingQualityDeclarations.ToGameQualities(ModDeclarationCollections.OrEmpty(definition.Qualities)),
			onDrink = (ml, body) => _effectInvoker.Drink(id, ml, body),
			onHealthUse = (ml, limb) => _effectInvoker.ApplyToLimb(id, ml, limb)
		};
	}

	private static void ApplyLocale(string id, IModLiquidDefinition definition)
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
