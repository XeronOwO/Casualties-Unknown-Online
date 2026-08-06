using CasualtiesUnknownOnline.Runtime.Session;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter;

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
		if (template is null)
		{
			log.LogWarning("Remote body: \"Experiment\" player object not found in scene.");
			return null;
		}

		var clone = Object.Instantiate(template);
		clone.name = $"Character_{remote.SteamId:X}";
		clone.SetActive(true);

		var body = clone.GetComponentInChildren<Body>();
		if (body is null)
		{
			Object.Destroy(clone);
			log.LogWarning("Remote body: no Body component in \"Experiment\" clone.");
			return null;
		}

		body.transform.position = anchor;
		body.targetLookPos = new Vector2(1000f, 460f);
		clone.AddComponent<RemoteBodyDriver>();

		// Freeze ALL physics and joints — the limbs are separate
		// Rigidbody2D+HingeJoint rigs that would otherwise keep simulating and
		// convulse the proxy while the session overwrites the root transform.
		foreach (var rb in clone.GetComponentsInChildren<Rigidbody2D>())
		{
			rb.simulated = false;
		}

		foreach (var hinge in clone.GetComponentsInChildren<HingeJoint2D>())
		{
			hinge.enabled = false;
		}

		return body;
	}
}
