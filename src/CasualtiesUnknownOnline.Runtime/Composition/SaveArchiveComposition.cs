using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.Runtime.Composition;

/// <summary>
/// The CUO world archive (docs/architecture/save-archive-format.md): the archive
/// repository is the ONLY writer of the save system — the native <c>save.sv</c> is
/// never written and never read (decisions 164/165) — together with the snapshot
/// encoder, the two halves of a consistent cut, the Game Adapter's
/// starting-supplies report and the save service that arms the archive policy.
/// It registers last: the save layer reads the world, item, character and
/// transport facts every other feature owns. A null root keeps the whole system
/// disabled, which is the test composition root's default.
/// </summary>
internal static class SaveArchiveComposition
{
	/// <summary>Registers the archive repository, the cut/restore facts and the save service.</summary>
	internal static void AddSaveArchive(IServiceCollection services, string? savesRoot, string? gameBuild)
	{
		services.AddSingleton<SaveArchiveWriter>();
		services.AddSingleton<SaveArchiveReader>();
		services.AddSingleton<WorldSnapshotEncoder>();
		// The consistent cut's two observation halves: the Runtime probe reads the
		// CUO-owned in-flight state, the restore audit carries a restore's
		// live-world write back to the caller that started it.
		services.AddSingleton<WorldCutTransientProbe>();
		services.AddSingleton<WorldRestoreAudit>();
		// The Game Adapter's starting-supplies report (S4.3): a broadcast point, not an
		// account — the adapter owns the decision and its once-per-body rule, and the
		// publisher/subscriber split keeps it from reading back its own report.
		services.AddSingleton<StartingSupplyAudit>();
		services.AddSingleton<IStartingSupplyControl>(p => p.GetRequiredService<StartingSupplyAudit>());
		services.AddSingleton<IStartingSupplyPublisher>(p => p.GetRequiredService<StartingSupplyAudit>());
		if (savesRoot is not null)
		{
			services.AddSingleton(p => new WorldRepository(
				savesRoot,
				p.GetRequiredService<ILogger<WorldRepository>>(),
				p.GetRequiredService<SaveArchiveWriter>(),
				p.GetRequiredService<SaveArchiveReader>()));
		}

		services.AddSingleton(p => new WorldSaveService(
			savesRoot is null ? null : p.GetRequiredService<WorldRepository>(),
			p.GetRequiredService<ISessionControl>(),
			p.GetRequiredService<ICharacterDataControl>(),
			p.GetRequiredService<ItemKernelAuthority>(),
			p.GetRequiredService<ITransportIdentity>(),
			p.GetRequiredService<WorldSnapshotEncoder>(),
			p.GetRequiredService<IWorldFactSource>(),
			p.GetRequiredService<ILoggerFactory>(),
			p.GetRequiredService<ILogger<WorldSaveService>>(),
			gameBuild,
			utcNow: null,
			// The Game Adapter registers the native world-fact reader through
			// extraRegistrations (it is the only layer that knows the game's keypad,
			// geyser and block-damage tables). It is optional by design: a build
			// without one captures and restores only the Runtime-owned facts, and
			// that gap is reported, never silent.
			nativeWorldFacts: p.GetService<INativeWorldFacts>(),
			// The transient policy's Runtime half (the pickup queue, the operation
			// sessions, the deferred creation reports): the game-side half is handed
			// in by the adapter at the frame-end seam.
			transients: p.GetRequiredService<WorldCutTransientProbe>(),
			// Carries a restore's live-world half back to the caller that started it.
			audit: p.GetRequiredService<WorldRestoreAudit>(),
			// The item domain's half of a restore: a mid-run cut's world items are
			// reconciled against the regenerated layer, so the restore path has to
			// arm/cancel that expectation.
			items: p.GetRequiredService<IItemControl>(),
			// The world-entity domain's half: the kernel's restored per-entity facts
			// are written at the same world-entry seam (and dropped for a layer-end
			// cut). The save layer only arms/cancels the expectation; the projection
			// owns the facts and the replay owns the write.
			worldEntities: p.GetRequiredService<IRestoredWorldEntitySource>(),
			// The archive's policy knobs (S4.4): the interval autosave and the backup
			// retention. The production plugin replaces this monitor with the BepInEx
			// config-backed one; the default keeps the frozen values of §7.
			options: p.GetRequiredService<IOptionsMonitor<SaveOptions>>()));
		services.AddSingleton<IWorldSaveControl>(p => p.GetRequiredService<WorldSaveService>());
	}
}
