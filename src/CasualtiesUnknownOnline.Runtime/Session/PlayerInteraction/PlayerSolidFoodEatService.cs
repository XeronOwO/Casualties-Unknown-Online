using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The cross-player SOLID-FOOD eat's two halves on the host, split out of
/// <see cref="PlayerItemUseService"/> at the architecture line gate.
/// <para>
/// The family is the one the host cannot compute: a food's <c>useAction</c> is a
/// delegate that writes the eating body AND the item it is handed, so the eat
/// runs on the AFFECTED side's client, against its own body and its own object
/// of the offered item, and the item's post-eat state has to come back. That
/// makes the host's part two steps with one admission between them:
/// </para>
/// <list type="number">
/// <item><see cref="Admit"/> — the request was validated for this eater and this
/// item, so the one outcome report that follows is authorized (the item belongs
/// to somebody else, and the item domain refuses a member's report about another
/// member's carried item: this admission IS the authorization);</item>
/// <item><see cref="HandleOutcome"/> — the report arrives, the item's post-eat
/// state is committed onto its OWNER through <see cref="PlayerItemUseCommit"/>,
/// and the result the caller publishes carries it to that owner's own item.</item>
/// </list>
/// <para>
/// One admission, one report: an entry is consumed by the report it authorizes,
/// so a repeated or late report cannot roll an item's settled state back, and the
/// whole table dies with the session that issued it.
/// </para>
/// </summary>
internal sealed class PlayerSolidFoodEatService(
	PlayerCharacterAccess characters,
	ISolidFoodSemantics semantics,
	PlayerItemUseCommit commit,
	ILogger log) : ISessionReset
{
	private readonly PlayerCharacterAccess _characters = characters;
	private readonly ISolidFoodSemantics _semantics = semantics;
	private readonly PlayerItemUseCommit _commit = commit;
	private readonly ILogger _log = log;

	/// <summary>The admitted eats whose outcome is still owed (one report per admission).</summary>
	private readonly SolidFoodEatGrants _eats = new();

	/// <summary>The host admitted a cross-player eat of this item by this eater — remember who owns it.</summary>
	internal void Admit(ulong eater, ulong itemId, ulong owner) => _eats.Grant(eater, itemId, owner);

	/// <summary>Session ended: an admission belongs to the session that issued it.</summary>
	public void ResetSessionState()
	{
		if (_eats.Count > 0)
		{
			_log.LogInformation("[ItemUse] session ended with {Count} admitted solid-food eat(s) unsettled; grants dropped.", _eats.Count);
		}

		_eats.Reset();
	}

	/// <summary>
	/// Host only: the affected side ran a cross-player eat. Commits what the item
	/// became onto its OWNER and answers the use result the caller must publish, or
	/// null when the report is refused — no admitted eat for this pair, the owner no
	/// longer carries the item, or the item is no longer a solid food on this host.
	/// Every refusal is logged with the reason.
	/// </summary>
	internal PlayerItemUseResultMsg? HandleOutcome(ulong sender, PlayerItemEatOutcomeMsg msg)
	{
		if (msg.ItemInstanceId == 0 || !_eats.TryTake(sender, msg.ItemInstanceId, out var owner))
		{
			_log.LogWarning("[ItemUse] refused the eat outcome from {Eater} for item {ItemId}: no admitted cross-player eat of that item is in flight.",
				sender, msg.ItemInstanceId);
			return null;
		}

		if (_characters.GetCharacterData(owner) is not { } ownerData
			|| !CarriedItemUseTree.TryFind(ownerData.Items, msg.ItemInstanceId, out var originalItem))
		{
			_log.LogWarning("[ItemUse] the eat outcome from {Eater} names item {ItemId}, which {Owner} no longer carries — nothing committed.",
				sender, msg.ItemInstanceId, owner);
			return null;
		}

		var verdict = SolidFoodAdmission.Classify(_semantics, originalItem.ItemId);
		if (verdict == SolidFoodVerdict.NotSolidFood)
		{
			_log.LogWarning("[ItemUse] refused the eat outcome from {Eater}: {ItemId} (id {InstanceId}) is not a solid food on this host.",
				sender, originalItem.ItemId, msg.ItemInstanceId);
			return null;
		}

		// What the use did to the item OBJECT is the item's own known shape, not
		// something the report could say: the delegate that destroys the item has
		// already destroyed the eater's copy by the time a report could carry it, and
		// the delegate that hands over a replacement was refused at admission.
		var destroyed = verdict == SolidFoodVerdict.EatsAndDestroys;
		var newItem = PlayerCharacterAccess.CloneItem(originalItem);
		newItem.Condition = msg.Condition;

		_commit.CommitItemAfterUse(owner, originalItem.InstanceId, newItem, destroyed, movedToTheTarget: false);
		_log.LogInformation("[ItemUse] {Eater} ate {ItemId} (id {InstanceId}) of {Owner}; condition {Condition:F2}{Consumed}.",
			sender, originalItem.ItemId, originalItem.InstanceId, owner, msg.Condition, destroyed ? ", consumed by its own use action" : "");

		return new PlayerItemUseResultMsg
		{
			UserSteamId = owner,
			TargetSteamId = sender,
			ItemInstanceId = originalItem.InstanceId,
			ItemDestroyed = destroyed,
			ItemAfter = destroyed ? null : PlayerCharacterAccess.CloneItem(newItem),
		};
	}
}
