using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The mod domain facade (Phase 4 Mod API). It composes the lifecycle,
/// command and state domains as internal collaborators and presents the stable
/// public control surfaces (<see cref="IModsControl"/>, <see cref="IModUiControl"/>,
/// <see cref="IModContentControl"/>, <see cref="ICuoService"/>) to the rest of
/// the runtime. The actual loaded-mod table, lifecycle pump, host-command
/// execution and mod-state persistence live in their own top-level classes so
/// this type stays a thin wiring point instead of a god object.
/// </summary>
public sealed class ModService : ICuoService, IModsControl, IModUiControl, IModContentControl
{
	private readonly ModCatalog _catalog;
	private readonly ModStateStore _stateStore;
	private readonly ModDataStore _dataStore;
	private readonly ModStatusStore _statusStore;
	private readonly ModCommandService _commands;
	private readonly ModLifecycle _lifecycle;
	private bool _disposed; // the container may dispose the same singleton once per registration (3.1 behaviour) — the ICuoService contract requires idempotent dispose

	public ModService(
		SessionService session,
		ModChannel channel,
		ModRegistry registry,
		ConsoleCommandRegistry consoleCommands,
		PacketSender sender,
		ITimeSource time,
		ILoggerFactory loggerFactory,
		ILogger<ModService> log,
		ModStateFileStore stateFile,
		RemoteVitalsService remoteVitals,
		RemoteInventoryService remoteInventory,
		IModEntitySpawner entitySpawner,
		IModItemSpawner itemSpawner,
		IModTilePlacer tilePlacer,
		IModStructurePlacer structurePlacer,
		IModNativeApiProvider nativeApiProvider)
	{
		_catalog = new ModCatalog();
		_stateStore = new ModStateStore(stateFile, log);
		_dataStore = new ModDataStore(log);
		_statusStore = new ModStatusStore(log);
		_commands = new ModCommandService(_catalog, session, sender, time, log);
		_lifecycle = new ModLifecycle(_catalog, _commands, consoleCommands, _stateStore, _dataStore, _statusStore, session, channel, registry, time, loggerFactory, log, remoteVitals, remoteInventory, entitySpawner, itemSpawner, tilePlacer, structurePlacer, nativeApiProvider);
	}

	public void Initialize()
	{
		// Host mod state is loaded before discovery so Bind sees the persisted
		// table (a mod can read its own state in Bind/Initialize). The in-memory
		// table is process-scoped like the file store; no lazy reload after a
		// session end — only a new process start may reload the disk copy.
		_stateStore.Load();

		_lifecycle.Initialize();
	}

	public void Start()
	{
	}

	public void Update() => _lifecycle.Update();

	public void Stop() => _lifecycle.Stop();

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_commands.FailAllPending("framework shutdown");
		_lifecycle.Dispose();
	}

	// ---- IModsControl ----

	public void FireModMessageReceived(ulong sender, ModMessageMsg msg) => _lifecycle.FireModMessageReceived(sender, msg);

	public void FireModCommandRequestReceived(ulong sender, ModCommandRequestMsg msg) => _commands.FireCommandRequestReceived(sender, msg);

	public void FireModCommandResultReceived(ulong sender, ModCommandResultMsg msg) => _commands.FireCommandResultReceived(sender, msg);

	public IReadOnlyList<ModManifest> CurrentModManifests => _lifecycle.CurrentModManifests;

	public bool IsDiscoveryComplete => _lifecycle.IsDiscoveryComplete;

	// ---- IModUiControl / IModContentControl ----

	public IReadOnlyList<ModUiWindow> Windows => _lifecycle.Windows;

	public IReadOnlyList<ModContentRegistration> Entries => _lifecycle.Entries;

	/// <summary>Test seam (InternalsVisibleTo): the loaded instances, in discovery order — the lifecycle tests assert on them.</summary>
	internal IReadOnlyList<ICuoMod> LoadedMods => _lifecycle.LoadedMods;
}
