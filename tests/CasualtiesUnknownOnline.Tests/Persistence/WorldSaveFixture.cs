using System;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

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
		FakeWorldFactSource worldFacts)
	{
		Service = service;
		Repository = repository;
		Kernel = kernel;
		Characters = characters;
		Session = session;
		WorldFacts = worldFacts;
	}

	internal WorldSaveService Service { get; }

	internal SaveTestRepository Repository { get; }

	internal ItemKernelAuthority Kernel { get; }

	internal FakeCharacterDataControl Characters { get; }

	internal FakeSessionControl Session { get; }

	/// <summary>The world-fact tables this fixture's service reads and rewrites.</summary>
	internal FakeWorldFactSource WorldFacts { get; }

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
		IRestoredWorldEntitySource? worldEntities = null)
	{
		repository ??= SaveTestRepository.Create(label);
		var kernel = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
		var characters = new FakeCharacterDataControl();
		var session = new FakeSessionControl { LocalSteamId = hostId, HostSteamId = hostId };
		var transport = new FakeTransportIdentity { LocalPeerId = hostId, LocalDisplayName = displayName, IsIpDirect = ipDirect };
		var worldFacts = new FakeWorldFactSource();
		var service = new WorldSaveService(
			repository.Repository,
			session,
			characters,
			kernel,
			transport,
			new WorldSnapshotEncoder(NullLogger<WorldSnapshotEncoder>.Instance),
			worldFacts,
			NullLoggerFactory.Instance,
			NullLogger<WorldSaveService>.Instance,
			gameBuild: "test",
			nativeWorldFacts: nativeWorldFacts,
			transients: transients,
			worldEntities: worldEntities);

		return new WorldSaveFixture(service, repository, kernel, characters, session, worldFacts);
	}

	/// <summary>A second service over the SAME world repository with a fresh kernel — a host restart.</summary>
	internal WorldSaveFixture Restart(string label, bool ipDirect = false, string displayName = "Host") =>
		Create(label, ipDirect, displayName, hostId: Session.LocalSteamId, repository: Repository);

	public void Dispose() => Service.Dispose();
}
