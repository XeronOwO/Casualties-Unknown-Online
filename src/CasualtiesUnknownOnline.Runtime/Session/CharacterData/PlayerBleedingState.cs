using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// ONE player's continuous bleeding state on the HOST: the per-limb bleed amount and the
/// body's blood volume, advanced by the game's own bleeding rules (the 2026-10-18 ruling —
/// the host computes the continuous values, the affected client judges the events and
/// applies the row).
///
/// <para>
/// What is OWNED here and what is only READ is the whole design:
/// </para>
/// <list type="bullet">
/// <item>OWNED — <c>bleedAmount</c> per limb and <c>bloodVolume</c>. They advance from the
/// previous authoritative point, never from the next report: a plain report is not allowed to
/// write them (<see cref="ContinuousVitals"/>), and only an event the host arbitrated does
/// (<see cref="ApplyAuthority"/>).</item>
/// <item>READ — every input the rules take from the wire: viscosity, venom, hunger,
/// internal bleeding, and per limb the skin health, the tourniquet flag, the shrapnel
/// count, the bandage slow amount and the dismembered flag. They may change every report
/// and are copied in without judging them (a non-finite value is ignored, never adopted).</item>
/// <item>NOT HERE — anything random or terminal. Infection, internal-bleeding rolls, brain
/// damage, death and unconsciousness stay with the affected client and its events
/// (judgment ownership). This model never produces a state transition.</item>
/// </list>
///
/// <para>
/// The rules are the game's, quoted where they live: the per-limb decay and the blood drain
/// are <c>Limb.Update</c> (<c>Limb.cs:498-525</c>), the internal-bleeding and venom drains
/// and the derived totals are <c>Body.HandleBody</c> (<c>Body.cs:2799-2861</c>), and the
/// regeneration toward 100 with the [-100, 200] clamp is <c>Body.HandleCirculation</c>
/// (<c>Body.cs:688-695</c>). The two run settings it needs (<c>healingrate</c>,
/// <c>bleedrate</c>) are world globals the host owns.
/// </para>
///
/// <para>
/// ORDER: the game runs the limb pass and the body pass as two separate MonoBehaviour
/// updates, so the relative order of the two passes is not defined by the game. This model
/// fixes one — the limb pass, then the body pass — and inside each pass follows the native
/// order (drain before decay; regeneration and clamp before the internal/venom drains), so two
/// hosts replay the same numbers; the order is a definition here, not an accident.
/// </para>
///
/// <para>
/// The inputs are SAMPLED: they arrive on the 1 Hz snapshot and are held between reports, so
/// their own native decay between reports (the bandage's 1.25/s, the internal bleeding's
/// 0.045/s, the venom's move-toward-zero) is the reporting client's, not re-run here. This is a
/// sampled model of the bleeding family, not a second copy of the whole body.
/// </para>
/// </summary>
public sealed class PlayerBleedingState
{
	/// <summary>The clotting base of <c>Body.cs:3522</c>.</summary>
	private const float ClottingBase = 0.025f;

	/// <summary>The regeneration base of <c>Body.cs:19</c>.</summary>
	private const float RegenBase = 0.035f;

	/// <summary>The internal-bleeding drain of <c>Body.cs:2799</c>.</summary>
	private const float InternalBleedDrain = 0.0057f;

	/// <summary>The venom drain divisor of <c>Body.cs:2861</c>.</summary>
	private const float VenomDrainDivisor = 500f;

	/// <summary>The bandage's own decay of <c>Limb.cs:524</c> (1.25 units/s while a bandage is on).</summary>
	private const float BandageDecayPerSecond = 1.25f;

	/// <summary>The clamp of the native internal bleeding (<c>Body.cs</c> IntBleedingClamped).</summary>
	private const float InternalBleedingCap = 25f;

	/// <summary>The regeneration target and the native blood-volume clamp (<c>Body.cs:688-695</c>).</summary>
	private const float RegenTarget = 100f;
	private const float BloodVolumeMin = -100f;
	private const float BloodVolumeMax = 200f;

	private readonly float[] _bleed;
	private readonly float[] _skinHealth;
	private readonly float[] _bandageSlow;
	private readonly bool[] _blocked;
	private readonly bool[] _hasShrapnel;
	private readonly bool[] _dismembered;
	private readonly float _healingRate;
	private readonly float _bleedRate;
	private float _bloodVolume;
	private float _viscosity;
	private float _venom;
	private float _hunger;
	private float _internalBleeding;

	private PlayerBleedingState(int limbCount, float healingRate, float bleedRate)
	{
		_bleed = new float[limbCount];
		_skinHealth = new float[limbCount];
		_bandageSlow = new float[limbCount];
		_blocked = new bool[limbCount];
		_hasShrapnel = new bool[limbCount];
		_dismembered = new bool[limbCount];
		_healingRate = healingRate;
		_bleedRate = bleedRate;
	}

	/// <summary>The body's blood volume in the game's units.</summary>
	public float BloodVolume => _bloodVolume;

	/// <summary>The number of limbs the state covers (the snapshot's highest index + 1).</summary>
	public int LimbCount => _bleed.Length;

	/// <summary>
	/// The body-level bleeding speed the panel's "l/m" line reads: every limb's unblocked
	/// bleed plus the internal bleeding, minus the regeneration, floored at zero
	/// (<c>Body.cs:2800-2822</c>). Derived, never owned — it is a view of the two owned
	/// values and the inputs.
	/// </summary>
	public float TotalBleedSpeed
	{
		get
		{
			var bleedingSpeedMultiplier = BleedingSpeedMultiplier();
			var total = InternalBleedingClamped() * InternalBleedDrain;
			for (var i = 0; i < _bleed.Length; i++)
			{
				if (_dismembered[i])
				{
					continue;
				}

				// Body.cs:2807 — the limb's bleed through its own speed multiplier (the same
				// body-wide value the drain uses), and a tourniquet blocks it entirely.
				total += (_blocked[i] ? 0f : _bleed[i]) * bleedingSpeedMultiplier;
			}

			total -= RegenPerSecond();
			return total < 0f ? 0f : total;
		}
	}

	/// <summary>
	/// Seed a state from a full character snapshot: the outputs AND the inputs. This is the
	/// only place the owned values are taken from a report — the service calls it once per
	/// player (the first snapshot it sees of that player, and after every authoritative
	/// event, which the host itself wrote into the snapshot).
	/// </summary>
	public static PlayerBleedingState Seed(CharacterDataMsg data, float healingRate, float bleedRate)
	{
		var state = new PlayerBleedingState(LimbCountOf(data), healingRate, bleedRate);
		state.ApplyAuthority(data);
		state.ApplyInputs(data);
		return state;
	}

	/// <summary>
	/// Take the OWNED values from an authoritative snapshot: the host's own write after an
	/// event it arbitrated (a bite, a limb latch, a heal, a medical operation), or the first
	/// snapshot of a reconnect. Never called for a plain 1 Hz report.
	/// </summary>
	public void ApplyAuthority(CharacterDataMsg data)
	{
		_bloodVolume = FiniteOr(data.Health?.BloodVolume ?? _bloodVolume, _bloodVolume);
		for (var i = 0; i < data.Limbs.Count; i++)
		{
			var limb = data.Limbs[i];
			if (limb.Index < 0 || limb.Index >= _bleed.Length)
			{
				continue;
			}

			_bleed[limb.Index] = FiniteOr(limb.BleedAmount, _bleed[limb.Index]);
		}
	}

	/// <summary>
	/// Take the INPUTS from the latest snapshot. They change with the player's own state
	/// (dirty bandages, tourniquets taken off, hunger) and are not the host's to own.
	/// </summary>
	public void ApplyInputs(CharacterDataMsg data)
	{
		_viscosity = FiniteOr(data.Health?.BloodViscosity ?? _viscosity, _viscosity);
		_venom = FiniteOr(data.Health?.VenomCurrent ?? _venom, _venom);
		_hunger = FiniteOr(data.Health?.Hunger ?? _hunger, _hunger);
		_internalBleeding = FiniteOr(data.Health?.InternalBleeding ?? _internalBleeding, _internalBleeding);

		for (var i = 0; i < data.Limbs.Count; i++)
		{
			var limb = data.Limbs[i];
			if (limb.Index < 0 || limb.Index >= _bleed.Length)
			{
				continue;
			}

			_skinHealth[limb.Index] = FiniteOr(limb.SkinHealth, _skinHealth[limb.Index]);
			_bandageSlow[limb.Index] = FiniteOr(limb.BandageSlowAmount, _bandageSlow[limb.Index]);
			_blocked[limb.Index] = limb.BlockedBleeding;
			_hasShrapnel[limb.Index] = limb.Shrapnel > 0;
			_dismembered[limb.Index] = limb.Dismembered;
		}
	}

	/// <summary>
	/// Advance the owned values by <paramref name="dt"/> seconds with the game's rules, in the
	/// native intra-update order: the limb pass drains the body with the limb's PRE-decay amount
	/// and then decays it (<c>Limb.cs:500</c> precedes <c>Limb.cs:524</c>), and the body pass
	/// regenerates and clamps (<c>Body.cs:688-696</c>) before the internal-bleeding and venom
	/// drains (<c>Body.cs:2799</c>, <c>Body.cs:2861</c>).
	/// </summary>
	public void Advance(float dt)
	{
		if (dt <= 0f)
		{
			return;
		}

		var bleedingSpeedMultiplier = BleedingSpeedMultiplier();
		var clotting = ClottingSpeed();

		for (var i = 0; i < _bleed.Length; i++)
		{
			if (_dismembered[i])
			{
				continue;
			}

			// Limb.cs:500 — the limb's own contribution to the blood loss, through the
			// body-wide multiplier and the tourniquet's block, BEFORE this limb's decay.
			_bloodVolume -= (_blocked[i] ? 0f : _bleed[i]) * bleedingSpeedMultiplier * dt;

			// Limb.cs:524 — the decay, then the shrink of the clamp's upper bound.
			var decay = _hasShrapnel[i] ? 0f : dt * clotting * _healingRate;
			var bandage = _bandageSlow[i] > 0f ? dt * BandageDecayPerSecond : 0f;
			var limit = 100f - _skinHealth[i];
			_bleed[i] = Clamp(_bleed[i] - decay - bandage, 0f, limit);
		}

		// Body.cs:688-696 — regeneration toward 100 and the native clamp, before the drains.
		_bloodVolume = MoveTowards(_bloodVolume, RegenTarget, dt * RegenPerSecond());
		_bloodVolume = Clamp(_bloodVolume, BloodVolumeMin, BloodVolumeMax);

		// Body.cs:2799 / 2861 — the internal and venom drains (the venom only drains while it is
		// positive natively; a negative value must never ADD blood).
		_bloodVolume -= InternalBleedingClamped() * dt * InternalBleedDrain;
		if (_venom > 0f)
		{
			_bloodVolume -= _venom * dt / VenomDrainDivisor;
		}

		if (!IsFinite(_bloodVolume))
		{
			// A non-finite input slipped past ApplyInputs (a broken run setting, for example):
			// the tick must not poison the state or throw in MoveTowards.
			_bloodVolume = RegenTarget;
		}
	}

	/// <summary>
	/// One limb's owned bleed amount, by its native index. False for a limb the state does
	/// not cover or one that is dismembered (its value no longer drains and must not be
	/// written back).
	/// </summary>
	public bool TryGetBleed(int limbIndex, out float bleedAmount)
	{
		if (limbIndex < 0 || limbIndex >= _bleed.Length)
		{
			bleedAmount = 0f;
			return false;
		}

		bleedAmount = _bleed[limbIndex];
		return !_dismembered[limbIndex];
	}

	/// <summary>The bleeding speed multiplier of <c>Body.cs:3523</c>.</summary>
	private float BleedingSpeedMultiplier() =>
		(0.01f + Clamp01(Remap(_viscosity, -100f, 0f, 0.01f, 0f))) * _bleedRate;

	/// <summary>The clotting speed of <c>Body.cs:3522</c>.</summary>
	private float ClottingSpeed() =>
		ClottingBase
		* Clamp01(Remap(_viscosity, -100f, 0f, 0f, 1f))
		* Clamp01(1f - _venom / 20f);

	/// <summary>The regeneration speed of <c>Body.cs:19</c>.</summary>
	private float RegenPerSecond() => RegenBase * Math.Max(_hunger * 0.01f, 0f) * _healingRate;

	/// <summary>
	/// The native internal-bleeding clamp (<c>Body.cs</c> IntBleedingClamped): the drain
	/// reads at most 25 units of the reported value.
	/// </summary>
	private float InternalBleedingClamped() => Clamp(_internalBleeding, 0f, InternalBleedingCap);

	private static int LimbCountOf(CharacterDataMsg data)
	{
		var count = 0;
		foreach (var limb in data.Limbs)
		{
			if (limb.Index >= count)
			{
				count = limb.Index + 1;
			}
		}

		return count;
	}

	private static float Remap(float value, float from1, float to1, float from2, float to2) =>
		(value - from1) / (to1 - from1) * (to2 - from2) + from2;

	private static float Clamp(float value, float min, float max) =>
		value < min ? min : value > max ? max : value;

	private static float Clamp01(float value) => Clamp(value, 0f, 1f);

	/// <summary>A reported number is adopted only when it is finite; a NaN/infinity is a corrupt claim and keeps the previous value.</summary>
	private static float FiniteOr(float value, float fallback) => IsFinite(value) ? value : fallback;

	/// <summary>net48 has no <c>float.IsFinite</c>.</summary>
	private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

	private static float MoveTowards(float current, float target, float maxDelta)
	{
		if (!IsFinite(current))
		{
			return RegenTarget;
		}

		if (!IsFinite(maxDelta) || maxDelta < 0f)
		{
			return current;
		}

		if (!IsFinite(target))
		{
			target = RegenTarget;
		}

		var delta = target - current;
		if (Math.Abs(delta) <= maxDelta)
		{
			return target;
		}

		return current + Math.Sign(delta) * maxDelta;
	}
}
