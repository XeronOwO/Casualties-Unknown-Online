using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The local apply side of the cross-player push operation. A push is a
/// one-shot body interaction: the target's own client applies the native
/// ragdoll + velocity delta in a RemoteApply scope, the pusher's client pays
/// the small stamina/temperature cost, and every side plays the one-shot
/// push sound at the target's position. The target's resulting motion then
/// rides the existing 20 Hz player state stream as the presentation fallback.
/// </summary>
internal sealed class PlayerPushApply(GameAdapterDomains domains)
{
	private const float PushStaminaCost = 1f;
	private const float PushHeatGain = 0.03f;

	public void Apply(PlayerPushResultMsg msg)
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			domains.Log.LogWarning("[Push] result received but the local body is not ready — skipped.");
			return;
		}

		var changed = false;
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			if (msg.TargetSteamId == domains.Session.LocalSteamId)
			{
				body.Ragdoll();
				body.SetVelocity(body.rb.velocity + new Vector2(msg.ForceX, msg.ForceY));
				domains.Log.LogInformation("[Push] local body pushed by {Pusher}.", msg.PusherSteamId);
				changed = true;
			}

			if (msg.PusherSteamId == domains.Session.LocalSteamId)
			{
				body.stamina -= PushStaminaCost;
				body.temperature += PushHeatGain;
				domains.Log.LogInformation("[Push] local player pushed {Target}.", msg.TargetSteamId);
				changed = true;
			}

			PlayPushSound(msg.TargetSteamId);
		}

		if (changed)
		{
			domains.CharacterDataSync.ReportInventoryChanged(body);
		}
	}

	private void PlayPushSound(ulong targetSteamId)
	{
		Vector2? soundPos = null;
		if (targetSteamId == domains.Session.LocalSteamId)
		{
			var localBody = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
			if (localBody != null) // Unity object — ==
			{
				soundPos = localBody.transform.position;
			}
		}
		else if (domains.Entities.GetRemotePlayer(targetSteamId) is { } remote)
		{
			soundPos = new Vector2(remote.Position.X, remote.Position.Y);
		}

		if (soundPos is { } pos)
		{
			Sound.Play("landsmall1", pos, false, true, null, 1f, 1f, false, false);
		}
	}
}
