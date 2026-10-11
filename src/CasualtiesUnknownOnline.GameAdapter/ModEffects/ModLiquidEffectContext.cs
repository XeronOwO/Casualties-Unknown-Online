using CasualtiesUnknownOnline.Abstractions;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.ModEffects;

/// <summary>
/// The live context handed to one registered effect invocation. It is created per
/// native call, on the affected player's own client, over the very body and limb
/// the game's own delegate received — the framework never resolves, substitutes or
/// re-targets either of them.
///
/// <para>
/// Every accessor is a Game Adapter type, so the contract's public surface carries
/// no game type and only this folder knows what a <c>Body</c> is.
/// </para>
/// </summary>
internal sealed class ModLiquidEffectContext : IModLiquidEffectContext
{
	private readonly Body _body;

	internal ModLiquidEffectContext(
		string modId,
		string liquidId,
		float amountMl,
		Body body,
		Limb? limb,
		ulong ownerSteamId,
		ILogger log)
	{
		ModId = modId;
		LiquidId = liquidId;
		AmountMl = amountMl;
		OwnerSteamId = ownerSteamId;
		_body = body;

		var operations = new ModEffectBody(modId, body, log);
		Body = operations;
		Limb = limb == null ? null : operations.Wrap(limb);
	}

	/// <summary>The mod that authored this effect — what its timed-effect keys are namespaced by.</summary>
	internal string ModId { get; }

	/// <inheritdoc />
	public string LiquidId { get; }

	/// <inheritdoc />
	public float AmountMl { get; }

	/// <inheritdoc />
	public ulong OwnerSteamId { get; }

	/// <inheritdoc />
	public IModEffectBody Body { get; }

	/// <inheritdoc />
	public IModEffectLimb? Limb { get; }

	/// <inheritdoc />
	public GameObject? NativeObject => _body == null ? null : _body.gameObject; // Unity object — == (destroyed-object check)
}
