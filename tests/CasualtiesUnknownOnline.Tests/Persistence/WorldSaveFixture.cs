using System;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// One save service wired over a throwaway world repository with a real kernel,
/// the fake character table, an in-memory world-fact source and a scripted
/// session — the save suites (cut, continue and world facts) share it so none
/// hand-rolls its own composition.
/// </summary>
internal sealed class WorldSaveFixture : IDisposable
{
	private WorldSaveFixture(
		WorldSaveService service,
		SaveTestRepository repository,
		ItemKernelAuthority kernel,
		FakeCharacterDataControl characters,
		FakeSessionControl session,
		FakeWorldFactSource worldFacts,
		WorldRestoreAudit? audit,
		IRestoredWorldItemSource? items,
		MutableOptionsMonitor<SaveOptions> options,
		ILoggerFactory loggerFactory)
	{
		Service = service;
		Repository = repository;
		Kernel = kernel;
		Characters = characters;
		Session = session;
		WorldFacts = worldFacts;
		Audit = audit;
		Items = items;
		Options = options;
		LoggerFactory = loggerFactory;
	}

	internal WorldSaveService Service { get; }

	internal SaveTestRepository Repository { get; }

	internal ItemKernelAuthority Kernel { get; }

	internal FakeCharacterDataControl Characters { get; }

	internal FakeSessionControl Session { get; }

	/// <summary>The world-fact tables this fixture's service reads and rewrites.</summary>
	internal FakeWorldFactSource WorldFacts { get; }

	/// <summary>The restore account this fixture's service reports its live-world halves to, when the suite supplied one.</summary>
	internal WorldRestoreAudit? Audit { get; }

	/// <summary>
	/// The item domain's restored-set port the save layer drives, when the suite supplied
	/// one: it records every cancellation the service asked for, which is how the PORT
	/// WIRING (the supersession, layer-end and abandon call sites) is pinned rather than
	/// read.
	/// </summary>
	internal IRestoredWorldItemSource? Items { get; }

	/// <summary>
	/// The save policy the service reads at each decision. A suite that pins the
	/// interval autosave or the retention writes through this monitor, which is the
	/// same hot-reload path the BepInEx config file drives in production.
	/// </summary>
	internal MutableOptionsMonitor<SaveOptions> Options { get; }

	/// <summary>
	/// The factory every logger of this fixture's service comes from — a
	/// <see cref="RecordingLoggerFactory"/> when the suite supplied one, so a test can
	/// assert on the lines the save layer writes (the empty factory otherwise).
	/// </summary>
	internal ILoggerFactory LoggerFactory { get; }

	/// <summary>
	/// The same factory, typed for the suites that supplied a recorder — the
	/// observability suites assert on what production types WROTE. A suite that did
	/// not supply one fails here by name rather than silently asserting on nothing.
	/// </summary>
	internal RecordingLoggerFactory Recorder => Assert.IsType<RecordingLoggerFactory>(LoggerFactory);

	/// <summary>The cut writer the service drives — the suites pin its row shapes directly (the service itself owns the trigger, not the payload).</summary>
	internal WorldCutWriter Writer => Service.Writer!;

	/// <summary>The world this fixture's service owns (the run `TryBeginRun` created).</summary>
	internal string WorldId => Service.CurrentWorldId;

	internal static WorldSaveFixture Create(
		string label,
		bool ipDirect = false,
		string displayName = "Host",
		ulong hostId = 1001UL,
		SaveTestRepository? repository = null,
		FakeNativeWorldFacts? nativeWorldFacts = null,
		IWorldCutTransientProbe? transients = null,
		IRestoredWorldEntitySource? worldEntities = null,
		WorldRestoreAudit? audit = null,
		IRestoredWorldItemSource? items = null,
		ILoggerFactory? loggerFactory = null,
		Func<DateTime>? utcNow = null,
		SaveOptions? options = null)
	{
		repository ??= SaveTestRepository.Create(label);
		loggerFactory ??= NullLoggerFactory.Instance;
		var kernel = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
		var characters = new FakeCharacterDataControl();
		var session = new FakeSessionControl { LocalSteamId = hostId, HostSteamId = hostId };
		var transport = new FakeTransportIdentity { LocalPeerId = hostId, LocalDisplayName = displayName, IsIpDirect = ipDirect };
		var worldFacts = new FakeWorldFactSource();
		var monitor = new MutableOptionsMonitor<SaveOptions>(options ?? new SaveOptions());
		var service = new WorldSaveService(
			repository.Repository,
			session,
			characters,
			kernel,
			transport,
			new WorldSnapshotEncoder(NullLogger<WorldSnapshotEncoder>.Instance),
			worldFacts,
			loggerFactory,
			loggerFactory.CreateLogger<WorldSaveService>(),
			gameBuild: "test",
			utcNow: utcNow,
			nativeWorldFacts: nativeWorldFacts,
			transients: transients,
			worldEntities: worldEntities,
			audit: audit,
			items: items,
			options: monitor);

		return new WorldSaveFixture(service, repository, kernel, characters, session, worldFacts, audit, items, monitor, loggerFactory);
	}

	/// <summary>A second service over the SAME world repository with a fresh kernel — a host restart. The restore account and the item port are the caller's to supply: a restart is a NEW service, and sharing a stand-in silently would hide that (the suites that pin them compose through <see cref="Create"/>).</summary>
	internal WorldSaveFixture Restart(string label, bool ipDirect = false, string displayName = "Host") =>
		Create(label, ipDirect, displayName, hostId: Session.LocalSteamId, repository: Repository, loggerFactory: LoggerFactory);

	public void Dispose() => Service.Dispose();
}
