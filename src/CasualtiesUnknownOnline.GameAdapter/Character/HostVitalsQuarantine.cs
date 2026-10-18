namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The client-owned fields of a remote body, captured before the host's fixed pass and restored
/// after it. The fatal combination this exists for: the game's vital stages read and write these
/// fields, but the 2026-10-18 ruling reserves every one of them to the AFFECTED CLIENT — a random
/// outcome (infection, internal bleeding, stroke, embolism, shock) and a terminal transition
/// (death, unconsciousness, fibrillation, sleep, a mended/dislocated limb) must arrive from that
/// client's report or event, never from the host's own arithmetic.
///
/// <para>
/// The host's stages still execute (the ticket reuses them wholesale), and the second-check's
/// threshold comparisons still draw from <c>UnityEngine.Random</c> because the game's method
/// cannot be split and the RNG entry point has no IL body to patch. This capture is what makes
/// those draws harmless: every field the policy below names is restored, so no drawn outcome
/// persists, reaches a body transition, or leaves on the wire. The policy is the set the native
/// random/terminal writes reach: the body terminal/derived values (brain health, consciousness,
/// fibrillation progress/forced, heart rate — and therefore its <c>inCardiacArrest</c> getter —
/// respiratory rate, sleeping, the ECG variation draw), the random-event inputs (internal
/// bleeding, viscosity, stroke, embolism, septic shock, shock), and the limb values a draw or a
/// vital-limb death changes (infection/amount, broken/dislocated and their heal timers, skin and
/// muscle health). The report/event is the only writer that survives the pass.
/// </para>
///
/// <para>
/// Deliberately NOT here: the continuous values the host computes (blood volume, total bleeding
/// speed, limb bleed, the derived bleeding/clothing/immunity values), and the injury-direction limb
/// fields whose client updates arrive through the report or the owner event.
/// </para>
/// </summary>
internal sealed class HostVitalsQuarantine
{
	private float _brainHealth;
	private float _consciousness;
	private float _fibrillationProgress;
	private bool _fibrillationForced;
	private bool _sleeping;
	private float _internalBleeding;
	private float _bloodViscosity;
	private float _strokeAmount;
	private bool _hasPulmonaryEmbolism;
	private float _septicShock;
	private float _shock;
	private float _heartRate;
	private float _respiratoryRate;
	private float _randomFibrillationVariation;

	private bool[] _infected = [];
	private float[] _infectionAmount = [];
	private bool[] _broken = [];
	private bool[] _dislocated = [];
	private float[] _boneHealTimer = [];
	private float[] _dislocationTimer = [];
	private float[] _skinHealth = [];
	private float[] _muscleHealth = [];

	internal void Capture(Body body)
	{
		_brainHealth = body.brainHealth;
		_consciousness = body.consciousness;
		_fibrillationProgress = body.fibrillationProgress;
		_fibrillationForced = body.fibrillationForced;
		_sleeping = body.sleeping;
		_internalBleeding = body.internalBleeding;
		_bloodViscosity = body.bloodViscosity;
		_strokeAmount = body.strokeAmount;
		_hasPulmonaryEmbolism = body.hasPulmonaryEmbolism;
		_septicShock = body.septicShock;
		_shock = body.shock;
		_heartRate = body.heartRate;
		_respiratoryRate = body.respiratoryRate;
		_randomFibrillationVariation = body.randomFibrillationVariation;

		var limbs = body.limbs;
		if (_infected.Length != limbs.Length)
		{
			_infected = new bool[limbs.Length];
			_infectionAmount = new float[limbs.Length];
			_broken = new bool[limbs.Length];
			_dislocated = new bool[limbs.Length];
			_boneHealTimer = new float[limbs.Length];
			_dislocationTimer = new float[limbs.Length];
			_skinHealth = new float[limbs.Length];
			_muscleHealth = new float[limbs.Length];
		}

		for (var i = 0; i < limbs.Length; i++)
		{
			var limb = limbs[i];
			_infected[i] = limb.infected;
			_infectionAmount[i] = limb.infectionAmount;
			_broken[i] = limb.broken;
			_dislocated[i] = limb.dislocated;
			_boneHealTimer[i] = limb.boneHealTimer;
			_dislocationTimer[i] = limb.dislocationTimer;
			_skinHealth[i] = limb.skinHealth;
			_muscleHealth[i] = limb.muscleHealth;
		}
	}

	internal void Restore(Body body)
	{
		body.brainHealth = _brainHealth;
		body.consciousness = _consciousness;
		body.fibrillationProgress = _fibrillationProgress;
		body.fibrillationForced = _fibrillationForced;
		body.sleeping = _sleeping;
		body.internalBleeding = _internalBleeding;
		body.bloodViscosity = _bloodViscosity;
		body.strokeAmount = _strokeAmount;
		body.hasPulmonaryEmbolism = _hasPulmonaryEmbolism;
		body.septicShock = _septicShock;
		body.shock = _shock;
		body.heartRate = _heartRate;
		body.respiratoryRate = _respiratoryRate;
		body.randomFibrillationVariation = _randomFibrillationVariation;

		var limbs = body.limbs;
		for (var i = 0; i < limbs.Length && i < _infected.Length; i++)
		{
			var limb = limbs[i];
			limb.infected = _infected[i];
			limb.infectionAmount = _infectionAmount[i];
			limb.broken = _broken[i];
			limb.dislocated = _dislocated[i];
			limb.boneHealTimer = _boneHealTimer[i];
			limb.dislocationTimer = _dislocationTimer[i];
			limb.skinHealth = _skinHealth[i];
			limb.muscleHealth = _muscleHealth[i];
		}
	}
}
