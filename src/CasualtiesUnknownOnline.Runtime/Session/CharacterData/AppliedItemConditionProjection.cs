using System;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The ONE writer of an applied item row's durability AFTER the apply: the body's own
/// 1 Hz character capture, which already reads the limb component's live
/// <c>condition</c> — the field the game's own <c>Update</c> writes every frame on the
/// client that simulates the body.
/// <para>
/// The ruled model splits the state by who writes it: the item row owns the item's
/// DURABILITY (the game copies it in both directions and the player reads it as the
/// component's remaining life), while <c>conditionLossMinute</c> and <c>timeApplied</c>
/// are properties of this application and stay on the component. So the row has to
/// follow the component, and this is the only seam that carries the value: without it
/// the row keeps whatever the apply wrote, and a mod reading the item, and the archive
/// that stores it, would see a splint frozen at the condition it was installed with.
/// </para>
/// <para>
/// The row is updated only when the component has moved by <see cref="ConditionStep"/>,
/// so a slow decay costs one command per step rather than one per report — and the row's
/// staleness is bounded by that step and named rather than hidden. What is NOT written
/// here is the value used at a removal: that stays the affected side's live component
/// (decision 184), so the player always receives the condition the splint really has.
/// </para>
/// </summary>
public sealed class AppliedItemConditionProjection(
	ISessionControl session,
	ItemKernelAuthority authority,
	ILogger<AppliedItemConditionProjection> log)
{
	/// <summary>How far the component's condition must move before the durable row follows it.</summary>
	private const float ConditionStep = 0.01f;

	private const string SplintComponent = "SplintLimb";

	private const string TourniquetComponent = "TourniquetScript";

	private const string TourniquetDefinition = "tourniquet";

	private const string SplintDefinition = "splint";

	/// <summary>
	/// Host side: one character snapshot arrived (a member's 1 Hz report, or the host's own).
	/// Every applied row of that body whose limb carries the matching component follows the
	/// component's condition; an owner with no rows, or a component no row names, costs one
	/// lookup each and changes nothing.
	/// </summary>
	public void SyncFromCharacterData(ulong owner, CharacterDataMsg data)
	{
		// The kernel is the HOST's fact store; a guest replays it and must never author a
		// command of its own (its own reports are what the host judges).
		if (session.Role == SessionRole.Guest || owner == 0)
		{
			return;
		}

		foreach (var limb in data.Limbs)
		{
			foreach (var component in limb.Components)
			{
				var definition = DefinitionOf(component);
				if (definition is null)
				{
					continue;
				}

				var row = authority.FindAppliedLimbItem(new ActorId(owner), limb.Index, definition);
				if (row is null)
				{
					continue;
				}

				var condition = ConditionOf(component);
				if (Math.Abs(row.Value.Data.Condition - condition) < ConditionStep)
				{
					continue;
				}

				var updated = ItemKernelAuthority.ToCharacterItem(row.Value);
				updated.Condition = condition;
				if (!authority.TryUpdateState(session.LocalSteamId, row.Value.Identity.InstanceId, updated, out _, out var rejection))
				{
					log.LogWarning(
						"[AppliedItem] could not move item {ItemId} ({Definition}) to the condition the limb component reports ({Condition:F3}): {Reason}.",
						row.Value.Identity.InstanceId, definition, condition, rejection?.Reason);
					continue;
				}

				log.LogDebug(
					"[AppliedItem] item {ItemId} ({Definition}) on limb {Limb} of {Owner} now reads condition {Condition:F3} — the durable row follows the component the body reports.",
					row.Value.Identity.InstanceId, definition, limb.Index, owner, condition);
			}
		}
	}

	/// <summary>
	/// The item definition a limb component IS, for the two types whose own <c>TakeOff</c>
	/// instantiates an item again — the same boundary the applied family is defined by. Every
	/// other component (the ice pack's <c>ChilledLimb</c> included) answers null and is not a row.
	/// </summary>
	private static string? DefinitionOf(ComponentStateMsg component) => component.TypeName switch
	{
		TourniquetComponent => TourniquetDefinition,
		SplintComponent => StringField(component, "item") ?? SplintDefinition,
		_ => null,
	};

	private static float ConditionOf(ComponentStateMsg component)
	{
		foreach (var field in component.Fields)
		{
			if (field.Name == "condition" && field.Kind == SaveableFieldKind.Float)
			{
				return field.FloatValue;
			}
		}

		return 0f;
	}

	private static string? StringField(ComponentStateMsg component, string name)
	{
		foreach (var field in component.Fields)
		{
			if (field.Name == name && field.Kind == SaveableFieldKind.String)
			{
				return field.StringValue;
			}
		}

		return null;
	}
}
