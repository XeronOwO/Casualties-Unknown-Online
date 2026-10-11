using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.ModEffects;

/// <summary>
/// The named limb operations, implemented against the live game limb the native
/// call named. Like <see cref="ModEffectBody"/> beside it, this is a mapping and
/// nothing else: every member is the game's own field or the game's own method, and
/// no rule of CUO's own sits between a mod and the write it asked for.
/// </summary>
internal sealed class ModEffectLimb : IModEffectLimb
{
	private readonly Limb _limb;
	private readonly ModEffectBody _body;
	private readonly ILogger _log;
	private IReadOnlyList<IModEffectLimb>? _connected;

	internal ModEffectLimb(Limb limb, ModEffectBody body, int index, ILogger log)
	{
		_limb = limb;
		_body = body;
		Index = index;
		_log = log;
	}

	/// <inheritdoc />
	public int Index { get; }

	/// <inheritdoc />
	public string Name => _limb.fullName;

	/// <inheritdoc />
	public bool IsArm => _limb.isArm;

	/// <inheritdoc />
	public bool IsLeg => _limb.isLegLimb;

	/// <inheritdoc />
	public bool IsHead => _limb.isHead;

	/// <inheritdoc />
	public bool IsVital => _limb.isVital;

	/// <inheritdoc />
	public IModEffectBody Body => _body;

	/// <inheritdoc />
	public GameObject? NativeObject => _limb == null ? null : _limb.gameObject; // Unity object — == (destroyed-object check)

	/// <inheritdoc />
	public IReadOnlyList<IModEffectLimb> ConnectedLimbs => _connected ??= BuildConnected();

	/// <inheritdoc />
	public float GetVital(ModLimbVital vital) =>
		Accessor(vital) is { } access ? access.Read() : Undefined(vital, "read");

	/// <inheritdoc />
	public void AddVital(ModLimbVital vital, float amount)
	{
		if (Accessor(vital) is not { } access)
		{
			Undefined(vital, "add to");
			return;
		}

		access.Write(access.Read() + amount);
	}

	/// <inheritdoc />
	public void SetVital(ModLimbVital vital, float value)
	{
		if (Accessor(vital) is not { } access)
		{
			Undefined(vital, "set");
			return;
		}

		access.Write(value);
	}

	/// <inheritdoc />
	public void Disinfect(float amount) => _limb.SetDisinfect(amount);

	private IReadOnlyList<IModEffectLimb> BuildConnected()
	{
		var connected = new List<IModEffectLimb>(_limb.connectedLimbs.Length);
		foreach (var limb in _limb.connectedLimbs)
		{
			if (limb != null) // Unity object — != (destroyed-object check)
			{
				// Through the body's own wrapper table, so a limb reached here is the
				// same accessor as the one reached from IModEffectBody.Limbs.
				connected.Add(_body.Wrap(limb));
			}
		}

		return connected;
	}

	private (Func<float> Read, Action<float> Write)? Accessor(ModLimbVital vital) => vital switch
	{
		ModLimbVital.Pain => (() => _limb.pain, value => _limb.pain = value),
		ModLimbVital.MuscleHealth => (() => _limb.muscleHealth, value => _limb.muscleHealth = value),
		ModLimbVital.SkinHealth => (() => _limb.skinHealth, value => _limb.skinHealth = value),
		ModLimbVital.SkinHeal => (() => _limb.skinHealAmount, value => _limb.skinHealAmount = value),
		ModLimbVital.Infection => (() => _limb.infectionAmount, value => _limb.infectionAmount = value),
		ModLimbVital.BandageSlow => (() => _limb.bandageSlowAmount, value => _limb.bandageSlowAmount = value),
		ModLimbVital.Bleed => (() => _limb.bleedAmount, value => _limb.bleedAmount = value),
		_ => null,
	};

	private float Undefined(ModLimbVital vital, string action)
	{
		_log.LogWarning(
			"[LiquidContent] {ModId} asked to {Action} the undefined limb vital {Vital} — refused.",
			_body.ModId, action, vital);
		return 0f;
	}
}
