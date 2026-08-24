using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Tiny local-body-only marker carrying which nap coroutine the owner started.
/// <c>Body.TakeANap</c> branches between <c>NapCoroutine</c> and
/// <c>AltNapCoroutine</c> without exposing the choice as a field, so
/// <see cref="Patches.BodyNapPatch"/> records it here for
/// <c>RunCoordinator.PublishBodyState</c>. It is never added to a render clone
/// (only the local-body nap patch adds it).
/// </summary>
internal sealed class LocalNapTracker : MonoBehaviour
{
	/// <summary>The last nap variant (0 = standard, 1 = alt/sick — see <see cref="NapPresentation"/>).</summary>
	public byte NapVariant;
}
