using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The container behavior of a custom item. This is the contract a consumer
/// reads: <see cref="IModItemDefinition.Container"/> is interface-typed, so a mod
/// that computes a container value hands over its own implementation instead of
/// filling in the framework's.
///
/// <see cref="ModItemContainer"/> is the framework's ready-made implementation:
/// use it when every value is a constant, and implement this interface when one is
/// computed. Partial customisation is composition — an implementation hands back
/// a filled default for the members it does not touch — never inheritance from
/// the data class, which stays <c>sealed</c>.
///
/// <see cref="TagRestriction"/> returns a collection, so it is read through
/// <see cref="ModDeclarationCollections"/>: null means "every item is accepted",
/// the same rule every collection member of a declaration answers for, and the
/// framework's own implementation cannot produce null because a null write
/// coalesces into an empty list.
/// </summary>
public interface IModItemContainer
{
	/// <summary>Maximum total weight the container can hold.</summary>
	float Capacity { get; }

	/// <summary>Maximum weight allowed for one contained item.</summary>
	float MaxWeightPerItem { get; }

	/// <summary>Encumbrance multiplier applied to contained items (1 = normal).</summary>
	float EncumbranceReduction { get; }

	/// <summary>Whether contained items stay visually visible while inside.</summary>
	bool ItemsVisible { get; }

	/// <summary>Optional item-tag restriction. Null or empty means every item is accepted.</summary>
	List<string> TagRestriction { get; }
}
