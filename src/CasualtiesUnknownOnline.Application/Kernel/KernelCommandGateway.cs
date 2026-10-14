using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The admission seam for a command a MEMBER submitted over the wire: one owner
/// for "who may submit this" on the mapped kernel-command path. It sits in the
/// Application layer because eligibility is a session-level decision, not a
/// kernel-domain one.
///
/// <para>
/// SCOPE — the 2026-09-18 ruling binds it: the gateway decides ELIGIBILITY only
/// (who may submit), never what happens to another player's body, reach or
/// timing. Ownership here means "the item this member is reporting on is one it
/// may report on at all" — a world item (it saw the item in the world, or used
/// it in place) or the member's own carried, installed or consumed item — the
/// same rule the item domain applies to a drop; nothing about distance, state or
/// sequence is judged here, and the kernel still owns every domain verdict.
/// </para>
///
/// <para>
/// WHAT IS OUTSIDE IT: a guest's range request is answered in
/// <c>KernelProtocolService</c> and never becomes a command. The container-sync
/// report DOES reach this seam like any other member command — for a parent this
/// host already judged it is a member-authored fact, and the kernel's
/// <c>DecideSyncContainer</c> rewrites that parent's data, moves the listed
/// children in and destroys the parent's descendants the report left out — so it
/// has its own rule here. The one arm that stays the host's own materialization is
/// the MISSING-parent heal: nothing names that parent yet, so the domain creates
/// it as the reporter's own carried item, accept-first, exactly as before. The
/// handler's other heal (an update for an unknown carried id) does not pass here
/// either, and it names nothing this seam could judge.
/// </para>
///
/// <para>
/// ORDER is part of the contract, because a refusal that runs earlier can mask
/// one the kernel would have produced:
/// <list type="number">
/// <item>the command's actor must be the submitting member — the frame validator
/// already guarantees it for a direct peer, and this seam keeps the binding
/// local instead of three files away;</item>
/// <item>the declared <see cref="AuthorityKind"/> must be one a member may
/// author: a <see cref="AuthorityKind.HostOnly"/> fact is authored by the host's
/// own simulation and <see cref="AuthorityKind.PresentationOnly"/> is never a
/// kernel fact at all. WHICH kind declares what is
/// <see cref="WireCommandAuthorityPolicy"/>'s table, filled from the senders this
/// tree actually has — so a member's <c>RunStart</c> is refused here while its
/// item use is not;</item>
/// <item>the item a report NAMES must be one this member may speak for — the
/// shared rule (<see cref="MayReportOn"/>) behind a destroy report, a state
/// report and a container sync's parent: world content (the item lies on the
/// ground, or inside a container that ultimately does), the member's own carried,
/// installed or contained item, or an id this host never judged. The destroy half
/// is the handler's former <c>CanDestroy</c> check, moved here;</item>
/// <item>a container sync's listed CHILDREN must not be items the record puts in
/// another member's hands or on another member's limb
/// (<see cref="MayEnterOwnContainer"/>): the parent rule alone would still let a
/// report pull a peer's carried item into the reporter's own container;</item>
/// <item>the applied family's two reports must name the member's own item and the
/// member's own limb — a report that installs somebody else's item, or installs
/// on somebody else's limb, is the same remote-clone shape;</item>
/// <item>an id this host never judged is NOT this seam's to answer, so it is
/// admitted here. That branch is defensive rather than the production path: for
/// every item rule below, <c>KernelProtocolCommandHandler.
/// TryRefuseUnjudgedOperation</c> runs above this seam and already refuses an
/// unjudged id with its own reason, so a member never reaches this seam with one.
/// The guard exists so the seam can never INVENT a verdict for state it does not
/// hold if the ordering above it ever changes. A container sync's MISSING parent
/// is the same shape and is admitted for the same reason: the domain's own
/// accept-first heal is what creates it.</item>
/// </list>
/// </para>
/// </summary>
public sealed class KernelCommandGateway(IKernelItemFacts items, ILogger<KernelCommandGateway> log)
{
	private readonly IKernelItemFacts _items = items;
	private readonly ILogger<KernelCommandGateway> _log = log;

	/// <summary>
	/// Judges whether a member's submission may reach the kernel, and audits
	/// every refusal with one uniform line naming the command, the item it names
	/// (when it names one), the actor, the sender and the reason.
	/// </summary>
	public CommandAdmission AdmitMemberSubmission(ulong sender, GameCommand command)
	{
		if (command.Actor.Value != sender)
		{
			return Refuse(sender, command, RejectionReason.NotAuthorized,
				"the command names an actor other than the submitting member");
		}

		switch (command.Authority)
		{
			case AuthorityKind.HostOnly:
				return Refuse(sender, command, RejectionReason.NotAuthorized,
					"a host-only fact is authored by the host's own simulation, never by a member");
			case AuthorityKind.PresentationOnly:
				return Refuse(sender, command, RejectionReason.NotAuthorized,
					"a presentation-only command is never a kernel fact");
		}

		if (command is DestroyItemCommand destroy && !MayReportOn(sender, destroy.InstanceId))
		{
			// The item id rides the detail: for this verdict the sender is NOT
			// answered, so this audit line is the only trace a dropped report
			// leaves, and an operator needs to know WHICH item it named.
			return Ignore(sender, command, RejectionReason.NotAuthorized,
				$"item {destroy.InstanceId} is neither world content nor the sender's own (carried, installed or contained)");
		}

		if (command is UpdateItemStateCommand state && !MayReportOn(sender, state.InstanceId))
		{
			// Dropped in silence for the reason the destroy rule gives above: this shape
			// is the remote-clone display proxy, and answering it would start a guest
			// rollback on a copy that is not the item's fact.
			return Ignore(sender, command, RejectionReason.NotAuthorized,
				$"item {state.InstanceId} is another member's (carried, installed or contained), so its state is not the sender's to report");
		}

		if (command is SyncContainerItemsCommand sync && ContainerSyncRefusal(sender, sync) is { } containerDetail)
		{
			return Ignore(sender, command, RejectionReason.NotAuthorized, containerDetail);
		}

		if (command is ApplyLimbItemCommand apply && !MayInstallOnOwnLimb(sender, apply))
		{
			return Ignore(sender, command, RejectionReason.NotAuthorized,
				$"item {apply.InstanceId} is not carried by the sender, or the report installs it on somebody else's limb {apply.LimbIndex}");
		}

		if (command is RemoveLimbItemCommand remove && !MayTakeOffOwnLimb(sender, remove))
		{
			return Ignore(sender, command, RejectionReason.NotAuthorized,
				$"item {remove.InstanceId} is not applied to limb {remove.LimbIndex} of the sender, or the removal hands it to somebody else");
		}

		return CommandAdmission.Admit();
	}

	/// <summary>
	/// The item a member's report NAMES is one the record lets that member speak
	/// for. Three report families ask the same question — a destroy, a state update
	/// and a container sync's parent — so they share this one answer:
	/// <list type="bullet">
	/// <item>a WORLD item, and any item whose container chain is world-rooted: every
	/// peer may pick it up, use it in place or see it leave, so the reporter claims
	/// nobody's possession;</item>
	/// <item>the member's OWN carried, installed or contained item — an item's
	/// recorded state and its end are its holder's own report, which is the
	/// pre-release trust model this seam does not question;</item>
	/// <item>an id this host never judged: nothing here to judge, and the handler's
	/// own rules above answer it (the creation-before-operation invariant, or the
	/// accept-first heals);</item>
	/// <item>a terminal row: its end is already recorded, and the domain answers a
	/// report about it with its own reason.</item>
	/// </list>
	/// What is left — an item the record puts in another member's hands, on another
	/// member's limb or in another member's container — is the remote-clone display
	/// proxy's shape: admitting it rewrote a real owner's row, and for a destroy it
	/// emptied a real owner's bag from a viewer's side
	/// (<c>ItemDestroyAuthorityTests</c> pins that half).
	/// <para>
	/// The chain walk is the kernel's own (<see cref="ItemLocationChain"/>), the same
	/// one the layer reset and the save archive read, so "in the world" means one
	/// thing in this tree: a container that changed hands leaves its old contents
	/// recorded under the previous holder, and only the walk can tell those apart
	/// from an item somebody is really holding.
	/// </para>
	/// </summary>
	private bool MayReportOn(ulong sender, ulong itemId)
	{
		var current = _items.FindItem(itemId);
		if (current is null)
		{
			// Defensive, not the production path: the creation-before-operation
			// invariant above this seam already refuses an id with no judged
			// creation, and this seam holds no state to answer it with.
			return true;
		}

		var row = current.Value;
		return row.Location.Kind switch
		{
			ItemLocationKind.World or ItemLocationKind.Terminal => true,
			ItemLocationKind.Contained => row.Location.Owner.Value == sender
				|| ItemLocationChain.IsWorldRooted(row, _items.FindItem),
			_ => row.Location.Owner.Value == sender,
		};
	}

	/// <summary>
	/// A container sync's CHILDREN: the report claims each named item is inside the
	/// container it sends, and the domain moves it there (creating it when the host
	/// has never judged it). The one thing a child may not be is an item the record
	/// puts in another member's hands or on another member's limb — the same
	/// remote-clone shape, and without this rule a report could pull a peer's
	/// carried item into the reporter's own container. A row that is merely
	/// CONTAINED elsewhere is admitted on purpose: that is the container-changed-
	/// hands case above, and refusing it would refuse ordinary play.
	/// </summary>
	private bool MayEnterOwnContainer(ulong sender, ulong itemId)
	{
		var current = _items.FindItem(itemId);
		if (current is null)
		{
			return true;
		}

		return current.Value.Location.Kind switch
		{
			ItemLocationKind.Carried or ItemLocationKind.Applied => current.Value.Location.Owner.Value == sender,
			_ => true,
		};
	}

	/// <summary>
	/// The container sync's report detail when it is not this member's to send: the
	/// parent first (the whole subtree hangs off it), then the first child the record
	/// puts in another member's hands. <c>null</c> means admitted.
	/// </summary>
	private string? ContainerSyncRefusal(ulong sender, SyncContainerItemsCommand command)
	{
		if (!MayReportOn(sender, command.ParentIdentity.InstanceId))
		{
			return $"container {command.ParentIdentity.InstanceId} is another member's (carried, installed or contained), so its contents are not the sender's to rewrite";
		}

		foreach (var child in command.Children)
		{
			if (!MayEnterOwnContainer(sender, child.InstanceId))
			{
				return $"child {child.InstanceId} is another member's (carried or installed), so it cannot be moved into the sender's container";
			}
		}

		return null;
	}

	/// <summary>
	/// The applied family's own entry rule: a member may report that an item IT
	/// carries became the installed form of one of ITS OWN limbs. Both halves are
	/// checked against the item's recorded location rather than trusted from the
	/// command, because the gesture itself cannot carry either fact — the applier
	/// acts on the body it simulates — so a report that names somebody else's item
	/// or somebody else's limb is the remote-clone shape this seam exists to drop.
	/// A repeated observation of the same applied fact is admitted: it is a
	/// re-assertion, and the domain advances the revision for it.
	/// </summary>
	private bool MayInstallOnOwnLimb(ulong sender, ApplyLimbItemCommand command)
	{
		if (command.Owner.Value != sender)
		{
			return false;
		}

		var current = _items.FindItem(command.InstanceId);
		if (current is null)
		{
			// Defensive, as for the destroy rule above: the creation-before-operation
			// invariant answers an unjudged id before this seam sees it.
			return true;
		}

		return current.Value.Location.Kind switch
		{
			ItemLocationKind.Carried => current.Value.Location.Owner.Value == sender,
			ItemLocationKind.Applied => current.Value.Location.Owner.Value == sender && current.Value.Location.LimbIndex == command.LimbIndex,
			_ => false,
		};
	}

	/// <summary>
	/// The counterpart's rule: the limb is the SENDER's own, the destination is the
	/// sender (the component came off this client's own body and into its own
	/// hands), and the item really is installed on that limb. A removal that hands
	/// the item to a third party is a cross-player removal, and that one is the
	/// HOST's commit — it never travels as a member's report.
	/// </summary>
	private bool MayTakeOffOwnLimb(ulong sender, RemoveLimbItemCommand command)
	{
		if (command.AppliedOwner.Value != sender || command.NewOwner.Value != sender)
		{
			return false;
		}

		var current = _items.FindItem(command.InstanceId);
		if (current is null)
		{
			return true;
		}

		return current.Value.Location.Kind switch
		{
			ItemLocationKind.Applied => current.Value.Location.Owner.Value == sender
				&& current.Value.Location.LimbIndex == command.LimbIndex,
			// The already-taken-off shape, admitted on purpose: this seam decides ELIGIBILITY, and
			// the item is still the sender's own, so a RE-SENT report (the reconciliation window's
			// repeat of one operation) must reach the kernel and be answered from its operation
			// window instead of being dropped here. A plain carried item the sender really does
			// carry is refused by the domain by name, which is where that verdict belongs.
			ItemLocationKind.Carried => current.Value.Location.Owner.Value == sender,
			_ => false,
		};
	}

	private CommandAdmission Refuse(ulong sender, GameCommand command, RejectionReason reason, string detail)
	{
		Audit(sender, command, reason, detail, answered: true);
		return CommandAdmission.Refuse(reason);
	}

	private CommandAdmission Ignore(ulong sender, GameCommand command, RejectionReason reason, string detail)
	{
		Audit(sender, command, reason, detail, answered: false);
		return CommandAdmission.Ignore(reason);
	}

	private void Audit(ulong sender, GameCommand command, RejectionReason reason, string detail, bool answered) =>
		_log.LogWarning("Command admission refused ({Answer}): {Command} actor {Actor} sender {Sender} — {Reason}: {Detail}.",
			answered ? "answered" : "no answer, as before",
			command.GetType().Name,
			command.Actor.Value,
			sender,
			reason,
			detail);
}
