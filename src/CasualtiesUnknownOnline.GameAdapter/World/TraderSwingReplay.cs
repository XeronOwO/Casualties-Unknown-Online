using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Replays a hostile trader's one-shot swing presentation on a receiver-side
/// trader. The acting side already ran the native <c>TraderScript.Swing</c>
/// instantiation and swing sound; this helper reproduces the same prefab
/// orientation/scale/anchor on the peer's same-position trader (position-keyed
/// like the trade domain).
/// </summary>
internal static class TraderSwingReplay
{
	internal static void Play(TraderScript trader, TraderSwingMsg msg)
	{
		if (trader == null || trader.attackAnimation == null) // Unity objects — ==
		{
			return;
		}

		var direction = new Vector2(msg.Direction.X, msg.Direction.Y);
		if (direction.sqrMagnitude < 0.0001f)
		{
			return;
		}

		direction.Normalize();
		var prefab = string.IsNullOrEmpty(msg.Prefab) ? null : Resources.Load<GameObject>(msg.Prefab);
		var source = prefab != null ? prefab : trader.attackAnimation; // Unity object — ==
		if (source == null)
		{
			return;
		}

		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var go = Object.Instantiate(source);
			go.transform.eulerAngles = new Vector3(0f, 0f, Vector2.SignedAngle(Vector3.right, direction));
			go.transform.localScale = new Vector3(1f, direction.x > 0f ? 1f : -1f, 1f);
			go.transform.position = trader.torso.position + trader.torso.up * 0.8f;
			Sound.Play("BSSwing1", trader.transform.position, false, true, null, 1f, 1f, false, false);
			Object.Destroy(go, 2f);
		}
	}
}
