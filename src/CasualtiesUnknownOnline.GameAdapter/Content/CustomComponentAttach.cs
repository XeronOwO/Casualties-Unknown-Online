using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Shared component-attachment helper for the custom item and building
/// template factories. Both factories clone a vanilla prefab and then attach
/// mod-authored component types by name; keeping the resolution and validation
/// in one place prevents the two content families from drifting.
/// </summary>
internal static class CustomComponentAttach
{
	internal static void Attach(
		GameObject target,
		IEnumerable<string> componentTypeNames,
		Microsoft.Extensions.Logging.ILogger log,
		string logContext)
	{
		foreach (var componentTypeName in componentTypeNames)
		{
			if (string.IsNullOrWhiteSpace(componentTypeName))
			{
				continue;
			}

			var componentType = ResolveComponentType(componentTypeName);
			if (componentType is null)
			{
				log.LogWarning(
					"[{LogContext}] template cannot attach component {Component}: type was not found.",
					logContext, componentTypeName);
				continue;
			}

			if (!typeof(Component).IsAssignableFrom(componentType))
			{
				log.LogWarning(
					"[{LogContext}] template cannot attach {Component}: type is not a Unity Component.",
					logContext, componentTypeName);
				continue;
			}

			if (target.GetComponent(componentType) != null) // Unity object — ==
			{
				log.LogDebug(
					"[{LogContext}] template already has {Component}; skipped duplicate attach.",
					logContext, componentTypeName);
				continue;
			}

			target.AddComponent(componentType);
			log.LogInformation(
				"[{LogContext}] attached {Component} to template {Template}.",
				logContext, componentTypeName, target.name);
		}
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
