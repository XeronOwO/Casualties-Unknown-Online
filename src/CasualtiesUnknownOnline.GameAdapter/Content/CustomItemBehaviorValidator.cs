using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Validates the advanced behavior payloads on a <see cref="ModItemDefinition"/>
/// before the Game Adapter accepts it. The checks only cover authored numeric
/// contracts that could produce invalid runtime state (negative capacities,
/// NaN light values, zero-shot guns); game state that is later changed by the
/// base template remains the base prefab's responsibility.
/// </summary>
internal static class CustomItemBehaviorValidator
{
	internal static bool Validate(
		string modId,
		string id,
		ModItemDefinition definition,
		ILogger log)
	{
		var valid = true;

		if (definition.Container is { } container)
		{
			valid &= ValidateContainer(modId, id, container, log);
		}

		if (definition.Battery is { } battery)
		{
			valid &= ValidateBattery(modId, id, battery, log);
		}

		if (definition.Light is { } light)
		{
			valid &= ValidateLight(modId, id, light, log);
		}

		if (definition.Tool is { } tool)
		{
			valid &= ValidateTool(modId, id, tool, log);
		}

		if (definition.Gun is { } gun)
		{
			valid &= ValidateGun(modId, id, gun, log);
		}

		return valid;
	}

	private static bool ValidateContainer(
		string modId,
		string id,
		ModItemContainer container,
		ILogger log)
	{
		if (IsInvalidNonNegative(container.Capacity)
			|| IsInvalidNonNegative(container.MaxWeightPerItem)
			|| IsInvalidNonNegative(container.EncumbranceReduction))
		{
			log.LogWarning(
				"[ItemContent] {ModId}/{Id} has an invalid Container numeric value — refused.",
				modId, id);
			return false;
		}

		return true;
	}

	private static bool ValidateBattery(
		string modId,
		string id,
		ModItemBattery battery,
		ILogger log)
	{
		if (float.IsNaN(battery.StartCharge)
			|| float.IsInfinity(battery.StartCharge)
			|| battery.StartCharge < -1f)
		{
			log.LogWarning(
				"[ItemContent] {ModId}/{Id} has invalid Battery.StartCharge {StartCharge} — refused.",
				modId, id, battery.StartCharge);
			return false;
		}

		return true;
	}

	private static bool ValidateLight(
		string modId,
		string id,
		ModItemLight light,
		ILogger log)
	{
		if (IsInvalidNonNegative(light.Intensity)
			|| IsInvalidNonNegative(light.FalloffIntensity)
			|| IsInvalidNonNegative(light.OuterRadius)
			|| IsInvalidNonNegative(light.InnerRadius)
			|| IsInvalidNonNegative(light.OuterAngle)
			|| IsInvalidNonNegative(light.InnerAngle)
			|| IsInvalidColor(light.ColorR)
			|| IsInvalidColor(light.ColorG)
			|| IsInvalidColor(light.ColorB)
			|| IsInvalidColor(light.ColorA))
		{
			log.LogWarning(
				"[ItemContent] {ModId}/{Id} has an invalid Light numeric value — refused.",
				modId, id);
			return false;
		}

		return true;
	}

	private static bool ValidateTool(
		string modId,
		string id,
		ModItemTool tool,
		ILogger log)
	{
		if (IsInvalidNonNegative(tool.Damage)
			|| IsInvalidNonNegative(tool.StructuralDamage)
			|| IsInvalidNonNegative(tool.AttackCooldownMultiplier)
			|| IsInvalidNonNegative(tool.Distance)
			|| IsInvalidNonNegative(tool.KnockBack)
			|| IsInvalidNonNegative(tool.Cooldown)
			|| IsInvalidNonNegative(tool.StaminaUse)
			|| IsInvalidNonNegative(tool.Volume)
			|| IsInvalidNonNegative(tool.RotateAmount)
			|| IsInvalidNonNegative(tool.ConditionLossOnHit))
		{
			log.LogWarning(
				"[ItemContent] {ModId}/{Id} has an invalid Tool numeric value — refused.",
				modId, id);
			return false;
		}

		return true;
	}

	private static bool ValidateGun(
		string modId,
		string id,
		ModItemGun gun,
		ILogger log)
	{
		if (gun.MagCapacity is < 0)
		{
			log.LogWarning(
				"[ItemContent] {ModId}/{Id} has invalid Gun.MagCapacity {MagCapacity} — refused.",
				modId, id, gun.MagCapacity);
			return false;
		}

		if (gun.ShotsPerFire is < 1)
		{
			log.LogWarning(
				"[ItemContent] {ModId}/{Id} has invalid Gun.ShotsPerFire {ShotsPerFire} — refused.",
				modId, id, gun.ShotsPerFire);
			return false;
		}

		if (IsInvalidNullableNonNegative(gun.KnockBack)
			|| IsInvalidNullableNonNegative(gun.StructureDamage)
			|| IsInvalidNullableNonNegative(gun.AnimalDamage)
			|| IsInvalidNullableNonNegative(gun.Loudness)
			|| IsInvalidNullableNonNegative(gun.DesiredGasTime)
			|| IsInvalidNullableNonNegative(gun.VerticalSpread)
			|| IsInvalidNullableNonNegative(gun.ConditionLossPerShot))
		{
			log.LogWarning(
				"[ItemContent] {ModId}/{Id} has an invalid Gun numeric value — refused.",
				modId, id);
			return false;
		}

		return true;
	}

	private static bool IsInvalidNonNegative(float value) =>
		float.IsNaN(value) || float.IsInfinity(value) || value < 0f;

	private static bool IsInvalidNullableNonNegative(float? value) =>
		value is { } number && IsInvalidNonNegative(number);

	private static bool IsInvalidColor(float value) =>
		float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f;
}
