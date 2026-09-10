using System;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// One save service wired over a throwaway world repository with a real kernel,
/// the fake character table and a scripted session — the two save suites (cut
/// and continue) share it so neither hand-rolls its own composition.
/// </summary>
internal sealed class WorldSaveFixture : IDisposable
{
	private WorldSaveFixture(
		WorldSaveService service,
		SaveTestRepository repository,
		ItemKernelAuthority kernel,
		FakeCharacterDataControl characters,
		FakeSessionControl session)
	{
		Service = service;
		Repository = repository;
		Kernel = kernel;
		Characters = characters;
		Session = session;
	}

	internal WorldSaveService Service { get; }

	internal SaveTestRepository Repository { get; }

	internal ItemKernelAuthority Kernel { get; }

	internal FakeCharacterDataControl Characters { get; }

	internal FakeSessionControl Session { get; }

	/// <summary>The world this fixture's service owns (the run `TryBeginRun` created).</summary>
	internal string WorldId => Service.CurrentWorldId;

	internal static WorldSaveFixture Create(string label, bool ipDirect = false, string displayName = "Host", ulong hostId = 1001UL, SaveTestRepository? repository = null)
	{
		repository ??= SaveTestRepository.Create(label);
		var kernel = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
		var characters = new FakeCharacterDataControl();
		var session = new FakeSessionControl { LocalSteamId = hostId, HostSteamId = hostId };
		var transport = new FakeTransportIdentity { LocalPeerId = hostId, LocalDisplayName = displayName, IsIpDirect = ipDirect };
		var service = new WorldSaveService(
			repository.Repository,
			session,
			characters,
			kernel,
			transport,
			new WorldSnapshotEncoder(NullLogger<WorldSnapshotEncoder>.Instance),
			NullLoggerFactory.Instance,
			NullLogger<WorldSaveService>.Instance,
			gameBuild: "test");

		return new WorldSaveFixture(service, repository, kernel, characters, session);
	}

	/// <summary>A second service over the SAME world repository with a fresh kernel — a host restart.</summary>
	internal WorldSaveFixture Restart(string label, bool ipDirect = false, string displayName = "Host") =>
		Create(label, ipDirect, displayName, hostId: Session.LocalSteamId, repository: Repository);

	public void Dispose() => Service.Dispose();
}
