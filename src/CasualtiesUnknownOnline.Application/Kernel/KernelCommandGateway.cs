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
/// timing. Ownership here means "the item this member is reporting a destroy
/// for is a world item or the member's own carried item" — the same rule the
/// item domain applies to a drop; nothing about distance, state or sequence is
/// judged here, and the kernel still owns every domain verdict.
/// </para>
///
/// <para>
/// WHAT IS DELIBERATELY OUTSIDE IT: the two protocol heals in
/// <c>KernelProtocolCommandHandler</c> (a container-sync report, and an update
/// for an unknown carried id) materialize the reporter's own carried parent and
/// reach the kernel without passing here — they are the HOST's own
/// materialization of a member's report, not a member-authored fact, and the
/// missing-carried heal even stamps its own authority. A guest's range request is
/// answered in <c>KernelProtocolService</c> and never becomes a command. Those
/// paths are pre-existing and unchanged; the seam covers the mapped
/// member-authored command.
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
/// author: <see cref="AuthorityKind.HostOnly"/> facts are authored by the host's
/// own simulation and <see cref="AuthorityKind.PresentationOnly"/> is never a
/// kernel fact at all;</item>
/// <item>a destroy report must name an item this member may report destroyed
/// (a world item, or its own carried item) — moved verbatim out of the handler's
/// former <c>CanDestroy</c> check, which this change deleted;</item>
/// <item>an id this host never judged is NOT this seam's to answer, so it is
/// admitted here. That branch is defensive rather than the production path: for
/// the only kind this seam judges (a destroy report),
/// <c>KernelProtocolCommandHandler.TryRefuseUnjudgedOperation</c> runs above it
/// and already refuses an unjudged id with its own reason, so a member never
/// reaches this seam with one. The guard exists so the seam can never INVENT a
/// verdict for state it does not hold if the ordering above it ever changes.
/// </item>
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

		if (command is DestroyItemCommand destroy && !MayReportDestroyed(sender, destroy.InstanceId))
		{
			// The item id rides the detail: for this verdict the sender is NOT
			// answered, so this audit line is the only trace a dropped report
			// leaves, and an operator needs to know WHICH item it named.
			return Ignore(sender, command, RejectionReason.NotAuthorized,
				$"item {destroy.InstanceId} is neither a world item nor carried by the sender");
		}

		return CommandAdmission.Admit();
	}

	/// <summary>
	/// The moved entry-point rule: a member may report a destroy for a world item
	/// (it saw the item leave the world) or for its own carried item (it consumed
	/// it). A carried item owned by someone else is refused — that shape is the
	/// remote-clone display proxy reporting the owner's real instance id, and
	/// accepting it emptied a real owner's bag from a viewer's side
	/// (<c>ItemDestroyAuthorityTests</c> pins it).
	/// </summary>
	private bool MayReportDestroyed(ulong sender, ulong itemId)
	{
		var current = _items.FindItem(itemId);
		if (current is null)
		{
			// Defensive, not the production path: the creation-before-operation
			// invariant above this seam already refuses an id with no judged
			// creation, and this seam holds no state to answer it with.
			return true;
		}

		return current.Value.Location.Kind != ItemLocationKind.Carried
			|| current.Value.Location.Owner.Value == sender;
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
