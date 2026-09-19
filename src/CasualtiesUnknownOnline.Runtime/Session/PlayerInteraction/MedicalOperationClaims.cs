using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// One owner for the claims the medical operation family shares. Several
/// operators may work one victim — and one limb — at the same time, so the only
/// claims left are the two that are a fact about a single holder: the ITEM an
/// operation consumes (one instance must not be spent twice) and the OPERATOR
/// slot (the native engine runs one minigame per client, so one player cannot run
/// two operations at once).
/// </summary>
/// <remarks>
/// The target limb is deliberately NOT claimed here. The unit of work is
/// (target, limb, operation kind) plus, where the minigame has discrete pieces,
/// the piece itself — several units on one limb coexist, and the outcome of a unit
/// that resolves once is arbitrated by <see cref="MedicalOperationUnitRules"/>
/// against the authoritative limb state, never by a lock taken on the limb. The
/// refusal vocabulary stays "already reserved": the reason string travels to the
/// operator over the wire and is part of the observed behaviour.
/// </remarks>
internal sealed class MedicalOperationClaims
{
	private readonly HashSet<ulong> _items = [];
	private readonly HashSet<ulong> _operators = [];

	/// <summary>True when the item instance is held by an open operation.</summary>
	internal bool IsItemReserved(ulong itemInstanceId) => _items.Contains(itemInstanceId);

	/// <summary>Claims the item instance; false when it was already claimed.</summary>
	internal bool TryReserveItem(ulong itemInstanceId) => _items.Add(itemInstanceId);

	/// <summary>Releases the item instance the operation held.</summary>
	internal void ReleaseItem(ulong itemInstanceId) => _items.Remove(itemInstanceId);

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
		_operators.Clear();
	}
}
