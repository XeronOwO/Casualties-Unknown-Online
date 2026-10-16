namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// What a DEAD player's own client does with the camera while they wait to be revived. The camera belongs to the
/// client that is watching and the body being watched is already replicated as that client's own display clone,
/// so nothing here travels: this is presentation, and the only thing a session decides about it is what its
/// rules allow (<see cref="HostRules.IHostRules"/>).
///
/// <para>
/// The game's own death screen stays the entry point and the place a player returns to; watching is something
/// they do FROM it. That is why <see cref="StopWatching"/> exists as its own verb rather than being implied by a
/// disconnect: the exit paths (a trader revive, the next-layer respawn, the menu-return cut) all end the view,
/// and a player who simply wants the screen back can ask for it.
/// </para>
/// </summary>
public interface ISpectatorControl
{
	/// <summary>True while this client's camera is following someone.</summary>
	bool IsWatching { get; }

	/// <summary>
	/// Follow the member with this SteamId. False when there is nothing to watch: the local character is not in
	/// a state that may watch (alive, or unconscious with the rule off), or that member has no body here to
	/// follow. A false answer changes nothing — the caller reports it rather than the view moving anyway.
	/// </summary>
	bool TryWatch(ulong steamId);

	/// <summary>Stop watching and go back to the game's own death screen.</summary>
	void StopWatching();

	/// <summary>
	/// Ask for the free camera, or give it up. False when the request is refused — which happens when the host's
	/// rule does not allow it, because that is a group decision (a free camera can scout the layer ahead) and
	/// this client's own preference only chooses WITHIN it. A refusal changes nothing.
	/// </summary>
	bool TrySetFreeFly(bool wanted);
}
