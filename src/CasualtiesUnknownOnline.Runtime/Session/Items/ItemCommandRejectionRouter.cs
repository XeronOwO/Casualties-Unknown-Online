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
/// <item>a state report or a native use for an item the host never judged settles by
/// that same statement, because the statement is the creation claim for what this
/// member carries and the local copy is the only copy there is;</item>
/// <item>a refused native-use CLAIM (<see cref="RejectionReason.UseNotPermitted"/>)
/// puts the recorded state back, for the same reason a console write does: the use
/// already ran here before it was reported;</item>
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
			_ => ItemRejectMsg.Reason.UnknownItem,
		};
		_log.LogWarning("Kernel command for item {ItemId} rejected ({Reason}) — surfacing item reject.", itemId, reason);
		ItemRejected?.Invoke(itemId, mappedReason);
	}
}
