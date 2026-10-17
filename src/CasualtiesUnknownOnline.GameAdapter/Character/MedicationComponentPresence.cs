namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// What the receiver should do with a <c>Body</c> component when authoritative state
/// arrives: match the sender, INCLUDING when the sender no longer has it.
/// <para>
/// The capture side writes a component's fields only while the component is there, so an
/// absent component arrives as the message's defaults — a <c>false</c> presence flag, or
/// zeroed amounts. Reading that as "nothing to do" is what left a stale component standing
/// on every receiver: the owner's copy was gone and the message said so, and the receiver
/// kept its own. The three states a receiver can be in are therefore named here rather than
/// implied by an <c>if</c> with no else arm.
/// </para>
/// <para>
/// For the amount-carrying components, "no component" and "component whose amount decayed
/// to zero" are the same wire value. That is safe because the game's own
/// <c>Update</c> destroys those components once their state clears, so removing one early
/// reaches the state the sender is in.
/// </para>
/// </summary>
internal enum MedicationComponentPresence
{
	/// <summary>The sender has no such component — the receiver's copy, if any, must go.</summary>
	Absent,

	/// <summary>The sender has it and it is switched off — the receiver keeps it, switched off.</summary>
	Inactive,

	/// <summary>The sender has it and it is in effect — the receiver keeps it and carries the values.</summary>
	Active,
}
