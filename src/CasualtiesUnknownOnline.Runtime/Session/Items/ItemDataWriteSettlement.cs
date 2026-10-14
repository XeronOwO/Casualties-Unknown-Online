using System;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;
/// <summary>
/// The member's half of a console item-data write: what happens when the host says
/// that write did not happen.
///
/// <para>
/// A console write is the holder's own report — the client applies the value to its
/// own local object, then reports it (see <c>ItemDataCommands</c>) — so by the time
/// the host judges it, this side has already changed. A host refusal therefore
/// leaves a row the client must SETTLE, and the only honest value to settle it to is
/// the one the session records: this client's kernel row IS the host's own committed
/// fact replayed here (the guest's kernel never executes its own command, it waits
/// for the committed batch or the verdict), so writing that row's state back onto
/// the local object restores exactly the value the host still holds.
/// </para>
///
/// <para>
/// The settle is unconditional on the REASON — any refusal means the write did not
/// happen, whatever the host's grounds were — and it never guesses: an item this
/// client records no row for, or a local object the scene refuses to write, is
/// logged and reported as such rather than papered over, because a silent failure
/// here is a divergence a later reader could not attribute.
/// </para>
///
/// <para>
/// One consumer: the command console prints <see cref="Refused"/> where the operator
/// typed the command. The settle itself runs whether or not a console is open — the
/// divergence is a session fact, not a UI one. Public because the two services that
/// carry it (the item domain and the console) name it in their own constructors.
/// </para>
/// </summary>
public sealed class ItemDataWriteSettlement(
	IKernelItemFacts items,
	ILocalItemAccess localItems,
	ILogger<ItemDataWriteSettlement> log)
{
	private readonly IKernelItemFacts _items = items;
	private readonly ILocalItemAccess _localItems = localItems;
	private readonly ILogger<ItemDataWriteSettlement> _log = log;

	/// <summary>One refused console write arrived from the host and was settled. The console renders it; nothing else listens.</summary>
	public event Action<ItemDataWriteRefusal>? Refused;

	/// <summary>
	/// The host refused a console write this client reported: put the item's recorded
	/// state back on the local object and say what happened. Called by
	/// <see cref="ItemService.OnCommandRejected"/> for the write kind only — every
	/// other refused kind settles by its own family's rule.
	/// </summary>
	internal void Settle(ulong instanceId, RejectionReason reason)
	{
		var row = _items.FindItem(instanceId);
		if (row is null)
		{
			// The record is the value to settle to, so without a row there is nothing
			// to put back. It is reachable only if the row vanished between the write
			// and the verdict (a layer boundary, or a terminal row the record dropped).
			_log.LogWarning("[ItemWrite] the host refused a console write to #{InstanceId} ({Reason}) and this client records no row for it — the local value cannot be settled.",
				instanceId, reason);
			Refused?.Invoke(new ItemDataWriteRefusal(instanceId, "(unrecorded)", reason, LocalItemWriteResult.NotHeld));
			return;
		}

		var definitionId = row.Value.Identity.DefinitionId;
		var recorded = ItemKernelAuthority.ToCharacterItem(row.Value);
		var restore = _localItems.WriteLocalItem(instanceId, recorded);
		if (restore == LocalItemWriteResult.Applied)
		{
			_log.LogInformation("[ItemWrite] the host refused the console write to #{InstanceId} ({Definition}, {Reason}) — the recorded state is back on the local object (condition {Condition}).",
				instanceId, definitionId, reason, recorded.Condition);
		}
		else
		{
			_log.LogWarning("[ItemWrite] the host refused the console write to #{InstanceId} ({Definition}, {Reason}) and the local scene did not take the recorded state ({Result}) — this side may disagree with the host until the item is reported again.",
				instanceId, definitionId, reason, restore);
		}

		Refused?.Invoke(new ItemDataWriteRefusal(instanceId, definitionId, reason, restore));
	}
}
