using System;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;
using UnityEngine;

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
	internal static GameObject? Create(string id, ModItemDefinition definition, Microsoft.Extensions.Logging.ILogger log)
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

		var template = UnityEngine.Object.Instantiate(basePrefab) as GameObject;
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
		UnityEngine.Object.DontDestroyOnLoad(template);

		var item = template.GetComponent<Item>();
		if (item == null) // Unity object — ==
		{
			UnityEngine.Object.Destroy(template);
			log.LogWarning(
				"[ItemContent] cannot build runtime template {Id}: base prefab {Template} has no Item component.",
				id, templateId);
			return null;
		}

		item.id = id;

		foreach (var componentTypeName in definition.SpawnComponents)
		{
			if (string.IsNullOrWhiteSpace(componentTypeName))
			{
				continue;
			}

			var componentType = ResolveComponentType(componentTypeName);
			if (componentType is null)
			{
				log.LogWarning(
					"[ItemContent] template {Id} cannot attach component {Component}: type was not found.",
					id, componentTypeName);
				continue;
			}

			if (!typeof(Component).IsAssignableFrom(componentType))
			{
				log.LogWarning(
					"[ItemContent] template {Id} cannot attach {Component}: type is not a Unity Component.",
					id, componentTypeName);
				continue;
			}

			if (template.GetComponent(componentType) != null) // Unity object — ==
			{
				log.LogDebug(
					"[ItemContent] template {Id} already has {Component}; skipped duplicate attach.",
					id, componentTypeName);
				continue;
			}

			template.AddComponent(componentType);
			log.LogInformation("[ItemContent] attached {Component} to template {Id}.", componentTypeName, id);
		}

		return template;
	}

	private static Type? ResolveComponentType(string name)
	{
		var direct = Type.GetType(name, throwOnError: false);
		if (direct is not null)
		{
			return direct;
		}

		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			try
			{
				var type = assembly.GetType(name, throwOnError: false);
				if (type is not null)
				{
					return type;
				}

				foreach (var candidate in assembly.GetTypes())
				{
					if (candidate.Name == name)
					{
						return candidate;
					}
				}
			}
			catch (ReflectionTypeLoadException ex)
			{
				foreach (var candidate in ex.Types)
				{
					if (candidate is not null && candidate.Name == name)
					{
						return candidate;
					}
				}
			}
		}

		return null;
	}
}
