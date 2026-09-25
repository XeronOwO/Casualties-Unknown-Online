using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Marks a LOCAL body as being carried by another player. The carry relation
/// owns the body's TRANSFORM, not its simulation: a conscious/alive rider keeps
/// the game's own per-frame simulation (vitals, heart progression behind the
/// ECG animation, limb state, sounds) and only its movement input and the
/// native idle-sit are gated while carried
/// (<see cref="Runtime.Session.EntitySync.CarriedBodySimulation"/>).
/// GameAdapter drives its transform from the carrier's entity state each frame,
/// so the carried player's own client moves its body to the carrier's back and
/// reports that position through the normal 20 Hz/1 Hz streams — all other
/// peers see the carried body through ordinary entity state, without a separate
/// carry-specific render network. A dead or unconscious carried body still uses
/// the frozen pinned-ragdoll presentation.
/// </summary>
internal sealed class CarriedBodyDriver : MonoBehaviour
{
	/// <summary>The SteamId of the player carrying this body.</summary>
	public ulong CarrierSteamId;

	/// <summary>
	/// True when a driver component is present AND it is still actively carrying
	/// this body. A released driver is zeroed before Unity destroys it, so the
	/// render-proxy patches stop freezing the body in the same frame; otherwise
	/// the deferred <c>Object.Destroy</c> lets one more proxy frame run after
	/// <see cref="CarriedBodyPlacement.RestoreLocalBody"/> and re-freeze all
	/// rigidbodies — the "dropped body cannot move" regression.
	/// </summary>
	internal static bool IsActivelyCarried(bool driverPresent, ulong carrierSteamId) =>
		driverPresent && carrierSteamId != 0;

	/// <summary>True when the given Body still has an active carried-body driver.</summary>
	internal static bool IsCarrying(Body body)
	{
		var driver = body.GetComponent<CarriedBodyDriver>();
		return IsActivelyCarried(driver != null, driver != null ? driver.CarrierSteamId : 0); // Unity object — ==
	}

	/// <summary>True when the given Limb's parent Body still has an active carried-body driver.</summary>
	internal static bool IsCarryingInParent(Component component)
	{
		var driver = component.GetComponentInParent<CarriedBodyDriver>();
		return IsActivelyCarried(driver != null, driver != null ? driver.CarrierSteamId : 0); // Unity object — ==
	}
}
