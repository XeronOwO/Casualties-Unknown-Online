using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The restore seam's account of the applied family's two carriers, read ONCE per
/// restore — the one moment both are known in full and neither can be behind the
/// other: the kernel holds the archive's item rows (an applied row says WHICH item
/// is installed on which limb) and the decoded character files hold the limb
/// components (the limb row says the component is there).
/// <para>
/// The rule is the design's: a disagreement is a DIVERGENCE the restore NAMES, never
/// one it guesses at. It is read-only by construction — nothing here relocates, ends
/// or invents an item — because either carrier could be the stale one, and picking a
/// winner would silently rewrite a world the player saved.
/// </para>
/// <para>
/// Both directions are named, and they fail differently: a component with no row is
/// an item whose id nobody recorded (the gesture that takes it off will have to make
/// a new item), while a row with no component is an item the world says is installed
/// on a limb that does not carry it (nothing will ever take it off). The pairs are
/// keyed by (owner, limb, definition) — the same binding the removal and take-off
/// seams resolve — so a splint and a tourniquet on one limb are two facts, not one.
/// </para>
/// </summary>
internal sealed class RestoredAppliedItemAudit(ItemKernelAuthority items, ILogger<RestoredAppliedItemAudit> log)
{
	/// <summary>The native component type a splint's installed form is; its <c>item</c> field names the definition.</summary>
	private const string SplintComponent = "SplintLimb";

	/// <summary>The tourniquet's component type, whose own <c>TakeOff</c> hardcodes the definition.</summary>
	private const string TourniquetComponent = "TourniquetScript";

	private const string TourniquetDefinition = "tourniquet";

	/// <summary>The definition a splint component falls back to when its own <c>item</c> field is absent.</summary>
	private const string SplintDefinition = "splint";

	/// <summary>
	/// Compare the restored characters' limb components against the restored kernel's
	/// applied rows, both ways, and name every disagreement with the three facts that
	/// locate it (owner, limb, definition). The pairs are what the caller BOUND — a
	/// stored character no present peer claims restores nobody's body, so auditing it
	/// would describe a divergence no player has.
	/// </summary>
	internal void Audit(IReadOnlyDictionary<ulong, CharacterDataMsg> bound)
	{
		if (bound.Count == 0)
		{
			return;
		}

		var installed = new HashSet<(ulong Owner, int LimbIndex, string Definition)>();
		var componentsWithoutRows = 0;
		foreach (var character in bound)
		{
			foreach (var (limbIndex, definition) in Components(character.Value))
			{
				installed.Add((character.Key, limbIndex, definition));
				if (items.FindAppliedLimbItem(new ActorId(character.Key), limbIndex, definition) is not null)
				{
					continue;
				}

				componentsWithoutRows++;
				log.LogWarning(
					"[AppliedAudit] the restored body of {Owner} carries a {Definition} component on limb {Limb}, but no applied item row names that limb — the component's item id was never recorded, so taking it off will create a NEW item.",
					character.Key, definition, limbIndex);
			}
		}

		var rowsWithoutComponents = 0;
		foreach (var item in items.QueryItems().Values)
		{
			if (item.Location.Kind != ItemLocationKind.Applied)
			{
				continue;
			}

			var pair = (item.Location.Owner.Value, item.Location.LimbIndex, item.Identity.DefinitionId);
			if (installed.Contains(pair))
			{
				continue;
			}

			rowsWithoutComponents++;
			log.LogWarning(
				"[AppliedAudit] the restored world says item {ItemId} ({Definition}) is installed on limb {Limb} of {Owner}, but that restored body carries no such component — the row names a limb the world does not wear it on.",
				item.Identity.InstanceId, item.Identity.DefinitionId, item.Location.LimbIndex, item.Location.Owner.Value);
		}

		var appliedRows = items.QueryItems().Values.Count(item => item.Location.Kind == ItemLocationKind.Applied);
		if (componentsWithoutRows == 0 && rowsWithoutComponents == 0)
		{
			log.LogInformation(
				"[AppliedAudit] the restored applied items and the restored limb components agree in both directions ({Owners} restored character(s), {Rows} applied row(s)).",
				bound.Count, appliedRows);
			return;
		}

		log.LogError(
			"[AppliedAudit] the restore found {Components} limb component(s) with no applied item row and {Rows} applied item row(s) with no limb component ({Characters} restored character(s), {Total} applied row(s)). Nothing was moved or ended: the two carriers are left exactly as the archive wrote them.",
			componentsWithoutRows, rowsWithoutComponents, bound.Count, appliedRows);
	}

	/// <summary>
	/// The (limb, definition) pairs one restored body's limb rows declare: the two
	/// component types whose own <c>TakeOff</c> instantiates an item again, which is
	/// the boundary the applied family exists for. Every other component — the ice
	/// pack's <c>ChilledLimb</c> included — is deliberately not a row here.
	/// </summary>
	private static IEnumerable<(int LimbIndex, string Definition)> Components(CharacterDataMsg data)
	{
		foreach (var limb in data.Limbs)
		{
			foreach (var component in limb.Components)
			{
				if (component.TypeName == TourniquetComponent)
				{
					yield return (limb.Index, TourniquetDefinition);
					continue;
				}

				if (component.TypeName == SplintComponent)
				{
					yield return (limb.Index, StringField(component, "item") ?? SplintDefinition);
				}
			}
		}
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
