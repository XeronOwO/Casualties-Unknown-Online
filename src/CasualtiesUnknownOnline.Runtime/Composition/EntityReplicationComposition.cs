using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Tutorial;
using Microsoft.Extensions.DependencyInjection;

namespace CasualtiesUnknownOnline.Runtime.Composition;

/// <summary>
/// The replicated entity/character state: the player and enemy kernel projections,
/// the two 20 Hz state exchanges with their join announcements, the tutorial-claw
/// presentation stream, and the SteamID-keyed character table with the read models
/// the Online UI serves from it. These services own replicated state — not world
/// tables and not the item kernel — so they register as one family, before the
/// world module whose channels they read.
/// </summary>
internal static class EntityReplicationComposition
{
	/// <summary>Registers the entity/enemy streams, the claw presentation stream and the character-data read models.</summary>
	internal static void AddEntityReplication(IServiceCollection services)
	{
		// Entity-sync domain: the entity table, the sync decisions and the
		// 20 Hz state exchange + join announcements. It reads the session's
		// control surface, so it runs after the session in the Update order.
		services.AddSingleton<PlayerKernelStatusProjection>();
		services.AddSingleton<EntitySyncService>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<EntitySyncService>());
		services.AddSingleton<IEntitySyncControl>(p => p.GetRequiredService<EntitySyncService>());
		// Enemy-sync domain: host-authoritative enemy snapshots (the host
		// publishes the simulated enemies, this broadcasts at 20 Hz + the
		// world-entry full snapshot; the guest receives for its render copies).
		services.AddSingleton<EnemyKernelProjection>();
		services.AddSingleton<EnemyKernelRestoreProjection>();
		services.AddSingleton<EnemySyncService>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<EnemySyncService>());
		services.AddSingleton<IEnemySyncControl>(p => p.GetRequiredService<EnemySyncService>());
		// Tutorial-claw presentation stream (host-authoritative 20 Hz claw visual;
		// no course/prop state — the Game Adapter owns the capture/apply).
		services.AddSingleton<TutorialClawService>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<TutorialClawService>());
		services.AddSingleton<ITutorialClawControl>(p => p.GetRequiredService<TutorialClawService>());
		// Character-data domain: the SteamID-keyed reconnect table. It is
		// in-memory and SESSION-scoped by construction — the world archive is the
		// only persistent copy of a character (decisions 162/164) and feeds this
		// table at a restore through the claim (S4 scope 6 retired the separate
		// character-data disk store as a second source of truth). No pump, not an
		// ICuoService — it only reacts to reports and handshakes.
		services.AddSingleton<PlayerKernelLimbProjection>();
		services.AddSingleton<PlayerKernelRestoreProjection>();
		services.AddSingleton<CharacterDataStore>();
		services.AddSingleton<ICharacterDataControl>(p => p.GetRequiredService<CharacterDataStore>());
		// Remote-vitals cache: the Online UI's read-only view of the latest
		// character snapshots (no pump, not an ICuoService — only reacts to the
		// character-data stream and session end).
		services.AddSingleton<RemoteVitalsService>();
		// Remote-inventory cache: the Online UI's read-only view of the latest
		// carried/worn item snapshots (same events and lifecycle as vitals).
		services.AddSingleton<RemoteInventoryService>();
		// Registry-backed unified remote-presentation read model: the same
		// character-data stream projected into RemoteCharacterPresentation.State
		// under the global ProjectionHealthCoordinator contract.
		services.AddSingleton<RemoteCharacterPresentationStore>();
	}
}
