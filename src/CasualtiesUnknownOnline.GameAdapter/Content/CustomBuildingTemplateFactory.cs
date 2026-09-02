using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Builds the in-memory GameObject template for a custom building definition.
/// The template clones a vanilla prefab (the mod's <c>TemplateId</c>), renames
/// it to the custom building id, applies optional <c>BuildingEntity</c> field
/// overrides, and attaches the requested mod component types. The template
/// stays inactive and is cached by
/// <see cref="GameAdapterBuildingContentProvider"/>; every materialization path
/// instantiates this template instead of a resource the game never shipped.
/// </summary>
internal static class CustomBuildingTemplateFactory
{
	internal static GameObject? Create(string id, ModBuildingDefinition definition, Microsoft.Extensions.Logging.ILogger log)
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
				"[BuildingContent] cannot build runtime template {Id}: base prefab {Template} was not found.",
				id, templateId);
			return null;
		}

		var template = UnityEngine.Object.Instantiate(basePrefab) as GameObject;
		if (template == null) // Unity object — ==
		{
			log.LogWarning(
				"[BuildingContent] cannot build runtime template {Id}: base prefab {Template} could not be instantiated.",
				id, templateId);
			return null;
		}

		template.name = id;
		template.SetActive(false);
		template.AddComponent<CustomBuildingTemplateMarker>();
		UnityEngine.Object.DontDestroyOnLoad(template);

		var building = template.GetComponent<BuildingEntity>();
		if (building == null) // Unity object — ==
		{
			UnityEngine.Object.Destroy(template);
			log.LogWarning(
				"[BuildingContent] cannot build runtime template {Id}: base prefab {Template} has no BuildingEntity component.",
				id, templateId);
			return null;
		}

		building.id = id;

		if (definition.Health is not null)
		{
			building.health = definition.Health.Value;
		}

		if (definition.RequireGround is not null)
		{
			building.requireGround = definition.RequireGround.Value;
		}

		if (definition.Animal is not null)
		{
			building.animal = definition.Animal.Value;
		}

		if (definition.CantHit is not null)
		{
			building.cantHit = definition.CantHit.Value;
		}

		if (definition.Metallic is not null)
		{
			building.metallic = definition.Metallic.Value;
		}

		if (definition.IgnoreBodyOptimize is not null)
		{
			building.ignoreBodyOptimize = definition.IgnoreBodyOptimize.Value;
		}

		if (definition.DropChanceMultiplier is not null)
		{
			building.dropChanceMultiplier = definition.DropChanceMultiplier.Value;
		}

		if (definition.GuaranteedDropAmount is not null)
		{
			building.guaranteedDropAmount = definition.GuaranteedDropAmount.Value;
		}

		CustomComponentAttach.Attach(template, definition.SpawnComponents, log, "BuildingContent");
		return template;
	}
}
