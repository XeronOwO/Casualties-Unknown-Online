using System;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.ModEffects;

/// <summary>
/// The bridge between the game's own item delegates and a mod's registered
/// function — what the two delegates installed on a mod item's <c>ItemInfo</c>
/// actually call.
///
/// <para>
/// The lookup happens per call, never at build time: the vanilla item table is
/// injected when the game's own <c>Item.GlobalItems</c> first exists, which is
/// unrelated to when a mod registers, so a captured delegate would make the
/// registration order load-bearing for nothing. It also keeps the whole family in
/// one place — the game's own use and limb-use call sites, and CUO's cross-player
/// limb-tool chain, which runs the item's own <c>useLimbAction</c> on the treated
/// player's client, all reach the same installed delegate, so none of them needs a
/// second rule.
/// </para>
///
/// <para>
/// The invocation is guarded, and that is not a formality: this call runs INSIDE the
/// game's own <c>Body.UseItem</c> / wound-view frame, so an exception escaping a
/// mod's action would take the game's own action down with it. A throwing action
/// costs the action, is logged with the mod id, and the gesture completes.
/// </para>
/// </summary>
internal sealed class ItemActionInvoker(
	ModItemEffectStore effectStore,
	ISessionControl session,
	ILogger log)
{
	private readonly ModItemEffectStore _effectStore = effectStore;
	private readonly ISessionControl _session = session;
	private readonly ILogger _log = log;

	/// <summary>The game's own <c>useAction</c> half, as the delegate the built <c>ItemInfo</c> carries.</summary>
	internal ItemInfo.Use UseAction(string itemId) =>
		(body, item) => Invoke(itemId, body, null, item, limbUse: false);

	/// <summary>The game's own <c>useLimbAction</c> half, as the delegate the built <c>ItemInfo</c> carries.</summary>
	internal ItemInfo.UseLimb LimbAction(string itemId) =>
		(limb, item) => Invoke(itemId, limb == null ? null : limb.body, limb, item, limbUse: true);

	private void Invoke(string itemId, Body? body, Limb? limb, Item? item, bool limbUse)
	{
		var half = limbUse ? "limb-use" : "use";
		var registration = limbUse ? _effectStore.FindLimbUse(itemId) : _effectStore.FindUse(itemId);
		if (registration is null)
		{
			// No mod authored this half. The gesture still completes — that is the
			// inert outcome mod-declared-behaviour-with-no-function.md settled, and
			// the line is what makes "nothing happened" observable at the moment it
			// happens rather than only at load time.
			_log.LogWarning(
				"[ItemContent] {Id} was used ({Half}) with no effect — no mod registered one for that half.",
				itemId, half);
			return;
		}

		if (body == null || item == null) // Unity objects — == (destroyed-object check)
		{
			_log.LogWarning(
				"[ItemContent] {ModId}'s {Half} action for {Id} was skipped: the body or the item it landed on is gone.",
				registration.ModId, half, itemId);
			return;
		}

		try
		{
			var context = new ModItemActionContext(
				registration.ModId, itemId, item, body, limb, _session.LocalSteamId, _log);
			registration.Action(context);
		}
		catch (Exception e)
		{
			_log.LogError(
				e,
				"[ItemContent] {ModId}'s {Half} action for {Id} threw — the gesture completed without it.",
				registration.ModId, half, itemId);
		}
	}
}
