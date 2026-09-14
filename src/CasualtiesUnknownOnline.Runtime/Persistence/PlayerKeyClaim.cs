namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// Which present peer claims a stored <c>characters/&lt;playerKey&gt;.json</c>, as the answer
/// to one question. Three values, because the two failures are not the same fact:
/// <see cref="Unclaimed"/> is decision 162's benign "that player is absent from this session"
/// (the file stays for a later claim and the peer joins as a NEW player), while
/// <see cref="Ambiguous"/> is a REFUSAL — an IP-direct world keys its characters by display
/// name and that mode deliberately allows duplicate names, so a stored character with two
/// present claimants must be handed to NEITHER of them. Collapsing the two into one boolean
/// is what let the first matching peer silently take another player's character.
/// </summary>
public enum PlayerKeyClaim
{
	/// <summary>No present peer claims this key: that player is absent (decision 162 — a new character).</summary>
	Unclaimed,

	/// <summary>Exactly one present peer claims it; the resolution reports that peer's id.</summary>
	Claimed,

	/// <summary>More than one present peer claims it. A refusal, never a silent pick; the resolution reports no peer.</summary>
	Ambiguous,
}
