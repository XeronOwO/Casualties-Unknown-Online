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
			if (Item.GlobalItems.ContainsKey(pair.Key))
			{
				continue;
			}

			Item.GlobalItems.Add(pair.Key, BuildItemInfo(pair.Key, pair.Value));
			_log.LogInformation("[ItemContent] injected {Id} into Item.GlobalItems.", pair.Key);
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
