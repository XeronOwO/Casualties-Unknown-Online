using System;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The mod definition → vanilla <c>ItemInfo</c> mapping: the fields every custom
/// item carries, the behaviour slices a definition may author, and the no-effect
/// use action an item that declares usability WITHOUT a Tool or Gun behaviour is
/// given.
///
/// <para>
/// That last one is a contract rather than a convenience: <c>Body.UseItem</c> and
/// <c>Body.UseItemInHand</c> run <c>ItemInfo.useAction</c> behind nothing but the
/// item's own <c>usable</c> flag, so an installed item whose action is missing
/// would throw inside the game's own use path. The action installed here names the
/// content and changes nothing, because the mod API cannot carry a use-action
/// function yet — a declared behaviour with no function is reported, never thrown.
/// </para>
/// </summary>
internal static class ModItemInfoFactory
{
	/// <summary>Build the vanilla <c>ItemInfo</c> one accepted definition describes.</summary>
	internal static ItemInfo Build(string id, ModItemDefinition definition, ILogger log)
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

		if (definition.DecayMinutes > 0f)
		{
			info.decayMinutes = definition.DecayMinutes;
			info.rotSpeed = 1.666f / definition.DecayMinutes;
		}

		if (definition.Qualities.Count > 0)
		{
			info.qualities = CraftingQualityDeclarations.ToGameQualities(definition.Qualities);
		}

		if (definition.Tool is { } tool)
		{
			info.usable = true;
			info.usableWithLMB = true;
			info.autoAttack = true;
			info.useAction = (body, item) => UseTool(body, item, tool);
		}

		if (definition.Gun is not null)
		{
			info.usable = true;
			info.usableWithLMB = true;
			info.autoAttack = true;
			info.useAction = (body, item) =>
			{
				if (item != null) // Unity object — ==
				{
					var gun = item.GetComponent<GunScript>();
					if (gun != null) // Unity object — ==
					{
						gun.triggerPressed = true;
					}
				}
			};
			info.tags = AddTag(info.tags, "gun");
		}

		if (definition.Battery is not null)
		{
			info.destroyAtZeroCondition = false;
			info.decayInfo |= (byte)ItemInfo.DecayType.BatteryDecay;
		}

		if (!string.IsNullOrWhiteSpace(info.tags))
		{
			ApplyTags(info);
		}

		// The game's own use paths run this delegate on the item's `usable` flag
		// alone and never null-check it, so a declared-usable item carries one even
		// when the definition authors no behaviour of its own.
		if (info.useAction is null && (info.usable || info.usableWithLMB))
		{
			info.useAction = (body, item) => log.LogWarning(
				"[ItemContent] {Id} was used with no effect — the mod API cannot author a use action, so only a Tool or Gun behaviour gives an item one.",
				id);
		}

		return info;
	}

	private static void ApplyTags(ItemInfo info)
	{
		var field = typeof(ItemInfo).GetField("actualTags", BindingFlags.Instance | BindingFlags.NonPublic);
		if (field is null)
		{
			info.SetTags();
			return;
		}

		field.SetValue(info, (info.tags ?? string.Empty).Split(','));
	}

	private static void UseTool(Body? body, Item? item, ModItemTool tool)
	{
		if (body == null || item == null) // Unity objects — ==
		{
			return;
		}

		var attack = new AttackInfo
		{
			damage = tool.Damage,
			structuralDamage = tool.StructuralDamage,
			attackCooldownMult = tool.AttackCooldownMultiplier,
			distance = tool.Distance,
			knockBack = tool.KnockBack,
			cooldown = tool.Cooldown,
			attackAnim = string.IsNullOrWhiteSpace(tool.AttackAnimation)
				? null
				: Resources.Load<GameObject>(tool.AttackAnimation),
			staminaUse = tool.StaminaUse,
			piercing = tool.Piercing,
			swingSounds = [.. tool.SwingSounds],
			volume = tool.Volume,
			physicalSwing = tool.PhysicalSwing,
			rotateAmount = tool.RotateAmount,
			doAttackAnim = tool.DoAttackAnimation,
			metalMoreDamage = tool.MetalMoreDamage
		};

		if (body.Attack(attack, 0))
		{
			item.condition -= tool.ConditionLossOnHit;
		}
	}

	private static string AddTag(string? tags, string tag)
	{
		if (string.IsNullOrWhiteSpace(tags))
		{
			return tag;
		}

		if (tags!.Split(',').Any(entry => string.Equals(entry.Trim(), tag, StringComparison.OrdinalIgnoreCase)))
		{
			return tags;
		}

		return tags.TrimEnd(',') + "," + tag;
	}
}
