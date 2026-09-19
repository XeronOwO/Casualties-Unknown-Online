using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// One owner for the claims the medical operation family shares: which item
/// instance, which target limb and which operator slot an open operation holds.
/// The medical, shrapnel and other-medical services used to hand two
/// <see cref="HashSet{T}"/>s to each other by reference and to ask each other
/// "does this operator already have an operation" through a lambda chain, which
/// made the claim rule a property of three services at once. The rule now has a
/// single home, so changing it (the per-unit model) is a change in this type
/// instead of a change in three.
/// </summary>
/// <remarks>
/// The refusal vocabulary stays "already reserved": the reason string travels to
/// the operator over the wire and is part of the observed behaviour.
/// </remarks>
internal sealed class MedicalOperationClaims
{
	private readonly HashSet<ulong> _items = [];
	private readonly HashSet<(ulong Target, int Limb)> _limbs = [];
	private readonly HashSet<ulong> _operators = [];

	/// <summary>True when the item instance is held by an open operation.</summary>
	internal bool IsItemReserved(ulong itemInstanceId) => _items.Contains(itemInstanceId);

	/// <summary>Claims the item instance; false when it was already claimed.</summary>
	internal bool TryReserveItem(ulong itemInstanceId) => _items.Add(itemInstanceId);

	/// <summary>Releases the item instance the operation held.</summary>
	internal void ReleaseItem(ulong itemInstanceId) => _items.Remove(itemInstanceId);

	/// <summary>True when an open operation holds this target limb.</summary>
	internal bool IsLimbReserved(ulong target, int limbIndex) => _limbs.Contains((target, limbIndex));

	/// <summary>Claims the target limb; false when it was already claimed.</summary>
	internal bool TryReserveLimb(ulong target, int limbIndex) => _limbs.Add((target, limbIndex));

	/// <summary>Releases the target limb the operation held.</summary>
	internal void ReleaseLimb(ulong target, int limbIndex) => _limbs.Remove((target, limbIndex));

	/// <summary>True when this player already holds one operation of the family.</summary>
	internal bool IsOperatorBusy(ulong steamId) => _operators.Contains(steamId);

	/// <summary>Claims the operator slot; false when the player already held one.</summary>
	internal bool TryReserveOperator(ulong steamId) => _operators.Add(steamId);

	/// <summary>Releases the operator slot the operation held.</summary>
	internal void ReleaseOperator(ulong steamId) => _operators.Remove(steamId);

	/// <summary>Drops every claim (session end).</summary>
	internal void Clear()
	{
		_items.Clear();
		_limbs.Clear();
		_operators.Clear();
	}
}
