using CasualtiesUnknownOnline.Runtime.Session.World;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Holds a replayed turret's lightSprite steady during the 0.5 s warning
/// window. The replay sets didShoot=true at the warning to lock the game's
/// real shot (TurretScript.cs:40-53) and start the 15 s reload, but that also
/// makes TurretScript.Update start the lightSprite flicker
/// (TurretScript.cs:29) 0.5 s early. This gate overrides the flicker in
/// LateUpdate (after every Update) until the delayed shot visual lands, then
/// removes itself so the native flicker takes over exactly like the trigger
/// side.
/// </summary>
internal sealed class TurretLightSpriteGate : MonoBehaviour
{
	private SpriteRenderer? _lightSprite;

	private float _remaining;

	/// <summary>Start the warning-window override on a replayed turret.</summary>
	internal static void Begin(TurretScript turret)
	{
		if (turret.GetComponent<TurretLightSpriteGate>() != null) // Unity object — ==
		{
			return; // duplicate replay: one gate is enough
		}

		var gate = turret.gameObject.AddComponent<TurretLightSpriteGate>();
		gate._lightSprite = turret.lightSprite;
		gate._remaining = TurretReplayTimeline.ShotDelaySeconds;
	}

	private void LateUpdate()
	{
		if (_remaining > 0f)
		{
			_remaining -= Time.deltaTime;
			if (_lightSprite != null) // Unity object — ==
			{
				_lightSprite.enabled = true;
			}
		}
		else
		{
			Destroy(this);
		}
	}
}
