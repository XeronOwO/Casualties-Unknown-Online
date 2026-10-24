namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// What a cross-player item use needs from the TARGET's body, per family. The user's 2026-10-24 finding: an
/// unconscious teammate is exactly who the medical families exist for, so requiring a CONSCIOUS target refused the
/// gesture's whole point. The operator's own consciousness is the gate that belongs to the actor (the participant
/// gate), and the target only has to be able to receive what its family does.
///
/// <para>
/// The split mirrors the medical operation family's own rule (`MedicalTargetBodyValidator`: responsiveness is
/// required by the injection and fragment-pull families and by none of the bandaging, splinting, defibrillation or
/// amputation ones), which keeps one answer to "may an unresponsive body be treated" across both families instead of
/// two. A DEAD body is refused by both: no family here is the defibrillator, and a corpse is the medical operation
/// family's business, not an item's.
/// </para>
/// </summary>
public static class ItemUseTargetPolicy
{
	/// <summary>
	/// True for the families where the TARGET itself consumes the item — drinking and eating — because a body that
	/// cannot swallow cannot do either. Every other family applies something TO the body and therefore works on an
	/// unresponsive target.
	/// </summary>
	public static bool RequiresAConsciousTarget(ItemUseFamily family) =>
		family is ItemUseFamily.Drink or ItemUseFamily.SolidFood;
}
