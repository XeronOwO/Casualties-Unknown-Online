using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Local target-side application of host-authoritative timed limb effects
/// carried by cross-player item-use results. It reuses the game's
/// <c>CoUtils.DoTimedOp</c> 1 Hz tick semantics directly, so a cross-player
/// suture behaves exactly like the native self-use path (Item.cs:381). The
/// effect lives on the target's simulated body; the ordinary character snapshot
/// paths carry the resulting state back to the host.
/// </summary>
internal static class TimedLimbEffectApply
{
	public static void Apply(Body body, IReadOnlyList<TimedLimbEffectMsg> effects, ILogger log)
	{
		if (effects.Count == 0)
		{
			return;
		}

		foreach (var effect in effects)
		{
			if (effect.LimbIndex < 0 || effect.LimbIndex >= body.limbs.Length)
			{
				log.LogWarning("[ItemUse] timed limb effect skipped: limb {Limb} is out of range ({Count} limbs).", effect.LimbIndex, body.limbs.Length);
				continue;
			}

			var limb = body.limbs[effect.LimbIndex];
			CoUtils.instance.DoTimedOp("suture" + limb.name, () =>
			{
				limb.bleedAmount = Mathf.Max(0f, limb.bleedAmount + effect.BleedPerSecond);
			}, effect.DurationSeconds);
			log.LogInformation("[ItemUse] scheduled timed limb effect: limb {Limb}, {Delta:F2} bleed/s for {Duration:F1}s.", effect.LimbIndex, effect.BleedPerSecond, effect.DurationSeconds);
		}
	}
}
