using System;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Applies the advanced behavior DTOs of a <see cref="ModItemDefinition"/> to a
/// CUO custom item runtime template. Container/battery/gun are vanilla game
/// components configured directly; light uses a reflection seam because the URP
/// <c>Light2D</c> type is not in the Game Adapter reference graph. The template
/// is inactive by design, so the component fields are set before any Awake/Start
/// runs on a spawned clone.
/// </summary>
internal static class CustomItemBehaviorApplier
{
	private static Type? _light2DType;

	internal static void Apply(GameObject template, ModItemDefinition definition, Microsoft.Extensions.Logging.ILogger log)
	{
		if (template == null) // Unity object — ==
		{
			return;
		}

		if (definition.Container is { } container)
		{
			ApplyContainer(template, container);
		}

		if (definition.Battery is { } battery)
		{
			ApplyBattery(template, battery);
		}

		if (definition.Gun is { } gun)
		{
			ApplyGun(template, gun);
		}

		if (definition.Light is { } light)
		{
			ApplyLight(template, light, log);
		}

		if (definition.Visual is { } visual)
		{
			ApplyVisual(template, visual, log);
		}
	}

	private static void ApplyVisual(GameObject template, ModItemVisual visual, Microsoft.Extensions.Logging.ILogger log)
	{
		var state = template.GetComponent<CustomItemVisualState>();
		if (state == null) // Unity object — ==
		{
			state = template.AddComponent<CustomItemVisualState>();
		}

		var renderer = template.GetComponent<SpriteRenderer>();
		if (renderer != null) // Unity object — ==
		{
			state.NormalSprite = renderer.sprite;
			state.NormalSortingOrder = renderer.sortingOrder;
		}

		if (!string.IsNullOrWhiteSpace(visual.WornSpritePath))
		{
			var wornSprite = Resources.Load<Sprite>(visual.WornSpritePath);
			if (wornSprite == null) // Unity object — ==
			{
				log.LogWarning(
					"[ItemContent] cannot resolve worn sprite resource for template {Template}: {Path}.",
					template.name, visual.WornSpritePath);
			}
			else
			{
				state.HasWornSprite = true;
				state.WornSprite = wornSprite;
			}
		}

		state.WornOffset = new Vector2(visual.WornSpriteOffsetX, visual.WornSpriteOffsetY);
		if (visual.WornSpriteSortingOrder is { } sortingOrder)
		{
			state.HasWornSortingOrder = true;
			state.WornSortingOrder = sortingOrder;
		}

		if (!string.IsNullOrWhiteSpace(visual.LiquidMaskPath))
		{
			var liquidMask = Resources.Load<Sprite>(visual.LiquidMaskPath);
			if (liquidMask == null) // Unity object — ==
			{
				log.LogWarning(
					"[ItemContent] cannot resolve liquid-mask sprite resource for template {Template}: {Path}.",
					template.name, visual.LiquidMaskPath);
			}
			else
			{
				state.HasLiquidMask = true;
				state.LiquidMaskSprite = liquidMask;
			}
		}

		state.ApplyLiquidMask();
	}

	private static void ApplyContainer(GameObject template, ModItemContainer container)
	{
		var cont = template.GetComponent<Container>();
		if (cont == null) // Unity object — ==
		{
			cont = template.AddComponent<Container>();
		}

		cont.maxWeight = container.Capacity;
		cont.maxWeightPerItem = container.MaxWeightPerItem;
		cont.encumberanceMult = container.EncumbranceReduction;
		cont.itemsVisible = container.ItemsVisible;
		cont.tagRestriction = container.TagRestriction?.ToArray() ?? [];
	}

	private static void ApplyBattery(GameObject template, ModItemBattery battery)
	{
		var item = template.GetComponent<Item>();
		var bat = template.GetComponent<BatteryItem>();
		if (bat == null) // Unity object — ==
		{
			bat = template.AddComponent<BatteryItem>();
		}

		var maxCharge = PresetToMaxCharge(battery.Preset);
		bat.preset = (BatteryItem.BatteryPreset)(int)battery.Preset;
		bat.maxAllowedCharge = maxCharge;
		bat.notSpawnWithBattery = !battery.SpawnWithBattery;
		bat.batteryWasFavourited = false;

		if (item == null) // Unity object — ==
		{
			return;
		}

		if (!battery.SpawnWithBattery)
		{
			bat.batteryType = string.Empty;
			bat.maxCharge = 0f;
			item.condition = 0f;
			return;
		}

		bat.batteryType = PresetToBatteryId(battery.Preset);
		bat.maxCharge = maxCharge;

		var startCharge = battery.StartCharge < 0f
			? maxCharge
			: battery.StartCharge <= 1f
				? maxCharge * battery.StartCharge
				: Mathf.Min(battery.StartCharge, maxCharge);

		item.condition = Mathf.Clamp01(startCharge / Mathf.Max(1f, maxCharge));
	}

	private static void ApplyGun(GameObject template, ModItemGun gun)
	{
		var gunScript = template.GetComponent<GunScript>();
		if (gunScript == null) // Unity object — ==
		{
			gunScript = template.AddComponent<GunScript>();
		}

		if (gun.AmmoType.HasValue)
		{
			gunScript.ammoType = (GunScript.AmmoType)(int)gun.AmmoType.Value;
		}

		if (gun.FiringMode.HasValue)
		{
			gunScript.firingMode = (GunScript.FiringMode)(int)gun.FiringMode.Value;
		}

		if (gun.FeedType.HasValue)
		{
			gunScript.feedType = (GunScript.FeedType)(int)gun.FeedType.Value;
		}

		if (gun.MagCapacity.HasValue)
		{
			gunScript.magCapacity = Mathf.Max(0, gun.MagCapacity.Value);
		}

		if (gun.KnockBack.HasValue)
		{
			gunScript.knockBack = gun.KnockBack.Value;
		}

		if (gun.StructureDamage.HasValue)
		{
			gunScript.structureDamage = gun.StructureDamage.Value;
		}

		if (gun.AnimalDamage.HasValue)
		{
			gunScript.animalDamage = gun.AnimalDamage.Value;
		}

		if (gun.Loudness.HasValue)
		{
			gunScript.loudness = gun.Loudness.Value;
		}

		if (gun.DesiredGasTime.HasValue)
		{
			gunScript.desiredGasTime = gun.DesiredGasTime.Value;
		}

		if (gun.ShotsPerFire.HasValue)
		{
			gunScript.shotsPerFire = Mathf.Max(1, gun.ShotsPerFire.Value);
		}

		if (gun.VerticalSpread.HasValue)
		{
			gunScript.verticalSpread = gun.VerticalSpread.Value;
		}

		if (gun.ConditionLossPerShot.HasValue)
		{
			gunScript.conditionLossPerShot = gun.ConditionLossPerShot.Value;
		}

		ApplyGunSpriteFallbacks(template, gunScript);
	}

	private static void ApplyLight(GameObject template, ModItemLight light, Microsoft.Extensions.Logging.ILogger log)
	{
		LightItem? lightItem = null;
		if (light.AddLightItem)
		{
			lightItem = template.GetComponent<LightItem>();
			if (lightItem == null) // Unity object — ==
			{
				lightItem = template.AddComponent<LightItem>();
			}
		}

		var lightType = ResolveLight2DType();
		if (lightType is null)
		{
			log.LogWarning(
				"[ItemContent] cannot apply Light behavior to template {Id}: Light2D type was not found in loaded assemblies.",
				template.name);
			return;
		}

		var lightComponent = FindExistingLight(template, lightType);
		if (lightComponent is null)
		{
			var child = new GameObject("CustomLight");
			child.transform.SetParent(template.transform, false);
			lightComponent = child.AddComponent(lightType);
		}

		var component = (Component)lightComponent;
		component.transform.localPosition = new Vector3(light.OffsetX, light.OffsetY, 0f);
		component.transform.localRotation = Quaternion.Euler(0f, 0f, light.Rotation);

		SetLightProperty(component, "intensity", light.Intensity);
		SetLightProperty(component, "color", new Color(light.ColorR, light.ColorG, light.ColorB, light.ColorA));
		SetLightProperty(component, "falloffIntensity", light.FalloffIntensity);
		SetLightProperty(component, "pointLightOuterRadius", light.OuterRadius);
		SetLightProperty(component, "pointLightInnerRadius", light.InnerRadius);
		SetLightProperty(component, "pointLightOuterAngle", light.OuterAngle);
		SetLightProperty(component, "pointLightInnerAngle", light.InnerAngle);

		var enumType = GetPropertyOrFieldType(component, "lightType");
		if (enumType is { IsEnum: true })
		{
			SetLightProperty(component, "lightType", Enum.ToObject(enumType, (int)light.LightType));
		}

		MarkLightForUpdate(component, light);

		if (lightItem != null)
		{
			SetLightItemLight(lightItem, component);
			lightItem.shouldEnable = true;
		}
	}

	private static void ApplyGunSpriteFallbacks(GameObject template, GunScript gunScript)
	{
		var renderer = template.GetComponent<SpriteRenderer>();
		if (renderer == null || renderer.sprite == null) // Unity objects — ==
		{
			return;
		}

		var sprite = renderer.sprite;
		gunScript.normalSprite ??= sprite;
		gunScript.rackedSprite ??= sprite;
		gunScript.normalSpriteNoMag ??= sprite;
		gunScript.rackedSpriteNoMag ??= sprite;
	}

	private static Component? FindExistingLight(GameObject template, Type lightType)
	{
		foreach (var candidate in template.GetComponentsInChildren<Component>(true))
		{
			if (candidate.GetType() == lightType)
			{
				return candidate;
			}
		}

		return null;
	}

	private static void SetLightProperty(Component component, string name, object? value)
	{
		var type = component.GetType();
		var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
		if (property is not null)
		{
			property.SetValue(component, value, null);
			return;
		}

		var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
		if (field is not null)
		{
			field.SetValue(component, value);
		}
	}

	private static Type? GetPropertyOrFieldType(Component component, string name)
	{
		var type = component.GetType();
		return type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.PropertyType
			?? type.GetField(name, BindingFlags.Public | BindingFlags.Instance)?.FieldType;
	}

	private static void SetLightItemLight(LightItem lightItem, Component lightComponent)
	{
		var field = typeof(LightItem).GetField("light", BindingFlags.Public | BindingFlags.Instance);
		if (field is not null)
		{
			field.SetValue(lightItem, lightComponent);
		}
	}

	private static void MarkLightForUpdate(Component component, ModItemLight light)
	{
		if (light.LightType != ModLightType.Point
			|| (light.InnerAngle >= 360f && light.OuterAngle >= 360f))
		{
			return;
		}

		var method = component.GetType().GetMethod(
			"MarkForUpdate",
			BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
			null,
			Type.EmptyTypes,
			null);
		method?.Invoke(component, null);
	}

	private static Type? ResolveLight2DType()
	{
		if (_light2DType is not null)
		{
			return _light2DType;
		}

		try
		{
			var assembly = Assembly.Load("Unity.RenderPipelines.Universal.Runtime");
			_light2DType = assembly.GetType("UnityEngine.Rendering.Universal.Light2D", throwOnError: false);
			if (_light2DType is not null)
			{
				return _light2DType;
			}
		}
		catch
		{
			// The URP assembly may already be loaded under a different simple name;
			// the scan below handles the already-loaded case.
		}

		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			Type[] types;
			try
			{
				types = assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException ex)
			{
				types = ex.Types;
			}

			foreach (var type in types)
			{
				if (type is not null
					&& type.Name == "Light2D"
					&& string.Equals(type.Namespace, "UnityEngine.Rendering.Universal", StringComparison.Ordinal))
				{
					_light2DType = type;
					return type;
				}
			}
		}

		return null;
	}

	private static float PresetToMaxCharge(ModBatteryPreset preset) =>
		preset switch
		{
			ModBatteryPreset.Small => 50f,
			ModBatteryPreset.Large => 300f,
			_ => 100f
		};

	private static string PresetToBatteryId(ModBatteryPreset preset) =>
		preset switch
		{
			ModBatteryPreset.Small => "smallbattery",
			ModBatteryPreset.Large => "largebattery",
			_ => "mediumbattery"
		};
}
