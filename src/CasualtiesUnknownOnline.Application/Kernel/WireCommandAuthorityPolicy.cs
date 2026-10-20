using System;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The authority a MEMBER's submission of one wire command kind declares — the
/// declaration the admission seam judges (<see cref="KernelCommandGateway"/>).
///
/// <para>
/// Before this policy the mapper stamped every mapped kind
/// <see cref="AuthorityKind.OwnerPredictedHostValidated"/>, so the declaration
/// carried no member-versus-host information at all: a member's <c>RunStart</c>
/// read exactly like a member's item use, and the seam's authority half was a
/// no-op (the inventory in
/// <c>docs/backlog/future/strict-validation-anti-cheat.md</c>).
/// </para>
///
/// <para>
/// The table answers ONE question per kind: may a member author it, and if so
/// which policy does that member's own side run under? The member-authored set is
/// not a guess — it is the set of kinds a sender in this tree actually emits, and
/// each entry names that sender. Every other mapped kind is authored by the
/// host's own simulation, so a member's submission of one is a fact the sender
/// cannot have observed, and the seam refuses it by name. The two wire-control
/// kinds (<c>RangeRequest</c>, <c>CommandRejected</c>) are not commands at all and
/// are deliberately NOT classified: <see cref="Declared"/> throws for them, and
/// for any kind a later change adds without answering this question.
/// </para>
///
/// <para>
/// The declaration is read on the HOST, when it decodes a member's submission
/// (<c>KernelWireMapper.FromWireCommand</c>); the host's own submissions never
/// pass through the mapper, so a <see cref="AuthorityKind.HostOnly"/> entry here
/// takes nothing away from the host's own commit path.
/// </para>
/// </summary>
public static class WireCommandAuthorityPolicy
{
	/// <summary>
	/// The authority a member's submission of <paramref name="kind"/> declares.
	/// Throws for a kind that is not a mapped command: an unclassified kind must
	/// fail loudly rather than inherit a policy nobody chose for it.
	/// </summary>
	public static AuthorityKind Declared(WireCommandKind kind) => kind switch
	{
		// ===== Member-authored, owner-predicted: the member's own client acts
		// first and the host validates the report against its record.
		//
		// The sender is the member that owns the item's current state or gesture:
		// ItemActionSync (a use, a slot move, a carried container's own content
		// move), ItemMessageFlowService (a pickup, a drop, a destroy, a container
		// take-out, a limb-item relocation).
		WireCommandKind.ItemPickup => AuthorityKind.OwnerPredictedHostValidated,
		// A container take-out: the member took one item out of a container into its own
		// hands, which no other client can observe (the container's own report states what
		// the container HOLDS, never where a child went). Same kernel fact as the pickup,
		// same authority — the sender's own report of a native gesture it already ran.
		WireCommandKind.ItemContainerTakeOut => AuthorityKind.OwnerPredictedHostValidated,
		WireCommandKind.ItemDrop => AuthorityKind.OwnerPredictedHostValidated,
		WireCommandKind.ItemDestroy => AuthorityKind.OwnerPredictedHostValidated,
		WireCommandKind.ItemUpdateState => AuthorityKind.OwnerPredictedHostValidated,
		// A native use: the member's own client ran the item's own use action and
		// reports the state it produced. It carries its own kind so the seam can
		// judge it by the item's own data — a use kind, the console write and the
		// plain state report are three different claims about one row.
		WireCommandKind.ItemUse => AuthorityKind.OwnerPredictedHostValidated,
		WireCommandKind.ItemContainerSync => AuthorityKind.OwnerPredictedHostValidated,
		WireCommandKind.ItemApplyLimb => AuthorityKind.OwnerPredictedHostValidated,
		WireCommandKind.ItemRemoveLimb => AuthorityKind.OwnerPredictedHostValidated,

		// A console write: the member's own client applies the value locally and
		// reports the resulting state, exactly as a native use does (the sender is
		// ItemActionSync's console-write report), so its authority is the same one.
		// What the authority does NOT express is the host's own RULE for the verb:
		// that is a per-session decision rather than a property of the kind
		// (<c>IMemberCommandRules</c>, read by <see cref="KernelCommandGateway"/>),
		// because the same report is a refusal on a host whose rule is off and an
		// ordinary state fact on one whose rule is on.
		WireCommandKind.ItemDataWrite => AuthorityKind.OwnerPredictedHostValidated,

		// ===== Member-authored, trigger observed: the native effect already ran on
		// the reporting member's own client — it has no way to intercept it first —
		// and the host commits the resulting fact.
		//
		// ItemSpawn is what the item domain itself stamps for the two spawn entry
		// points (ItemKernelAuthority.TrySpawn / TrySpawnCarried); a guest's
		// creation report (ItemMessageFlowService.SendItemSpawned) is the same
		// fact. The three enemy-combat kinds are replayed and reported by the
		// AFFECTED member's own client (EnemyCombatReplay, EnemyProximitySync ->
		// EnemyCombatKernelSubmitter), which is the side that runs the native
		// bite/lunge/effect on the body it simulates.
		WireCommandKind.ItemSpawn => AuthorityKind.TriggerObservedHostCommitted,
		WireCommandKind.RecordEnemyBite => AuthorityKind.TriggerObservedHostCommitted,
		WireCommandKind.RecordEnemyLunge => AuthorityKind.TriggerObservedHostCommitted,
		WireCommandKind.RecordEnemyEffect => AuthorityKind.TriggerObservedHostCommitted,

		// ===== Host-only: the host's own simulation authors these and no member
		// sender emits them, so a member's submission of one is refused.
		//
		// RunStart / AdvanceLayer: the run baseline is the host's alone
		// (KernelDomainCommands.TryStartRun / TryAdvanceLayer; matrix R1).
		// RecordTrapConsumed / RecordBuildingEntityHealth / RecordOpenedEntity /
		// RecordTrapState: the world-entity facts are host kernel commands, and a
		// guest reports the underlying game event on its own message instead
		// (matrix E7).
		// UpdatePlayerStatus: the players table is host-written (matrix P2).
		// SetPlayerCarry / ClearPlayerCarry: one authoritative carry write, made
		// from the carry/stop request and the host's UI (matrix P6).
		// UpsertEnemy / RemoveEnemy: the host's enemy table (matrix N2).
		// UpdateFluidRegion: the host simulates the fluid grid alone (matrix F4).
		// ItemTransfer: the host commits the transfer fact of an accepted take
		// request (matrix P7) — no member path sends this kind
		// (GuestCommandReconciliation states the same for its report window). The
		// host's own commit of that fact stamps the item domain's owner-predicted
		// value; this entry is about a MEMBER's submission, which is refused.
		WireCommandKind.RunStart => AuthorityKind.HostOnly,
		WireCommandKind.AdvanceLayer => AuthorityKind.HostOnly,
		WireCommandKind.RecordTrapConsumed => AuthorityKind.HostOnly,
		WireCommandKind.RecordBuildingEntityHealth => AuthorityKind.HostOnly,
		WireCommandKind.RecordOpenedEntity => AuthorityKind.HostOnly,
		WireCommandKind.RecordTrapState => AuthorityKind.HostOnly,
		WireCommandKind.UpdatePlayerStatus => AuthorityKind.HostOnly,
		WireCommandKind.SetPlayerCarry => AuthorityKind.HostOnly,
		WireCommandKind.ClearPlayerCarry => AuthorityKind.HostOnly,
		WireCommandKind.UpsertEnemy => AuthorityKind.HostOnly,
		WireCommandKind.RemoveEnemy => AuthorityKind.HostOnly,
		WireCommandKind.UpdateFluidRegion => AuthorityKind.HostOnly,
		WireCommandKind.ItemTransfer => AuthorityKind.HostOnly,
		_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "wire command kind has no declared member authority"),
	};
}
