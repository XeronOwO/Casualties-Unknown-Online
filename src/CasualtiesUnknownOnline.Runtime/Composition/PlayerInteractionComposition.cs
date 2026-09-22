using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.DependencyInjection;

namespace CasualtiesUnknownOnline.Runtime.Composition;

/// <summary>
/// Cross-player interaction: the visibility oracle and the local-body capture seam
/// the verdicts answer through (both defaulted here and replaced by the Game
/// Adapter in the plugin), the shared medical-operation session envelope, the
/// interaction service itself with the carry/push domains it owns, the item-traffic
/// observer and the crafting domain that rides the item service's world-table
/// gateway.
/// </summary>
internal static class PlayerInteractionComposition
{
	/// <summary>Registers the interaction seams, the medical sessions, the interaction service, the item-traffic pump and crafting.</summary>
	internal static void AddPlayerInteraction(IServiceCollection services)
	{
		// Direct player-interaction visibility oracle. The base composition root
		// permits every pair; the plugin replaces it with the Game Adapter's
		// world-backed line-of-sight implementation in extraRegistrations.
		services.AddSingleton<IPlayerInteractionVisibility>(new AllowAllPlayerInteractionVisibility());
		// Local-body capture for the target-body verdict seam. The base root has no
		// game scene, so it captures nothing and the gate answers from this side's own
		// latest snapshot; the plugin replaces it with the Game Adapter's live capture.
		services.AddSingleton<ILocalCharacterCapture>(new UnavailableLocalCharacterCapture());
		// Remote medical operation session domain: generic start/update/end/cancel
		// plus host-side reservations and timeout/disconnect cleanup. Stage 1 uses
		// it for real-time injection; later stages reuse the same session envelope.
		services.AddSingleton<MedicalOperationSessionService>();
		services.AddSingleton<IMedicalOperationControl>(p => p.GetRequiredService<MedicalOperationSessionService>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<MedicalOperationSessionService>());

		// Direct player interaction (cross-player inventory take) — depends on the
		// session, character-data and item control surfaces; no pump.
		services.AddSingleton<PlayerInteractionService>();
		services.AddSingleton<IPlayerInteractionControl>(p => p.GetRequiredService<PlayerInteractionService>());
		// Item-traffic observer: logs the per-window item-message volume (no
		// batching/rate-limit — observe first, optimize only if the numbers hurt).
		services.AddSingleton<ItemTrafficPump>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<ItemTrafficPump>());
		// Crafting domain: the one-operation-one-report apply + the recipe
		// unlock (no pump, not an ICuoService — it only reacts to calls and
		// messages; ItemService's crafting seams are its world-table gateway).
		services.AddSingleton<CraftSyncService>();
		services.AddSingleton<ICraftControl>(p => p.GetRequiredService<CraftSyncService>());
	}
}
