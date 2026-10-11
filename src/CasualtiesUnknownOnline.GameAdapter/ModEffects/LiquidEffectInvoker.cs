using System;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.ModEffects;

/// <summary>
/// The bridge between the game's own liquid delegates and a mod's registered
/// function — what the two delegates installed on a mod liquid's
/// <c>LiquidType</c> actually call.
///
/// <para>
/// The lookup happens per call, never at build time: the vanilla registry is
/// injected when the game's own <c>Liquids.Registry</c> first exists, which is
/// unrelated to when a mod registers, so a captured delegate would make the
/// registration order load-bearing for nothing. It also keeps the whole family in
/// one place — the game's own drink, the CUO cross-player drink
/// (<see cref="NativeDrinkApply"/>), the CUO topical/injection appliers and CUO's
/// own custom-world-byte drink all reach the same installed delegate, so none of
/// them needs a second rule.
/// </para>
///
/// <para>
/// The invocation is guarded, and that is not a formality: this call runs INSIDE
/// the game's own <c>WaterContainerItem.Drink</c> / <c>ApplyToLimb</c> /
/// <c>Inject</c> frame, so an exception escaping a mod's effect would take the
/// game's own action down with it. A throwing effect costs the effect, is logged
/// with the mod id, and the action completes.
/// </para>
/// </summary>
internal sealed class LiquidEffectInvoker(
	ModLiquidEffectStore effectStore,
	ISessionControl session,
	ILogger log)
{
	private readonly ModLiquidEffectStore _effectStore = effectStore;
	private readonly ISessionControl _session = session;
	private readonly ILogger _log = log;

	/// <summary>The game's own <c>onDrink</c> half: a body and a volume.</summary>
	internal void Drink(string liquidId, float ml, Body body) =>
		Invoke(liquidId, ml, body, null, limbUse: false);

	/// <summary>The game's own <c>onHealthUse</c> half: a limb and a volume — the same delegate for the topical apply and the injection.</summary>
	internal void ApplyToLimb(string liquidId, float ml, Limb limb) =>
		Invoke(liquidId, ml, limb == null ? null : limb.body, limb, limbUse: true);

	private void Invoke(string liquidId, float ml, Body? body, Limb? limb, bool limbUse)
	{
		var half = limbUse ? "limb use" : "drink";
		var registration = limbUse ? _effectStore.FindLimbUse(liquidId) : _effectStore.FindDrink(liquidId);
		if (registration is null)
		{
			// No mod authored this half. The action still completes — that is the
			// inert outcome mod-declared-behaviour-with-no-function.md settled, and
			// the line is what makes "nothing happened" observable at the moment it
			// happens rather than only at load time.
			_log.LogWarning(
				"[LiquidContent] {Id} was used ({Half}, {Ml:F1} ml) with no effect — no mod registered one for that half.",
				liquidId, half, ml);
			return;
		}

		if (body == null) // Unity object — == (destroyed-object check)
		{
			_log.LogWarning(
				"[LiquidContent] {ModId}'s {Half} effect for {Id} was skipped: the body it landed on is gone.",
				registration.ModId, half, liquidId);
			return;
		}

		try
		{
			var context = new ModLiquidEffectContext(
				registration.ModId, liquidId, ml, body, limb, _session.LocalSteamId, _log);
			registration.Effect(context);
		}
		catch (Exception e)
		{
			_log.LogError(
				e,
				"[LiquidContent] {ModId}'s {Half} effect for {Id} threw — the action completed without it.",
				registration.ModId, half, liquidId);
		}
	}
}
