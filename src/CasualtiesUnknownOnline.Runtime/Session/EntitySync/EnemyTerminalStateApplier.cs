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

	private static void ApplyLimb(CharacterDataMsg data, CharacterLimbMsg limb)
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
