using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Runtime.GameAdapter;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// What the host's refusal of one member console write did to this client: the item
/// it named, why the host refused, and whether the recorded state went back onto the
/// local object.
///
/// <para>
/// It exists so the operator reads the answer where they typed the command. A
/// console write is applied locally and reported, so the host's verdict arrives
/// LATER than the command's own answer: without this line the player would read
/// "written" and never learn that the host disagreed, while the value quietly
/// returned to what it was.
/// </para>
/// </summary>
/// <param name="InstanceId">The item the refused write named.</param>
/// <param name="DefinitionId">Its definition, so the line names the item the way every other line of the verb does.</param>
/// <param name="Reason">Why the host refused it.</param>
/// <param name="Restore">What the local scene answered when the recorded state was put back.</param>
public readonly record struct ItemDataWriteRefusal(
	ulong InstanceId,
	string DefinitionId,
	RejectionReason Reason,
	LocalItemWriteResult Restore)
{
	/// <summary>The console line's text: the refusal in the operator's words, and what happened to the value they wrote.</summary>
	internal string Describe()
	{
		var why = Reason switch
		{
			RejectionReason.HostRuleDenied => "this host's rule does not open the item-data write verbs to members",
			RejectionReason.NotAuthorized => "the record does not put that item in your hands",
			RejectionReason.UnknownAggregate => "this session records no judged item under that id",
			RejectionReason.InvalidTransition => "the item's recorded state does not take a write",
			RejectionReason.WrongRevision => "the host's record of that item moved on",
			_ => Reason.ToString(),
		};

		return Restore == LocalItemWriteResult.Applied
			? $"The host refused the write to #{InstanceId} {DefinitionId} ({why}) — the recorded value is back on this client."
			: $"The host refused the write to #{InstanceId} {DefinitionId} ({why}), and this client could NOT put the recorded value back ({Restore}) — the two sides may disagree until the next report of that item.";
	}
}
