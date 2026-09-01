using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The first concrete content binding provider: it turns
/// <see cref="ModItemDefinition"/> payloads from shared-content mods into vanilla
/// <c>ItemInfo</c> entries. It deliberately does not build prefabs or touch
/// spawning yet; it only registers the static definition into the vanilla item
/// table once <c>Item.GlobalItems</c> is ready. A future provider/patch layer
/// can extend the same registry to materialize runtime prefabs and custom item
/// behavior.
/// </summary>
public sealed class GameAdapterItemContentProvider(
	ILogger<GameAdapterItemContentProvider> log) : IContentBindingProvider, ICuoService
{
	private readonly ILogger<GameAdapterItemContentProvider> _log = log;
	private readonly Dictionary<string, ModItemDefinition> _definitions = [];

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
		}
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
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
