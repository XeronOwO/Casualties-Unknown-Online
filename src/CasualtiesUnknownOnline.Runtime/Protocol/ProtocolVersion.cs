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
	/// host's kernel after the first layer switch.</summary>
	public const int Current = 20;

}
