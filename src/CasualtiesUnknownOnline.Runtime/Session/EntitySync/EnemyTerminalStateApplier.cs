using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Pure application of an enemy attack/proximity event's terminal state onto a
/// <see cref="CharacterDataMsg"/> (exact rebuild, never a delta). The host's
/// <see cref="CharacterDataStore"/> uses it to merge the event into the saved
/// snapshot before the next 1 Hz report; the Game Adapter's clone fact table
/// uses the same machine so both records can never diverge in interpretation.
/// </summary>
public static class EnemyTerminalStateApplier
{
	public static void ApplyBite(CharacterDataMsg data, EnemyBiteMsg msg)
	{
		var health = EnsureHealth(data);
		ApplyLimb(data, msg.Limb);
		health.VenomTotal = msg.VenomTotal;
		health.Adrenaline = msg.Adrenaline;
		health.Happiness = msg.Happiness;
	}

	public static void ApplyLunge(CharacterDataMsg data, EnemyLungeMsg msg)
	{
		var health = EnsureHealth(data);
		ApplyLimb(data, msg.Limb);
		health.Adrenaline = msg.Adrenaline;
		health.Stamina = msg.Stamina;
	}

	/// <summary>A limb-latch or owner-judged injury event's full terminal state — replace the WHOLE
	/// limb set with the event's full set (Dismember mutates more than the
	/// reported limb, so merge-by-index would leave stale lower limbs) and,
	/// when the reporter carried it, the whole body health record. Shared by
	/// the host's saved-character merge and the clone fact table so both
	/// records interpret the event identically.
	/// <para>
	/// The event is the owner's judgment of its LIMB and non-owned body state; the two continuous
	/// blood values are the HOST's (the 2026-10-18 ruling) and are preserved from the record being
	/// updated. A limb's bleed travels with the limb set — that is the injury outcome the host must
	/// accept — but only UPWARD: the host already holds the last written limb bleed, so a lower
	/// value (a member claim, or the client's own decay) cannot pull the host model down; a
	/// dismembered limb's zero is the one downward write (the amputation authority). The clamp
	/// mutates the event's own limb objects, so the relay carries the host's value on purpose, the
	/// same way the health block's two blood values below do.
	/// </para>
	/// </summary>
	public static void ApplyLimbState(CharacterDataMsg data, LimbStateEventMsg msg)
	{
		var previousBleeds = new Dictionary<int, float>();
		foreach (var limb in data.Limbs)
		{
			previousBleeds[limb.Index] = limb.BleedAmount;
		}

		if (msg.Limbs.Count > 0)
		{
			data.Limbs = [.. msg.Limbs];
			foreach (var limb in data.Limbs)
			{
				if (limb.Dismembered)
				{
					limb.BleedAmount = 0f; // the amputation authority
				}
				else if (previousBleeds.TryGetValue(limb.Index, out var previous) && limb.BleedAmount < previous)
				{
					limb.BleedAmount = previous; // the host holds the last written value; never lower it
				}
			}
		}

		if (msg.Health is not null)
		{
			var hostBloodVolume = data.Health?.BloodVolume;
			var hostTotalBleedSpeed = data.Health?.TotalBleedSpeed;
			data.Health = msg.Health;
			if (hostBloodVolume is { } blood)
			{
				data.Health.BloodVolume = blood;
			}

			if (hostTotalBleedSpeed is { } total)
			{
				data.Health.TotalBleedSpeed = total;
			}
		}
	}

	public static void ApplyEffect(CharacterDataMsg data, EnemyEffectMsg msg)
	{
		var health = EnsureHealth(data);
		switch (msg.Kind)
		{
			case EnemyEffectKind.ElderHorrorTick:
				health.HorrifiedLevel = msg.HorrifiedLevel;
				health.FocusedLevel = msg.FocusedLevel;
				health.Adrenaline = msg.Adrenaline;
				health.Energy = msg.Energy;
				health.Stamina = msg.Stamina;
				break;
			case EnemyEffectKind.ElderHorrorDefeat:
				health.HorrifiedLevel = msg.HorrifiedLevel;
				health.Happiness = msg.Happiness;
				health.Caffeinated = msg.Caffeinated;
				break;
			case EnemyEffectKind.XalorisSepticTick:
				health.SepticShock = msg.SepticShock;
				break;
			case EnemyEffectKind.GrabberGrabbed:
				health.Shock = msg.Shock;
				health.EyePanicTime = msg.EyePanicTime;
				break;
		}
	}

	private static CharacterHealthMsg EnsureHealth(CharacterDataMsg data) =>
		data.Health ??= new CharacterHealthMsg();

	/// <summary>Replace (or append) one limb's full terminal state by index — exact rebuild, shared by the enemy events and the limb-latch events so both records interpret a limb identically.</summary>
	public static void ApplyLimb(CharacterDataMsg data, CharacterLimbMsg limb)
	{
		var idx = data.Limbs.FindIndex(l => l.Index == limb.Index);
		if (idx >= 0)
		{
			data.Limbs[idx] = limb;
		}
		else
		{
			data.Limbs.Add(limb);
		}
	}
}
