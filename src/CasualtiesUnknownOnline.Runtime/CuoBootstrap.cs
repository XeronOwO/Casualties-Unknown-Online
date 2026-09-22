using System;
using System.IO;
using CasualtiesUnknownOnline.Runtime.Composition;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Diagnostics;
using CasualtiesUnknownOnline.Runtime.Logging;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using ManualLogSource = BepInEx.Logging.ManualLogSource;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime;

/// <summary>
/// Assembles the CUO DI container. The plugin calls this once in Awake, then
/// forwards ICuoService lifecycle notifications from BepInEx/Unity callbacks
/// (architecture.md §5.5). Microsoft.Extensions provides the plumbing; BepInEx/Unity
/// own the lifecycle and main loop.
///
/// <para>
/// The registrations themselves live in feature composition modules — one per feature
/// family (container seams, networking, session policy, entity replication, world,
/// presentation, console, item tables and item service, player interaction, mods, the
/// save archive) under <c>Composition/</c>, plus the two modules earlier cycles
/// extracted beside their feature: kernel replication
/// (<c>Session/Items/KernelReplicationComposition</c>) and the content vocabulary
/// (<c>Session/Content/ContentVocabularyComposition</c>). Each is named for what it
/// owns rather than for the lines it holds. This file owns three things and nothing
/// else: the registration ORDER, the DI-cycle guard, and the startup-failure log.
/// </para>
/// </summary>
public static class CuoBootstrap
{
	/// <summary>
	/// The built container (set by the plugin after BuildServiceProvider; null
	/// before). A read-only diagnostics seam for runtime tools (HotRepl etc.) —
	/// business code must keep using constructor injection; this only answers
	/// "what is the running service graph" (same pattern as PatchBridge.Impl:
	/// the static seam is a query, never a state store).
	/// </summary>
	public static IServiceProvider? Services { get; set; }

	/// <summary>
	/// Builds the container. <paramref name="extraRegistrations"/> lets the plugin
	/// register the Game Adapter implementation (CUO.GameAdapter references the
	/// game, so the Runtime cannot reference it back).
	/// <paramref name="savesRoot"/> is the optional world-archive root
	/// (<c>&lt;CUO data root&gt;/cuo/saves</c> in production); null disables the save system.
	/// <paramref name="gameBuild"/> is the running game version recorded as a save's provenance.
	/// </summary>
	public static ServiceProvider BuildServiceProvider(
		ManualLogSource bepinExLogSource, string logDirectory, string? legacyLogPath = null,
		string? modStateFile = null, string? hostBanFile = null,
		string? savesRoot = null, string? gameBuild = null,
		Action<IServiceCollection>? extraRegistrations = null)
	{
		var services = new ServiceCollection();

		// The registration order below IS the contract: it is the order
		// GetServices<ICuoService>() hands the plugin as the update order, and the
		// order the IEnumerable<> registrations keep (the log sinks, the content
		// sources). Each call therefore stands exactly where its block stood when
		// this file held the registrations inline. Two modules expose two entry
		// points for that reason: networking's transport registers before the
		// session control and its packet plane after it, and the item tables
		// register on either side of the kernel replication block.
		ContainerComposition.AddContainerSeams(services, bepinExLogSource, logDirectory, legacyLogPath);
		// Registration order determines GetServices<ICuoService>() order: SteamService
		// before SteamTransport (transport reads steam readiness), SessionService
		// before EntitySyncService/PacketDispatcher (they read the session control
		// surface — resolved after the session is built).
		NetworkingComposition.AddTransport(services);
		SessionComposition.AddSessionPolicy(services);
		NetworkingPacketPlaneComposition.AddPacketPlane(services, hostBanFile);
		EntityReplicationComposition.AddEntityReplication(services);
		WorldComposition.AddWorld(services);
		PresentationComposition.AddCommunication(services);
		// Content-id vocabulary + its completion stages, in its own composition file
		// (the canonical namespace:path catalog the console completes resource
		// arguments from, plus the store a mod's completion stages land in).
		ContentVocabularyComposition.AddContentVocabulary(services);
		ConsoleComposition.AddConsole(services);
		ItemComposition.AddItemTables(services);
		// Kernel replication: the Application layer's protocol surface, the Runtime
		// services it reads, and the ports between them.
		KernelReplicationComposition.AddKernelReplication(services);
		ItemComposition.AddItemService(services);
		PlayerInteractionComposition.AddPlayerInteraction(services);
		ModComposition.AddMods(services, modStateFile);
		// ---- The CUO world archive (docs/architecture/save-archive-format.md) ----
		SaveArchiveComposition.AddSaveArchive(services, savesRoot, gameBuild);

		extraRegistrations?.Invoke(services);

		// Fail fast on DI cycles at the composition root. ValidateOnBuild
		// catches constructor/implementation-type cycles before startup; the
		// factory wrapper catches factory-mediated re-entrant resolution that
		// static validation cannot see.
		DiCycleGuard.WrapFactoryDescriptors(
			services,
			exception => LogCompositionRootFailure(bepinExLogSource, logDirectory, legacyLogPath, exception));
		try
		{
			return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
		}
		catch (Exception ex)
		{
			LogCompositionRootFailure(bepinExLogSource, logDirectory, legacyLogPath, ex);
			throw;
		}
	}

	private static void LogCompositionRootFailure(
		ManualLogSource bepinExLogSource,
		string logDirectory,
		string? legacyLogPath,
		Exception exception)
	{
		var message = $"CUO composition root validation failed: {exception}";
		bepinExLogSource.LogError(message);

		try
		{
			using var provider = new RollingFileLoggerProvider(
				logDirectory,
				legacyLogPath,
				new MutableOptionsMonitor<LoggingOptions>(new LoggingOptions()));
			provider.CreateLogger(nameof(CuoBootstrap))
				.LogError(exception, "CUO composition root validation failed.");
			if (provider.IsEnabled)
			{
				return;
			}
		}
		catch
		{
			// Fall through to the direct append below.
		}

		// If a standalone rolling provider cannot take the file (for example a
		// factory cycle is detected after the DI provider already owns
		// latest.log), append the startup failure directly so the CUO log still
		// gets the diagnostic.
		try
		{
			Directory.CreateDirectory(logDirectory);
			var latestLog = Path.Combine(logDirectory, "latest.log");
			File.AppendAllText(
				latestLog,
				$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERR] [{nameof(CuoBootstrap)}] CUO composition root validation failed: {exception}{Environment.NewLine}");
		}
		catch
		{
			// Startup diagnostics must never mask the original build failure.
		}
	}
}
