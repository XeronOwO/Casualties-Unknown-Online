using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Creates remote player bodies by cloning the scene's player character
/// ("Experiment" GameObject — same template KrokMP uses). On the host the clone
/// is simulated (driven by guest input); on the guest it is a render proxy that
/// reflects host state. The clone spawns exactly at the peer's position
/// (reported spawn point / PlayerJoin anchor) — no offset: a constant offset
/// would keep the two simulations permanently divergent.
/// </summary>
internal static class RemoteBodyFactory
{
	public static Body? CreateRemoteBody(PlayerEntity remote, Vector2 anchor, ILogger log)
	{
		var template = GameObject.Find("Experiment");
		if (template == null) // Unity object — == (is null misses destroyed)
		{
			log.LogWarning("Remote body: \"Experiment\" player object not found in scene.");
			return null;
		}

		var clone = Object.Instantiate(template);
		clone.name = $"Character_{remote.SteamId:X}";
		clone.SetActive(true);

		var body = clone.GetComponentInChildren<Body>();
		if (body == null) // Unity object — == (is null misses destroyed)
		{
			Object.Destroy(clone);
			log.LogWarning("Remote body: no Body component in \"Experiment\" clone.");
			return null;
		}

		body.transform.position = anchor;
		body.targetLookPos = new Vector2(1000f, 460f);
		// MUST go on the Body component's GameObject: the proxy-detection
		// patches query GetComponent<RemoteBodyDriver>() on the Body (which is a
		// CHILD of the root "Experiment" clone). On the root it was never found
		// and the clone ran the full original simulation (HandleBody → Ragdoll
		// → physics re-enabled → limbs falling).
		body.gameObject.AddComponent<RemoteBodyDriver>();

		// Visual-input fields the clone copies from the template at Instantiate
		// time are stale (the simulation that would keep them current is skipped
		// — Body.Update is replaced by the render-only patch). Zero the pose
		// state so the clone stands: crouch amount, water/climb flags.
		body.crouchAmount = 0f;
		body.inWater = false;
		body.currentClimbable = null;

		// Freeze ALL physics and joints — the limbs are separate
		// Rigidbody2D+HingeJoint rigs that would otherwise keep simulating and
		// convulse the proxy while the session overwrites the root transform.
		foreach (var rb in clone.GetComponentsInChildren<Rigidbody2D>())
		{
			rb.simulated = false;
		}

		// Strip instance ids from the clone's carried items (the template may
		// carry the local player's runtime items, copied by the Instantiate):
		// the clone is a render proxy — its items must never be found by the
		// world-item lookup (a pickup/drop of the original would otherwise
		// roll back or move the clone's copy instead of materializing the
		// real item). The clone's item DISPLAY is rendered separately by
		// CloneInventoryRenderer from the owner's 1 Hz character snapshot.
		foreach (var idComp in clone.GetComponentsInChildren<ItemInstanceId>())
		{
			Object.Destroy(idComp);
		}

		foreach (var hinge in clone.GetComponentsInChildren<HingeJoint2D>())
		{
			hinge.enabled = false;
		}

		// Disable ALL colliders on the proxy: it must never participate in
		// physics — contacts/queries can re-activate frozen rigidbodies
		// (observed: limb Rigidbody2D.simulated flipping back to true and
		// gravity dragging the clone apart), and the proxy should be
		// intangible anyway (players pass through it — BodyStartPatch).
		foreach (var col in clone.GetComponentsInChildren<Collider2D>())
		{
			col.enabled = false;
		}

		// IKHandle.Update (IKHandle.cs:43-57) lerps targetPos to
		// Camera.main.ScreenToWorldPoint(Input.mousePosition) and draws a
		// LineRenderer toward it — a clone would draw "aim lines" at the LOCAL
		// player's mouse. Disable: it is single-player interaction visuals.
		foreach (var ik in clone.GetComponentsInChildren<IKHandle>())
		{
			ik.enabled = false;
		}

		// Owner-local body auto-event components must not run on a render
		// clone. Vomiter/SelfHarmer/PantSound are mounted on the Body object
		// (Body.cs:1074/1077/3434) and their own Update methods are NOT part of
		// Body.Update/Limb.Update, so the render-proxy patch does not skip them.
		// MoodChangeSounds/SleepingBagUse additionally read PlayerCamera.main.body
		// (the local player), so leaving them enabled on a clone would double
		// local mood sounds or even destroy the clone from the local player's
		// sleeping-bag state. These effects remain owner-local by design; remote
		// presentation, if ever wanted, belongs in a dedicated future event path.
		foreach (var component in clone.GetComponentsInChildren<Vomiter>())
		{
			component.enabled = false;
		}

		foreach (var component in clone.GetComponentsInChildren<SelfHarmer>())
		{
			component.enabled = false;
		}

		foreach (var component in clone.GetComponentsInChildren<PantSound>())
		{
			component.enabled = false;
		}

		foreach (var component in clone.GetComponentsInChildren<MoodChangeSounds>())
		{
			component.enabled = false;
		}

		foreach (var component in clone.GetComponentsInChildren<SleepingBagUse>())
		{
			component.enabled = false;
		}

		return body;
	}
}
