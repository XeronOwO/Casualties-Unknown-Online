using System;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameAdapter.ModEffects;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The mod definition → vanilla <c>ItemInfo</c> mapping: every public field the
/// game's own item type carries, the behaviour slices a definition may author,
/// and the two action delegates an item that declares usability or limb
/// usability is given.
///
/// <para>
/// "Every public field" is a contract with a case behind it rather than a claim:
/// <c>ItemDeclarationParityTests</c> drives this build site with a definition
/// that declares each member and asserts that nothing the game reads is left
/// undeclarable — so a game update that adds a field fails the suite instead of
/// silently becoming a value every mod item inherits from the data class.
/// </para>
///
/// <para>
/// Those two are a contract rather than a convenience: <c>Body.UseItem</c> /
/// <c>Body.UseItemInHand</c> run <c>ItemInfo.useAction</c> behind nothing but the
/// item's own <c>usable</c> flag, and the game's own wound view runs
/// <c>ItemInfo.useLimbAction</c> behind <c>usableOnLimb</c>, and neither call site
/// null-checks. The delegates installed here are <see cref="ItemActionInvoker"/>'s:
/// they resolve, per call, the action the item's OWNER registered through
/// <see cref="IModItemActions"/> — the surface that makes a mod-authored use or limb
/// action expressible at all — and where none is registered the gesture still
/// completes and says so. That is the inert outcome
/// <c>mod-declared-behaviour-with-no-function.md</c> settled, now the "no action
/// registered" path rather than the only path.
/// </para>
/// </summary>
internal static class ModItemInfoFactory
{
	/// <summary>Build the vanilla <c>ItemInfo</c> one accepted definition describes.</summary>
	internal static ItemInfo Build(string id, IModItemDefinition definition, ItemActionInvoker actions)
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
			destroyAtZeroCondition = definition.DestroyAtZeroCondition,
			tags = definition.Tags ?? string.Empty,

			// The recognition gate is written from the DECLARATION rather than left to
			// the type's own field initialiser (`public Recognition rec = new(2)`,
			// ItemInfo.cs:181). That leftover asks for INT 2: 201 of the game's 218
			// locally built item definitions overwrite it and 24 inherit it, and a mod
			// author had no member either way — so an item whose author wrote only a name
			// would be shown as "unknown object", with the tooltip naming an INT
			// requirement nobody chose (PlayerCamera.ItemHoverDescription,
			// PlayerCamera.cs:1043-1055).
			rec = new Recognition(definition.RecognitionMinimum),
			onlyHoldInHands = definition.OnlyHoldInHands,
			combineable = definition.Combineable,
			ignoreDepression = definition.IgnoreDepression,
			scaleWeightWithCondition = definition.ScaleWeightWithCondition,
			autoAttack = definition.AutoAttack,
			jumpHeightMultChange = definition.JumpHeightMultiplierChange,
			slotRotation = definition.SlotRotation
		};

		// The three condition bits of the item's decay byte are the declaration's own;
		// their VALUES are the game's own flags (Item.HandleDecay masks bits 1, 2 and 4
		// out of `decayInfo`, Item.cs:186-197), and the validator refuses a value that
		// carries any other bit — the battery bit in particular, which belongs to the
		// Battery slice below because the decay tick drains that component.
		if (definition.DecayConditions != ModItemDecayCondition.None)
		{
			info.decayInfo |= (byte)definition.DecayConditions;
		}

		if (definition.DecayMinutes > 0f)
		{
			info.decayMinutes = definition.DecayMinutes;
			info.rotSpeed = 1.666f / definition.DecayMinutes;
		}

		var qualities = ModDeclarationCollections.OrEmpty(definition.Qualities);
		if (qualities.Count > 0)
		{
			info.qualities = CraftingQualityDeclarations.ToGameQualities(qualities);
		}

		// The wearable flag and the placement the game's own wear flow reads are one
		// declaration: Body.WearWearable resolves the declared limb name with
		// Body.LimbByName (Body.cs:1493) and dereferences the result on the next line,
		// so a flag installed without one is a NullReferenceException inside the game.
		// An incomplete declaration is therefore NOT installed as a wearable; the
		// provider reports it by name at load.
		if (definition.Wearable is { } wearable && WearableDeclaration.HasPlacement(wearable))
		{
			info.wearable = true;
			info.desiredWearLimb = wearable.Limb;
			info.wearSlotId = wearable.SlotId;
			info.wearableCanBeHeld = wearable.CanBeHeld;
			info.wearableArmor = wearable.Armor;
			info.wearableIsolation = wearable.Isolation;
			info.wearableHitDurabilityLossMultiplier = wearable.HitDurabilityLossMultiplier;
			info.wearableVisualOffset = wearable.VisualOffset;
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
		// when the definition authors no behaviour of its own. It resolves the action
		// its owner registered through IModItemActions, per call.
		//
		// `usableWithLMB` is checked TOGETHER with `usable` here although only
		// `usable` makes the delegate reachable (both of the game's use call sites
		// require it — see ModItemActionsAdapter.IsReachable): leaving no null
		// delegate is a different promise from judging reachability, and a definition
		// that declares the second flag without the first still gets a delegate that
		// reports instead of a null the game would throw on if a later update grew a
		// third call site. The registration rail is what refuses that shape.
		if (info.useAction is null && (info.usable || info.usableWithLMB))
		{
			info.useAction = actions.UseAction(id);
		}

		// The limb half on the same rule: the wound view runs it behind
		// `usableOnLimb` and never null-checks it, so a definition that declares the
		// flag carries a delegate either way. Installing one is also what puts a mod
		// item into the cross-player limb-tool chain, which reads exactly this pair
		// (`usableOnLimb` and a non-null `useLimbAction`).
		if (definition.UsableOnLimb)
		{
			info.usableOnLimb = true;
			info.useLimbAction = actions.LimbAction(id);
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

	private static void UseTool(Body? body, Item? item, IModItemTool tool)
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
			swingSounds = [.. ModDeclarationCollections.OrEmpty(tool.SwingSounds)],
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
