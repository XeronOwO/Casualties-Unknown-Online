using System;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The member's half of a report the host refuses AFTER this client has already
/// applied the claim to its own object: the console item-data write and the native
/// use both change the local item first and report afterwards, so a host refusal
/// must settle that object back or the two pictures stay apart.
///
/// <para>
/// The only honest value to settle it to is the one the session records: this
/// client's kernel row IS the host's own committed fact replayed here (the guest's
/// kernel never executes its own command, it waits for the committed batch or the
/// verdict), so writing that row's state back onto the local object restores exactly
/// the value the host still holds. That is why one settlement serves both families —
/// they differ in who reads the outcome (the console prints it; a use is silent), not
/// in what has to be put back.
/// </para>
///
/// <para>
/// The settle is unconditional on the REASON — any refusal means the report did not
/// happen, whatever the host's grounds were — and it never guesses: an item this
/// client records no row for, or a local object the scene refuses to write, is
/// logged and reported as such rather than papered over, because a silent failure
/// here is a divergence a later reader could not attribute. A use that consumed the
/// item locally is exactly that shape: the refusal cannot bring the object back, and
/// the warning says so.
/// </para>
///
/// <para>
/// One consumer reads the console half: the command console prints
/// <see cref="Refused"/> where the operator typed the command. The settle itself
/// runs whether or not a console is open — the divergence is a session fact, not a
/// UI one. A refused USE does not raise it: the operator never typed anything, and
/// its settlement is the silent state restore. Public because the two services that
/// carry it (the item domain and the console) name it in their own constructors.
/// </para>
/// </summary>
public sealed class ItemStateSettlement(
	IKernelItemFacts items,
	ILocalItemAccess localItems,
	IItemStateLanguage language,
	ILogger<ItemStateSettlement> log)
{
	private readonly IKernelItemFacts _items = items;
	private readonly ILocalItemAccess _localItems = localItems;
	private readonly IItemStateLanguage _language = language;
	private readonly ILogger<ItemStateSettlement> _log = log;

	/// <summary>One refused console WRITE arrived from the host and was settled. The console renders it; nothing else listens.</summary>
	public event Action<ItemDataWriteRefusal>? Refused;

	/// <summary>
	/// The host refused a console write this client reported: put the item's recorded
	/// state back on the local object and say what happened. Called by
	/// <see cref="ItemService.OnCommandRejected"/> for the write kind only — every
	/// other refused kind settles by its own family's rule.
	/// </summary>
	internal void SettleConsoleWrite(ulong instanceId, RejectionReason reason) =>
		Settle(instanceId, reason, origin: "console write", announce: true);

	/// <summary>
	/// The host refused a NATIVE-USE report: the use ran here before it was reported,
	/// so the recorded state is written back onto the local object. Nothing is
	/// printed — no operator asked for this — but the divergence is logged, and an
	/// item the local scene no longer holds (a use that consumed it) is named as the
	/// one shape this settlement cannot repair.
	/// </summary>
	internal void SettleUse(ulong instanceId, RejectionReason reason) =>
		Settle(instanceId, reason, origin: "use", announce: false);

	private void Settle(ulong instanceId, RejectionReason reason, string origin, bool announce)
	{
		var row = _items.FindItem(instanceId);
		if (row is null)
		{
			// The record is the value to settle to, so without a row there is nothing
			// to put back. It is reachable only if the row vanished between the report
			// and the verdict (a layer boundary, or a terminal row the record dropped).
			_log.LogWarning("[ItemState] the host refused a {Origin} to #{InstanceId} ({Reason}) and this client records no row for it — the local value cannot be settled.",
				origin, instanceId, reason);
			Announce(instanceId, "(unrecorded)", reason, LocalItemWriteResult.NotHeld, announce);
			return;
		}

		var definitionId = row.Value.Identity.DefinitionId;
		if (!_language.TryDescribe(row.Value.Data, out var recorded))
		{
			// No Game Adapter: the record's own shape cannot be named, so there is no
			// document to write back. The divergence is logged and announced, not hidden.
			_log.LogWarning("[ItemState] the host refused the {Origin} to #{InstanceId} ({Definition}, {Reason}) and this build cannot express an item state — the local value cannot be settled.",
				origin, instanceId, definitionId, reason);
			Announce(instanceId, definitionId, reason, LocalItemWriteResult.Unavailable, announce);
			return;
		}

		var restore = _localItems.WriteLocalItem(instanceId, recorded);
		if (restore.Applied)
		{
			_log.LogInformation("[ItemState] the host refused the {Origin} to #{InstanceId} ({Definition}, {Reason}) — the recorded state is back on the local object (condition {Condition}).",
				origin, instanceId, definitionId, reason, restore.Written?.Condition);
		}
		else
		{
			_log.LogWarning("[ItemState] the host refused the {Origin} to #{InstanceId} ({Definition}, {Reason}) and the local scene did not take the recorded state ({Result}: {Refusal}) — this side may disagree with the host until the item is reported again.",
				origin, instanceId, definitionId, reason, restore.Result, restore.Refusal);
		}

		Announce(instanceId, definitionId, reason, restore.Result, announce);
	}

	private void Announce(ulong instanceId, string definitionId, RejectionReason reason, LocalItemWriteResult restore, bool announce)
	{
		if (!announce)
		{
			return;
		}

		Refused?.Invoke(new ItemDataWriteRefusal(instanceId, definitionId, reason, restore));
	}
}
