using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The GUEST side of the fluid-presentation chain: the host simulates the
/// world fluid alone, so the guest would never see the transient effects that
/// the host's <c>FluidManager.SimulationStep</c> produces — the water-push
/// <c>WaterPusher</c> objects and the <c>waterflow1..3</c> sounds. This class
/// receives the host's dedicated <see cref="FluidPresentationMsg"/> events and
/// replays them onto the guest's local world: a WaterPusher gives the guest's
/// own body the same push/slip/ragdoll feel, and the sound plays at the exact
/// cell the host chose (the host already consumed the random clip index).
/// The authoritative fluid grid itself is untouched — it keeps riding the
/// <see cref="FluidRegionMsg"/> stream.
/// </summary>
internal sealed class FluidPresentationApplication(ILogger<FluidPresentationApplication> log)
{
	private readonly ILogger<FluidPresentationApplication> _log = log;

	internal void Apply(FluidPresentationMsg msg)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — == (no world yet — drop the transient, the next one covers it)
		{
			return;
		}

		var pos = world.BlockToWorldPos(new Vector2Int(msg.X, msg.Y));
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			switch (msg.Kind)
			{
				case FluidPresentationMsg.KindWaterPush:
					SpawnWaterPusher(pos, new Vector2(msg.DirX, msg.DirY));
					break;
				case FluidPresentationMsg.KindWaterflowSound:
					Sound.Play("waterflow" + msg.SoundIndex, pos, false, true, null, 1f, 1f, false, false);
					break;
				default:
					_log.LogWarning("[Fluid] unknown presentation kind={Kind} at=({X},{Y}).", msg.Kind, msg.X, msg.Y);
					return;
			}
		}

		_log.LogInformation("[Fluid] replayed presentation kind={Kind} at=({X},{Y}).", msg.Kind, msg.X, msg.Y);
	}

	/// <summary>Mirror of FluidManager.IncrMove's WaterPusher creation
	/// (FluidManager.cs:242-252): a 0.75 s trigger collider that pushes and
	/// slips the local body while it overlaps.</summary>
	private static void SpawnWaterPusher(Vector2 pos, Vector2 direction)
	{
		var go = new GameObject("WaterPush", typeof(CircleCollider2D), typeof(WaterPusher));
		go.transform.position = pos;
		var collider = go.GetComponent<CircleCollider2D>();
		collider.isTrigger = true;
		collider.radius = 1.5f;
		go.GetComponent<WaterPusher>().direction = direction;
		Object.Destroy(go, 0.75f);
	}
}
