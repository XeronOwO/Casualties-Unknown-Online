using System;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The GUEST's half of a host refusal: what this client does locally when the host
/// says a report it sent did not happen. It is one owner because the answer depends
/// on the REPORT the host judged (the kind the answer names), and the families have
/// different ways back to agreement:
///
/// <list type="bullet">
/// <item>a console write already applied its value here, so the recorded state is put
/// back (<see cref="ItemStateSettlement"/>);</item>
/// <item>a container report settles by re-stating the carried set — the absolute
/// report that carries the container AND its contents — never by the pickup rollback
/// below, which would unparent a live container into the world;</item>
/// <item>a container TAKE-OUT (an item released out of a container into this member's
/// hands) settles the same way, for the same reason one step smaller: the item is IN
/// the body, so the rollback would unparent it into the world;</item>
/// <item>a state report or a native use for an item the host never judged settles by
/// that same statement, because the statement is the creation claim for what this
/// member carries and the local copy is the only copy there is;</item>
/// <item>a refused native-use CLAIM (<see cref="RejectionReason.UseNotPermitted"/>)
/// puts the recorded state back, for the same reason a console write does: the use
/// already ran here before it was reported;</item>
/// <item>a refused LIMB RELOCATION (`ItemApplyLimb` / `ItemRemoveLimb`) is the applied
/// family's divergence (or a command the host does not hold) and settles by being NAMED:
/// the native call already ran, so the component and the item object stay where the game
/// left them, and the pickup rollback below would unparent a live item into the world for a
/// report that was never about a pickup;</item>
/// <item>every other reason keeps the family's oldest answer — surface the item
/// reject, which the adapter removes or rolls back (the refused-creation tombstone
/// above all: the local copy of a drop the host refused must be destroyed).</item>
/// </list>
///
/// <para>
/// Split out of <c>ItemService</c> at the aggregate-size gate, not to decorate it:
/// the routing is a policy table that changes whenever a report family gains a way
/// back, while the service owns the item tables and the tick; keeping the two in one
/// file made every settlement change look like a change to the item domain.
/// </para>
/// </summary>
internal sealed class ItemCommandRejectionRouter(
	ISessionControl session,
	ItemIdCoordinator idCoordinator,
	ItemStateSettlement settlement,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly ItemIdCoordinator _idCoordinator = idCoordinator;
	private readonly ItemStateSettlement _settlement = settlement;
	private readonly ILogger _log = log;

	/// <summary>The item reject the adapter rolls back on (pickup/block-drop family).</summary>
	internal event Action<ulong, ItemRejectMsg.Reason>? ItemRejected;

	internal void Handle(WireCommandKind kind, ulong itemId, RejectionReason reason)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		if (kind == WireCommandKind.ItemDataWrite)
		{
			// The console-write family settles its OWN refusals, and it has to: this
			// client applied that value to its local object before reporting it, so
			// the answer puts the session's recorded state back
			// (ItemStateSettlement). Falling through to the pickup/block-drop
			// mapping below would roll back the wrong thing — an item this client is
			// holding would be dropped as if a pickup had lost a race.
			_settlement.SettleConsoleWrite(itemId, reason);
			return;
		}

		if (kind == WireCommandKind.ItemContainerSync)
		{
			// A container report is never a pickup, so it never reaches the rollback below: that
			// rollback UNPARENTS the local item into the world (its own comment says so, for the
			// mid-drag and inside-a-container cases), and a container detached into the world while the
			// host still records it as this member's is the opposite of a settlement. Whatever reason
			// the host answered with — an unclaimed creation, a child it refuses to move, or a refused
			// creation's own tombstone — the way back is the carried-set statement, which carries the
			// container AND its contents.
			_log.LogInformation("Kernel command for container {ItemId} rejected ({Reason}) — container reports settle by re-stating the carried set, never by rolling the container back.", itemId, reason);
			_idCoordinator.RequestCarriedInventoryReport("the host refused a container report");
			return;
		}

		if (kind == WireCommandKind.ItemContainerTakeOut)
		{
			// A container take-out is never a pickup in the sense the rollback below means: the
			// item is IN this client's hands (it was released out of a container onto a slot or
			// onto a limb), so unparenting it into the world — what the pickup rollback does —
			// would be the opposite of a settlement, and the world reconcile kills an id its table
			// does not hold. The claim is about what this member CARRIES, so its way back is the
			// absolute carried-set statement, which carries the item AND its place: the refusal makes
			// the next statement due at once, and the statement is the creation claim when the host
			// never held the id at all.
			//
			// One reason is NOT settled that way: InvalidTransition means the record CONTRADICTS the
			// item's existence (it is terminal, or installed on a limb), and no carried-set statement
			// can revive a terminal row or take an item off a limb. That is the family's oldest
			// answer — the item reject — and it falls through to the rollback below.
			if (reason != RejectionReason.InvalidTransition)
			{
				_log.LogInformation("Kernel command for item {ItemId} rejected ({Reason}) — the container take-out is settled by re-stating the carried set, never by rolling the item out into the world.", itemId, reason);
				_idCoordinator.RequestCarriedInventoryReport("the host refused a container take-out");
				return;
			}
		}

		if (kind is WireCommandKind.ItemApplyLimb or WireCommandKind.ItemRemoveLimb)
		{
			// The applied family has exactly two doors, and a refusal of either is a DIVERGENCE
			// rather than a lost pickup race: the native limb action already ran on this side, so
			// the component and the item object are where the game left them, while the host's row
			// did not take the move (or never held the item at all). The pickup rollback below
			// would UNPARENT that live object into the world for a report that was never about a
			// pickup, and the world reconcile kills an id its table does not hold (batch
			// `20261018-u`'s row I4: a refused pickup from a take-off rolled the item out and the
			// copy died one keyframe later). Nothing is rolled back and nothing is re-stated: the
			// disagreement is named, which is the family's own rule.
			_log.LogWarning("Kernel command for item {ItemId} rejected ({Reason}) — the {Kind} relocation was refused: this side's native action already ran, so its object and the host's item row no longer agree (the applied family's divergence, or an item the host does not hold). Nothing was moved back; the local object stays where the native call left it.", itemId, reason, kind);
			return;
		}

		if (kind is WireCommandKind.ItemUpdateState or WireCommandKind.ItemUse && reason == RejectionReason.UnclaimedItem)
		{
			// A state/use report for an item the host never judged — refused because the
			// member's carried-set statement, which IS the creation claim for what it
			// carries, had already been judged without it. The way back is that same
			// statement, not the pickup rollback below: the local copy is the only copy
			// there is, so destroying it would delete the item the refusal asks to have
			// reported. Re-state the carried set now; its next report carries this
			// item with the state the use already produced, and the two sides agree.
			_log.LogInformation("Kernel command for item {ItemId} rejected ({Reason}) — the host has never judged it; re-stating the carried set.", itemId, reason);
			_idCoordinator.RequestCarriedInventoryReport("the host refused a use report for an item it has never judged");
			return;
		}

		if (kind == WireCommandKind.ItemUse && reason == RejectionReason.UseNotPermitted)
		{
			// The use CLAIM itself was refused — the item's own data says no native use
			// exists, so no client of this game could have performed it. The use already
			// ran on this side, so the session's recorded state is written back onto the
			// local object (ItemStateSettlement); the pickup rollback below would drop an
			// item this client is holding for a report that is not about a pickup.
			//
			// Scoped to THIS reason on purpose: the family's other answers keep their own
			// settlements — a refused creation's tombstone still reaches the drop-rollback
			// mapping below (the local copy of a drop the host refused must be destroyed),
			// and an unclaimed item settles by the carried-set statement above.
			_log.LogInformation("Kernel command for item {ItemId} rejected ({Reason}) — the use claim was refused; the recorded state is settled back onto the local object.", itemId, reason);
			_settlement.SettleUse(itemId, reason);
			return;
		}

		var mappedReason = reason switch
		{
			RejectionReason.BlockAlreadyBroken => ItemRejectMsg.Reason.BlockAlreadyBroken,
			RejectionReason.TrapDropUnaccounted => ItemRejectMsg.Reason.TrapDropUnaccounted,
			_ => ItemRejectMsg.Reason.UnknownItem,
		};
		_log.LogWarning("Kernel command for item {ItemId} rejected ({Reason}) — surfacing item reject.", itemId, reason);
		ItemRejected?.Invoke(itemId, mappedReason);
	}
}
