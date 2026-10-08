using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The two halves, on the host, of every cross-player use whose effect runs on the
/// AFFECTED side's own client — the SOLID-FOOD eat and the LIMB-TOOL use. Split
/// out of <see cref="PlayerItemUseService"/> at the architecture line gate, and
/// shared by both families because the host's part is the same shape for each:
/// </summary>
/// <list type="number">
/// <item><see cref="Admit"/> — the request was validated for this actor and this
/// item, so the one outcome report that follows is authorized (the item belongs to
/// somebody else, and the item domain refuses a member's report about another
/// member's carried item: this admission IS the authorization);</item>
/// <item><see cref="HandleOutcome"/> — the report arrives, the item's post-use
/// state is committed onto its OWNER through <see cref="PlayerItemUseCommit"/>, and
/// the result the caller publishes carries it to that owner's own item.</item>
/// </list>
/// <para>
/// One admission, one report: an entry is consumed by the report it authorizes, so
/// a repeated or late report cannot roll an item's settled state back, and the
/// whole table dies with the session that issued it.
/// </para>
/// <para>
/// Which family a report belongs to is the ITEM's own data on this host, asked in
/// the family chain's order: the solid-food verdict first (it is asked first there
/// too), then the limb-tool predicate. The one thing a report decides for itself is
/// the limb-tool family's consumption — the delegate that turns an item into a limb
/// component destroys the object, and only the side that ran it can see that — while
/// the eat's consumption stays the verdict the host read BEFORE admitting the eat,
/// because that report is issued from inside the game's own use call.
/// </para>
/// </summary>
internal sealed class PlayerItemActionOutcomeService(
	PlayerCharacterAccess characters,
	ILimbUseSemantics limbSemantics,
	ISolidFoodSemantics solidFoodSemantics,
	PlayerItemUseCommit commit,
	ILogger log) : ISessionReset
{
	private readonly PlayerCharacterAccess _characters = characters;
	private readonly ILimbUseSemantics _limbSemantics = limbSemantics;
	private readonly ISolidFoodSemantics _solidFoodSemantics = solidFoodSemantics;
	private readonly PlayerItemUseCommit _commit = commit;
	private readonly ILogger _log = log;

	/// <summary>The admitted uses whose outcome is still owed (one report per admission).</summary>
	private readonly ItemActionGrants _uses = new();

	/// <summary>The host admitted a cross-player use of this item by this actor — remember who owns it.</summary>
	internal void Admit(ulong actor, ulong itemId, ulong owner) => _uses.Grant(actor, itemId, owner);

	/// <summary>Session ended: an admission belongs to the session that issued it.</summary>
	public void ResetSessionState()
	{
		if (_uses.Count > 0)
		{
			_log.LogInformation("[ItemUse] session ended with {Count} admitted affected-side use(s) unsettled; grants dropped.", _uses.Count);
		}

		_uses.Reset();
	}

	/// <summary>
	/// Host only: the affected side ran a cross-player use. Commits what the item
	/// became onto its OWNER and answers the use result the caller must publish, or
	/// null when the report is refused — no admitted use for this pair, the owner no
	/// longer carries the item, or the item is in neither reporting family on this
	/// host. Every refusal is logged with the reason.
	/// </summary>
	internal PlayerItemUseResultMsg? HandleOutcome(ulong sender, PlayerItemActionOutcomeMsg msg)
	{
		if (msg.ItemInstanceId == 0 || !_uses.TryTake(sender, msg.ItemInstanceId, out var owner))
		{
			_log.LogWarning("[ItemUse] refused the affected-side outcome from {Actor} for item {ItemId}: no admitted cross-player use of that item is in flight.",
				sender, msg.ItemInstanceId);
			return null;
		}

		if (_characters.GetCharacterData(owner) is not { } ownerData
			|| !CarriedItemUseTree.TryFind(ownerData.Items, msg.ItemInstanceId, out var originalItem))
		{
			_log.LogWarning("[ItemUse] the affected-side outcome from {Actor} names item {ItemId}, which {Owner} no longer carries — nothing committed.",
				sender, msg.ItemInstanceId, owner);
			return null;
		}

		var verdict = SolidFoodAdmission.Classify(_solidFoodSemantics, originalItem.ItemId);
		bool destroyed;
		string family;
		if (verdict != SolidFoodVerdict.NotSolidFood)
		{
			// What the use did to the item OBJECT is the item's own known shape for this
			// family, not something the report could say: the delegate that destroys the
			// item has already destroyed the eater's copy by the time a report could carry
			// it, and the delegate that hands over a replacement was refused at admission.
			destroyed = verdict == SolidFoodVerdict.EatsAndDestroys;
			family = "ate";
		}
		else if (LimbToolAdmission.IsLimbTool(_limbSemantics, _solidFoodSemantics, originalItem.ItemId))
		{
			// The limb-tool family DOES know: its applier runs the delegate itself, so it
			// can see the object the delegate destroyed (the component-bearing tools) —
			// and the tool's own data cannot say it, because the destruction is written
			// inside the delegate's body.
			destroyed = msg.Consumed;
			family = "used on the affected side";
		}
		else
		{
			_log.LogWarning("[ItemUse] refused the affected-side outcome from {Actor}: {ItemId} (id {InstanceId}) is neither a solid food nor a limb tool on this host.",
				sender, originalItem.ItemId, msg.ItemInstanceId);
			return null;
		}

		var newItem = PlayerCharacterAccess.CloneItem(originalItem);
		newItem.Condition = msg.Condition;

		_commit.CommitItemAfterUse(owner, originalItem.InstanceId, newItem, destroyed, movedToTheTarget: false);
		_log.LogInformation("[ItemUse] {Actor} {Family} {ItemId} (id {InstanceId}) of {Owner}; condition {Condition:F2}{Consumed}.",
			sender, family, originalItem.ItemId, originalItem.InstanceId, owner, msg.Condition,
			destroyed ? ", consumed by its own action" : "");

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
