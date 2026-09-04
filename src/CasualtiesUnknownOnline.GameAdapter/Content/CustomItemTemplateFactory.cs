using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Builds the in-memory GameObject template for a custom item definition.
/// The template clones a vanilla prefab (the mod's <c>TemplateId</c>), renames
/// it to the custom item id, and attaches the requested mod component types.
/// The template stays inactive and is cached by
/// <see cref="GameAdapterItemContentProvider"/>; every materialization path
/// instantiates this template instead of a resource the game never shipped.
/// </summary>
internal static class CustomItemTemplateFactory
{
	internal static GameObject? Create(string id, ModItemDefinition definition, ILogger log)
	{
		var templateId = definition.TemplateId;
		if (string.IsNullOrWhiteSpace(templateId))
		{
			return null;
		}

		var basePrefab = Resources.Load<GameObject>(templateId);
		if (basePrefab == null) // Unity object — ==
		{
			log.LogWarning(
				"[ItemContent] cannot build runtime template {Id}: base prefab {Template} was not found.",
				id, templateId);
			return null;
		}

		var template = Object.Instantiate(basePrefab) as GameObject;
		if (template == null) // Unity object — ==
		{
			log.LogWarning(
				"[ItemContent] cannot build runtime template {Id}: base prefab {Template} could not be instantiated.",
				id, templateId);
			return null;
		}

		template.name = id;
		template.SetActive(false);
		template.AddComponent<CustomItemTemplateMarker>();
		Object.DontDestroyOnLoad(template);

		var item = template.GetComponent<Item>();
		if (item == null) // Unity object — ==
		{
			Object.Destroy(template);
			log.LogWarning(
				"[ItemContent] cannot build runtime template {Id}: base prefab {Template} has no Item component.",
				id, templateId);
			return null;
		}

		item.id = id;

		CustomItemBehaviorApplier.Apply(template, definition, log);
		CustomComponentAttach.Attach(template, definition.SpawnComponents, log, "ItemContent");
		return template;
	}
}
