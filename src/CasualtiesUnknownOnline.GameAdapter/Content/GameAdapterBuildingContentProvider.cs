using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The building content binding provider: it turns
/// <see cref="ModBuildingDefinition"/> payloads from shared-content mods into
/// runtime <c>BuildingEntity</c> prefab templates so CUO's existing
/// <c>EntitySpawned</c> channel can materialize the custom building without
/// exposing game types to mods. Template construction uses
/// <see cref="CustomBuildingTemplateFactory"/> and the resolved templates are
/// served through <see cref="TryResolveTemplate"/>.
/// </summary>
public sealed class GameAdapterBuildingContentProvider(
	ILogger<GameAdapterBuildingContentProvider> log) : IContentBindingProvider, ICuoService
{
	private readonly ILogger<GameAdapterBuildingContentProvider> _log = log;
	private readonly Dictionary<string, ModBuildingDefinition> _definitions = [];
	private readonly Dictionary<string, GameObject> _templates = [];
	private readonly HashSet<string> _templateFailures = [];
	private readonly HashSet<string> _vanillaIds = [];

	/// <inheritdoc />
	public string Kind => ModContentKind.Building;

	/// <inheritdoc />
	public bool TryBind(ModContentRegistration registration)
	{
		if (!string.Equals(registration.Definition.Kind, Kind, StringComparison.Ordinal))
		{
			return false;
		}

		var definition = ModBuildingDefinition.FromPayload(registration.Definition.Data);
		if (definition is null)
		{
			_log.LogWarning(
				"[BuildingContent] {ModId}/{Id} payload is not a valid ModBuildingDefinition — refused.",
				registration.ModId, registration.Definition.Id);
			return false;
		}

		var id = registration.Definition.Id;
		if (string.IsNullOrWhiteSpace(id))
		{
			_log.LogWarning("[BuildingContent] {ModId} registered a building with an empty id — refused.", registration.ModId);
			return false;
		}

		if (string.IsNullOrWhiteSpace(definition.TemplateId))
		{
			_log.LogWarning(
				"[BuildingContent] {ModId}/{Id} has no TemplateId — refused (a vanilla base prefab is required for a safe runtime template).",
				registration.ModId, id);
			return false;
		}

		if (_definitions.ContainsKey(id))
		{
			_log.LogWarning(
				"[BuildingContent] {ModId}/{Id} is already registered by another building-content provider/definition — refused.",
				registration.ModId, id);
			return false;
		}

		_definitions.Add(id, definition);
		_log.LogInformation(
			"[BuildingContent] accepted {ModId}/{Id} (schema {SchemaVersion}); template construction waits for the first update.",
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
		// Vanilla building prefabs live in the game world scene; building a
		// template before that scene exists would permanently record a fake
		// "missing prefab" failure. Wait for the world object before resolving.
		if (WorldGeneration.world is null)
		{
			return;
		}

		foreach (var pair in _definitions.ToArray())
		{
			EnsureTemplate(pair.Key, pair.Value);
		}
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
	}

	/// <summary>
	/// Resolve a custom runtime building template. Returns false when the id has
	/// no template (either the DTO did not request one or the build failed).
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
			_log.LogDebug("[BuildingContent] removed a destroyed runtime template for {Id}.", id);
		}

		template = null;
		return false;
	}

	private void EnsureTemplate(string id, ModBuildingDefinition definition)
	{
		if (_templates.ContainsKey(id) || _templateFailures.Contains(id) || _vanillaIds.Contains(id))
		{
			return;
		}

		if (Resources.Load<GameObject>(id) != null) // Unity object — ==; a vanilla prefab already exists, never shadow it
		{
			_vanillaIds.Add(id);
			_log.LogDebug("[BuildingContent] {Id} resolves as a vanilla prefab; no runtime template is built.", id);
			return;
		}

		var template = CustomBuildingTemplateFactory.Create(id, definition, _log);
		if (template is null)
		{
			_templateFailures.Add(id);
			_log.LogWarning("[BuildingContent] no runtime template was built for {Id}.", id);
			return;
		}

		_templates.Add(id, template);
		ApplyLocale(id, definition);
		_log.LogInformation(
			"[BuildingContent] built runtime template for {Id} (base {TemplateId}, components {ComponentCount}).",
			id, definition.TemplateId, definition.SpawnComponents.Count);
	}

	private static void ApplyLocale(string id, ModBuildingDefinition definition)
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
			Locale.currentLang.buildings[id] = definition.DisplayName;
		}

		if (!string.IsNullOrWhiteSpace(definition.Description))
		{
			Locale.currentLang.buildings[id + "dsc"] = definition.Description;
		}
	}
}
