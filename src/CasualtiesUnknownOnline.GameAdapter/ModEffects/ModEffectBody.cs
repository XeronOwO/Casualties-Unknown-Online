using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.ModEffects;

/// <summary>
/// The named body operations, implemented against the live game body. This type and
/// <see cref="ModEffectLimb"/> beside it are the ONLY place a mod-authored effect's
/// reach turns into a game write, which is what keeps the contract free of game
/// types and the write set enumerable.
///
/// <para>
/// Two implementation rules the census forced, both of them the game's own
/// semantics rather than a CUO approximation. The timed body goes through the
/// game's own <c>CoUtils</c> clock, so it ticks immediately and then once a game
/// second and a repeat under a live key extends rather than replaces. And the
/// component doses go through the game's own components. The one place this file
/// replaces a game helper is the get-or-add of those components: the game reaches
/// them through Unity.VisualScripting's <c>ComponentHolderProtocol</c>, which is
/// not one of the assemblies this project references, so the equivalent is done
/// with Unity's own <c>GetComponent</c> / <c>AddComponent</c> on the body's own
/// <c>GameObject</c> — which is the same object the game's own
/// <c>body.TryGetComponent&lt;Painkillers&gt;</c> reads in naloxone's branch, so the
/// two agree on where the component lives.
/// </para>
/// </summary>
internal sealed class ModEffectBody(string modId, Body body, ILogger log) : IModEffectBody
{
	private readonly string _modId = modId;
	private readonly Body _body = body;
	private readonly ILogger _log = log;
	private readonly Dictionary<Limb, ModEffectLimb> _wrappers = [];
	private IReadOnlyList<IModEffectLimb>? _limbs;

	/// <summary>The mod that authored the effect this body accessor serves — what its timed-effect keys are namespaced by.</summary>
	internal string ModId => _modId;

	/// <inheritdoc />
	public bool Alive => _body.alive;

	/// <inheritdoc />
	public bool Standing => _body.standing;

	/// <inheritdoc />
	public bool InCardiacArrest => _body.inCardiacArrest;

	/// <inheritdoc />
	public IReadOnlyList<IModEffectLimb> Limbs => _limbs ??= BuildLimbs();

	/// <inheritdoc />
	public void Drink(float ml) => _body.Drink(ml);

	/// <inheritdoc />
	public void Eat(float hunger, float weightGain) => _body.Eat(hunger, weightGain);

	/// <inheritdoc />
	public void Vomit() => _body.vomiter.Vomit();

	/// <inheritdoc />
	public void Burp() => _body.Burp();

	/// <inheritdoc />
	public void Ragdoll() => _body.Ragdoll();

	/// <inheritdoc />
	public void StartFibrillation() => _body.TryStartFibrillation(true);

	/// <inheritdoc />
	public void StartMindWipe()
	{
		if (_body.GetComponent<MindwipeScript>() == null) // Unity object — == (destroyed-object check)
		{
			_body.gameObject.AddComponent<MindwipeScript>();
		}
	}

	/// <inheritdoc />
	public void AddLimbVelocity(Vector2 velocity)
	{
		foreach (var limb in _body.limbs)
		{
			if (limb != null) // Unity object — != (destroyed-object check)
			{
				limb.rb.velocity += velocity;
			}
		}
	}

	/// <inheritdoc />
	public float GetVital(ModBodyVital vital) =>
		Accessor(vital) is { } access ? access.Read() : Undefined(vital, "read");

	/// <inheritdoc />
	public void AddVital(ModBodyVital vital, float amount)
	{
		if (Accessor(vital) is not { } access)
		{
			Undefined(vital, "add to");
			return;
		}

		access.Write(access.Read() + amount);
	}

	/// <inheritdoc />
	public void SetVital(ModBodyVital vital, float value)
	{
		if (Accessor(vital) is not { } access)
		{
			Undefined(vital, "set");
			return;
		}

		access.Write(value);
	}

	/// <inheritdoc />
	public void AddOpiate(float amount) => Painkillers().opiateAmount += amount;

	/// <inheritdoc />
	public void AddOpiateAntagonist(float amount) => Painkillers().antagonistAmount += amount;

	/// <inheritdoc />
	public void AddOpiateTolerance(float amount) => Painkillers().opiateTolerance += amount;

	/// <inheritdoc />
	public void TakeAntidepressant(float amount) => GetOrAdd<Antidepressants>().TakeDose(amount);

	/// <inheritdoc />
	public void AddSleepingPill(float amount) => GetOrAdd<SleepingPills>().amount += amount;

	/// <inheritdoc />
	public void StartTimedEffect(string key, float seconds, Action<IModEffectBody> tick)
	{
		// The key is namespaced because the game's clock is ONE flat table shared with
		// its own liquids: an unprefixed mod key could collide with a vanilla one, and a
		// collision there is silent — the duration merges and the FIRST registered tick
		// keeps running. Two callers must not be able to reach that state by accident.
		var namespaced = Namespaced(key);
		if (CoUtils.instance.DurationOf(namespaced) > 0f)
		{
			_log.LogDebug(
				"[LiquidContent] {ModId}'s timed effect {Key} is already running — its duration is extended and the tick registered first keeps running.",
				_modId, key);
		}

		CoUtils.instance.DoTimedOp(namespaced, () => Tick(key, tick), seconds);
	}

	/// <inheritdoc />
	public float TimedEffectRemaining(string key) => CoUtils.instance.DurationOf(Namespaced(key));

	/// <inheritdoc />
	public float TimedEffectHighest(string key) => CoUtils.instance.HighestDurationOf(Namespaced(key));

	/// <inheritdoc />
	public void PlaySound(string clipId) => Sound.Play(clipId, _body.transform.position, false, true, null, 1f, 1f, false, false);

	/// <summary>
	/// The accessor for one limb, shared with the wrapper the context hands out, so
	/// a limb reached through <see cref="Limbs"/> and the same limb reached through
	/// <see cref="IModEffectLimb.ConnectedLimbs"/> are the same accessor.
	/// </summary>
	internal ModEffectLimb Wrap(Limb limb)
	{
		if (_wrappers.TryGetValue(limb, out var existing))
		{
			return existing;
		}

		var wrapper = new ModEffectLimb(limb, this, Array.IndexOf(_body.limbs, limb), _log);
		_wrappers.Add(limb, wrapper);
		return wrapper;
	}

	/// <summary>The namespaced form of one mod-authored timed-effect key.</summary>
	private string Namespaced(string key) => "cuo:" + _modId + ":" + key;

	/// <summary>
	/// One tick of a mod's timed body, guarded. The guard has to sit HERE and not only
	/// around the effect invocation that started it: the tick runs inside the game's own
	/// <c>CoUtils</c> coroutine, so an exception escaping it would kill that coroutine
	/// while leaving the key and its remaining duration in the game's own table — the
	/// entry would then never tick again and every later call under that key would only
	/// "extend" a body nobody is running. A throwing tick therefore costs that one tick,
	/// and the tick loop keeps its schedule.
	/// </summary>
	private void Tick(string key, Action<IModEffectBody> tick)
	{
		try
		{
			tick(this);
		}
		catch (Exception e)
		{
			_log.LogError(
				e,
				"[LiquidContent] {ModId}'s timed effect {Key} threw on a tick — that tick was skipped and the effect keeps its schedule.",
				_modId, key);
		}
	}

	private IReadOnlyList<IModEffectLimb> BuildLimbs()
	{
		var limbs = new List<IModEffectLimb>(_body.limbs.Length);
		foreach (var limb in _body.limbs)
		{
			if (limb != null) // Unity object — != (destroyed-object check)
			{
				limbs.Add(Wrap(limb));
			}
		}

		return limbs;
	}

	private Painkillers Painkillers() => GetOrAdd<Painkillers>();

	private T GetOrAdd<T>() where T : Component
	{
		var component = _body.GetComponent<T>();
		if (component == null) // Unity object — == (destroyed-object check)
		{
			component = _body.gameObject.AddComponent<T>();
		}

		return component;
	}

	/// <summary>
	/// The one mapping table. Read and write live together on purpose: three separate
	/// switches (get / add / set) could drift into disagreeing about which game field a
	/// member names, and the gate that reconciles the enum against this table reads it
	/// as one.
	/// </summary>
	private (Func<float> Read, Action<float> Write)? Accessor(ModBodyVital vital) => vital switch
	{
		ModBodyVital.Thirst => (() => _body.thirst, value => _body.thirst = value),
		ModBodyVital.Hunger => (() => _body.hunger, value => _body.hunger = value),
		ModBodyVital.Temperature => (() => _body.temperature, value => _body.temperature = value),
		ModBodyVital.Happiness => (() => _body.happiness, value => _body.happiness = value),
		ModBodyVital.Sickness => (() => _body.sicknessAmount, value => _body.sicknessAmount = value),
		ModBodyVital.Weight => (() => _body.weightOffset, value => _body.weightOffset = value),
		ModBodyVital.Stamina => (() => _body.stamina, value => _body.stamina = value),
		ModBodyVital.Energy => (() => _body.energy, value => _body.energy = value),
		ModBodyVital.Consciousness => (() => _body.consciousness, value => _body.consciousness = value),
		ModBodyVital.Adrenaline => (() => _body.adrenaline, value => _body.adrenaline = value),
		ModBodyVital.Shock => (() => _body.shock, value => _body.shock = value),
		ModBodyVital.Caffeine => (() => _body.caffeinated, value => _body.caffeinated = value),
		ModBodyVital.Dirtiness => (() => _body.dirtyness, value => _body.dirtyness = value),
		ModBodyVital.BrainHealth => (() => _body.brainHealth, value => _body.brainHealth = value),
		ModBodyVital.BloodOxygen => (() => _body.bloodOxygen, value => _body.bloodOxygen = value),
		ModBodyVital.BloodViscosity => (() => _body.bloodViscosity, value => _body.bloodViscosity = value),
		ModBodyVital.BloodVolume => (() => _body.bloodVolume, value => _body.bloodVolume = value),
		ModBodyVital.InternalBleeding => (() => _body.internalBleeding, value => _body.internalBleeding = value),
		ModBodyVital.Stroke => (() => _body.strokeAmount, value => _body.strokeAmount = value),
		ModBodyVital.SepticShock => (() => _body.septicShock, value => _body.septicShock = value),
		ModBodyVital.RadiationSickness => (() => _body.radiationSickness, value => _body.radiationSickness = value),
		ModBodyVital.Venom => (() => _body.venomTotal, value => _body.venomTotal = value),
		ModBodyVital.AntibioticImmunity => (() => _body.antibioticImmunityTime, value => _body.antibioticImmunityTime = value),
		ModBodyVital.ClawRegrowth => (() => _body.clawRegrowTime, value => _body.clawRegrowTime = value),
		ModBodyVital.BrainGrowSickness => (() => _body.brainGrowSickness, value => _body.brainGrowSickness = value),
		ModBodyVital.HeartRate => (() => _body.heartRate, value => _body.heartRate = value),
		ModBodyVital.Fibrillation => (() => _body.fibrillationProgress, value => _body.fibrillationProgress = value),
		ModBodyVital.MedicineBloodPressure => (() => _body.bloodPressureChangeFromMedicine, value => _body.bloodPressureChangeFromMedicine = value),
		ModBodyVital.RespiratoryRate => (() => _body.respiratoryRate, value => _body.respiratoryRate = value),
		ModBodyVital.StimulantMultiplier => (() => _body.stimulantMultiplier, value => _body.stimulantMultiplier = value),
		ModBodyVital.ShakeIntensity => (() => _body.miscShakeIntensity, value => _body.miscShakeIntensity = value),
		ModBodyVital.OverdoseIndex => (() => _body.overdoseIndex, value => _body.overdoseIndex = (int)value),
		_ => null,
	};

	private float Undefined(ModBodyVital vital, string action)
	{
		_log.LogWarning(
			"[LiquidContent] {ModId} asked to {Action} the undefined body vital {Vital} — refused.",
			_modId, action, vital);
		return 0f;
	}
}
