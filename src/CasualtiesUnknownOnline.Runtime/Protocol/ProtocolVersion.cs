namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Wire compatibility version for the handshake. No released
	/// compatibility surface exists yet, so each behavioral wire extension bumps
	/// this and mixed-version sessions are rejected by the handshake.
	/// 19: `BlockStateEntryMsg.SupportLossSettled` — the receiver uses it to decide
	/// whether a snapshot row re-settles building support loss, so a peer without it
	/// would re-kill buildings the authority still holds.
	/// 20: `WorldItemsReset` / `ResetWorldItemsCommand` — the layer boundary now
	/// resets the item domain's world-rooted records, so a peer without the event
	/// would keep every earlier layer's world items and then disagree with the
	/// host's kernel after the first layer switch.
	/// 21: `RuntimeEntityRejected` — a runtime creation the host cannot
	/// materialize is now REJECTED instead of relayed, so a peer without the
	/// message would keep relaying an unowned creation to everyone and leave the
	/// reporter's copy and 60 s re-report alive forever.
	/// 22: `BlockDamageReport` — a guest's partial block damage now has an
	/// ABSOLUTE re-report whose answer is a `BlockDamageSnapshot` carrying this
	/// host's authoritative value per reported cell. A peer without the message
	/// would never send the report (its swallowed hits would stay missing from
	/// the host's own damage list forever) and would not understand the answer's
	/// zero rows.</summary>
	public const int Current = 22;

}
