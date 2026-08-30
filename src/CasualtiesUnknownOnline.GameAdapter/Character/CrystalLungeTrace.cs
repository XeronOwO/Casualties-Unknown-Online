namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The Harmony __state crossing CrystalEnemy.Lunge: the local body and its
/// pre-lunge limb values. The postfix finds the one limb the native hit
/// actually changed (the game's RaycastAll picks a random non-dismembered
/// limb, CrystalEnemy.cs:137-144) — report only after that verified write.
/// Split out of <see cref="EnemyCombatDirector"/> so the director's owner is
/// the combat arbitration, not the local-body trace detail.
/// </summary>
internal sealed class CrystalLungeTrace
{
	private readonly Body _body;
	private readonly float[] _skin;
	private readonly float[] _muscle;
	private readonly float[] _pain;
	private readonly float[] _bleed;

	private CrystalLungeTrace(Body body, float[] skin, float[] muscle, float[] pain, float[] bleed)
	{
		_body = body;
		_skin = skin;
		_muscle = muscle;
		_pain = pain;
		_bleed = bleed;
	}

	internal static CrystalLungeTrace? Capture(Body body)
	{
		var skin = new float[body.limbs.Length];
		var muscle = new float[body.limbs.Length];
		var pain = new float[body.limbs.Length];
		var bleed = new float[body.limbs.Length];
		for (var i = 0; i < body.limbs.Length; i++)
		{
			var limb = body.limbs[i];
			if (limb == null) // Unity object — ==
			{
				return null;
			}

			skin[i] = limb.skinHealth;
			muscle[i] = limb.muscleHealth;
			pain[i] = limb.pain;
			bleed[i] = limb.bleedAmount;
		}

		return new CrystalLungeTrace(body, skin, muscle, pain, bleed);
	}

	internal Limb? FindChangedLimb()
	{
		if (_body == null) // Unity object — ==
		{
			return null;
		}

		for (var i = 0; i < _body.limbs.Length && i < _skin.Length; i++)
		{
			var limb = _body.limbs[i];
			if (limb == null) // Unity object — ==
			{
				continue;
			}

			if (limb.skinHealth != _skin[i]
				|| limb.muscleHealth != _muscle[i]
				|| limb.pain != _pain[i]
				|| limb.bleedAmount != _bleed[i])
			{
				return limb;
			}
		}

		return null;
	}
}
