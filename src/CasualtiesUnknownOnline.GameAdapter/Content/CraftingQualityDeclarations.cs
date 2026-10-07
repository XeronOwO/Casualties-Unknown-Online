using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The crafting-quality labels one content provider accepted, and the label rule
/// it applies while binding. A label is either a vanilla one — a bare lower-case
/// token such as <c>rippable</c>, which is how a mod says "this content provides
/// the vanilla label", and all 18 of the game's own labels satisfy the content-id
/// NAMESPACE grammar — or a mod-authored one namespaced with the content-id
/// grammar (<c>mymod:material</c>), which is what keeps two mods' vocabularies
/// apart.
///
/// <para>
/// Canonical means the text IS what reaches the comparison: the game matches
/// qualities with <c>q.id == target.id</c> (an ordinal string comparison in
/// <c>Item.GetQualityThatMeetsCriteria</c> and <c>HasCommonQuality</c>), so a
/// mixed-case or padded label would be stored verbatim and match nothing —
/// refused here rather than normalised, because normalising would hide the
/// author's typo instead of reporting it.
/// </para>
///
/// <para>
/// A declaration carries its AMOUNT as well as its id, because the matcher asks
/// <c>q.amount &gt;= target.amount</c>: an item declaring <c>cutting 1</c> cannot
/// satisfy a recipe asking for <c>cutting 4</c>, so a check that only asked
/// "does anybody carry this label" would inject that recipe anyway. Membership in
/// the VANILLA vocabulary is deliberately not decided here — a mod binds before
/// the item table need not exist — so this is a shape-and-amount record, and the
/// recipe provider is where a label that resolves to nothing is caught.
/// </para>
/// </summary>
internal sealed class CraftingQualityDeclarations
{
	private readonly Dictionary<string, float> _amounts = [with(StringComparer.Ordinal)];

	/// <summary>
	/// True when every label is one the game could match. False names the
	/// offending label so the caller can report it; a null entry is refused with
	/// an empty id, because a payload can carry one and the injection path would
	/// otherwise dereference it.
	/// </summary>
	internal static bool IsValid(List<ModCraftingQuality> qualities, out string rejectedId)
	{
		foreach (var quality in qualities)
		{
			if (quality is not null && IsCanonical(quality.Id))
			{
				continue;
			}

			rejectedId = quality?.Id ?? string.Empty;
			return false;
		}

		rejectedId = string.Empty;
		return true;
	}

	/// <summary>
	/// The game-side qualities of a definition, ready for its item or liquid entry.
	/// A non-positive declared amount becomes <c>1</c> — the rule the liquid
	/// provider applied before this type existed, kept because the matcher needs a
	/// reachable amount and the recipe side normalises its own requirement to at
	/// least 1.
	/// </summary>
	internal static List<CraftingQuality> ToGameQualities(List<ModCraftingQuality> qualities) =>
		[.. qualities.Select(quality => new CraftingQuality(quality.Id, ReachableAmount(quality.Amount)))];

	/// <summary>
	/// Record the labels of a definition the caller accepted. One label declared
	/// twice keeps its larger amount, which is what a consumer comparing amounts
	/// has to assume.
	/// </summary>
	internal void Accept(List<ModCraftingQuality> qualities)
	{
		foreach (var quality in qualities)
		{
			var amount = ReachableAmount(quality.Amount);
			if (!_amounts.TryGetValue(quality.Id, out var existing) || amount > existing)
			{
				_amounts[quality.Id] = amount;
			}
		}
	}

	/// <summary>
	/// True when an accepted definition of the owning provider declares the label
	/// with an amount that can satisfy <paramref name="requiredAmount"/>. A
	/// requirement of <c>0</c> asks only for presence — the liquid direction, whose
	/// amounts are scaled by the volume in a container and so are reachable from
	/// any positive declaration.
	/// </summary>
	internal bool Provides(string qualityId, float requiredAmount) =>
		_amounts.TryGetValue(qualityId, out var amount) && amount >= requiredAmount;

	/// <summary>
	/// The amount a declaration and a recipe requirement normalise to: the
	/// matcher compares the two directly, so both sides must apply one rule.
	/// </summary>
	internal static float ReachableAmount(float declared) => declared <= 0f ? 1f : declared;

	private static bool IsCanonical(string? id)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			return false;
		}

		return ContentId.IsValidNamespace(id)
			|| (ContentId.TryParse(id, out var parsed)
				&& string.Equals(parsed.ToString(), id, StringComparison.Ordinal));
	}
}
