using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Runtime.Steam;
using CasualtiesUnknownOnline.Runtime.Time;
using CasualtiesUnknownOnline.Tests.Fakes;
using CasualtiesUnknownOnline.Tests.Patching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The starting-supplies coordinator's live behaviour (S4.3): which bodies get a grant,
/// which get an account instead, and the three rules that keep it from doing harm — once
/// per body, never on top of a character restore, and never on the game's own first-layer
/// grant.
///
/// The Runtime half is REAL (a <see cref="WorldSaveService"/> over a throwaway world
/// repository, so <c>RestoredGeneration</c> comes from an actual continue rather than a
/// hand-set flag), and the engine half is a fake behind the adapter seam — which is exactly
/// why the seam was drawn there: a coordinator that touched <c>Utils.Create</c> or
/// <c>Body.transform</c> directly could never be constructed in this host at all, because
/// the CLR binds a method body's Unity InternalCall members when it JITs the method.
/// </summary>
[Trait("Category", "Integration")]
public class StartingSupplyCoordinatorTests
{
	[Fact]
	public void Update_AnOmittedPlayerInARestoredWorld_IsSupplied()
	{
		// The acceptance case of S4.3, end to end: the host continued an archive, the
		// archive carries no character for this player, and the run's own startingsupplies
		// setting is handed over — the game's first-layer grant does NOT cover a restored
		// generation, which is the whole point of the stage.
		using var fixture = new Fixture();
		fixture.RestoreWorld(Supplies(2));
		var before = fixture.Reports.Count;

		fixture.Coordinator().Update();

		var report = Assert.Single(fixture.Reports.Skip(before));
		Assert.Equal(StartingSupplyGrantReport.Disposition.Granted, report.Outcome);
		Assert.Equal("full", report.Setting);
		Assert.Equal(["lantern", "dogfood", "waterbottle", "trashbag"], report.Items);
		Assert.Empty(report.Unplaced);
		Assert.Equal(4, fixture.Placed.Count);
	}

	[Fact]
	public void Update_TheSameBodyIsJudgedOnce_HoweverManyFramesRun()
	{
		// The pump runs every frame and the world keeps the same body across a layer
		// descent (<c>RegenerateWorld</c> never reloads the scene, so the body survives),
		// which is what makes "once per body" correct rather than "once per frame" — the
		// failure mode is a backpack that refills itself.
		using var fixture = new Fixture();
		fixture.RestoreWorld(Supplies(2));
		var coordinator = fixture.Coordinator();

		coordinator.Update();
		coordinator.Update();
		coordinator.Update();

		Assert.Single(fixture.Reports);
		Assert.Equal(4, fixture.Placed.Count);
	}

	[Fact]
	public void Update_ANewBodyAfterTheOldOneIsGone_IsJudgedOnItsOwnEntry()
	{
		// A death puts a new body under the player: it is a new entry and must be judged
		// again (the old body's judgement says nothing about this one).
		using var fixture = new Fixture();
		fixture.RestoreWorld(Supplies(1));
		var coordinator = fixture.Coordinator();

		coordinator.Update();
		fixture.ReplaceBody();
		coordinator.Update();

		var reports = fixture.Reports.ToList();
		Assert.Equal(2, reports.Count);
		Assert.All(reports, report => Assert.Equal(StartingSupplyGrantReport.Disposition.Granted, report.Outcome));
		Assert.Equal(2, fixture.Placed.Count);
	}

	[Fact]
	public void Update_AQueuedCharacterRestore_CancelsTheGrant()
	{
		// The world HAS a character for this player: the restore's own first pass wipes the
		// body's slots a frame later, so a grant here would be created, announced and then
		// destroyed — and the player would be told about items they never kept.
		using var fixture = new Fixture();
		fixture.RestoreWorld(Supplies(2));
		fixture.Restore.Queue(new CharacterDataMsg(), ownRun: false);
		var coordinator = fixture.Coordinator();

		coordinator.Update();
		Assert.Empty(fixture.Reports);
		Assert.Empty(fixture.Placed);

		// The body stays JUDGED: the pump must not re-decide it every frame while the
		// restore's two passes are still running.
		coordinator.Update();
		Assert.Empty(fixture.Reports);
	}

	[Fact]
	public void Update_ARestoreThatLandsBeforeTheGrant_CancelsIt()
	{
		// The decision is taken at the GRANT moment, not sampled when the body appeared,
		// because a restore can land in between: the host hands a reconnecting guest its
		// character while the guest is already in the world.
		using var fixture = new Fixture();
		fixture.RestoreWorld(Supplies(2));
		fixture.Restore.Queue(new CharacterDataMsg(), ownRun: false);

		fixture.Coordinator().Update();

		Assert.Empty(fixture.Reports);
		Assert.Empty(fixture.Placed);
	}

	[Fact]
	public void Update_AFreshRun_ReportsAlreadyOwnedInsteadOfGranting()
	{
		// The run's first layer of a run started HERE: the game's own grant hands the
		// supplies out inside generation (WorldGeneration.cs:1891), so CUO must not add a
		// second set. The player still gets an account — that is the line, not a duplicate.
		using var fixture = new Fixture();
		fixture.BeginFreshRun(Supplies(2), totalTraveled: 0);

		fixture.Coordinator().Update();

		var report = Assert.Single(fixture.Reports);
		Assert.Equal(StartingSupplyGrantReport.Disposition.AlreadyOwned, report.Outcome);
		Assert.Empty(fixture.Placed);
	}

	[Fact]
	public void Update_AMidRunJoinInAFreshRun_IsSupplied()
	{
		// A player joining a running world whose first layer is already behind it: the
		// game's first-layer test fails, so nothing has supplied them and they are a new
		// player here — the other half of the acceptance row.
		using var fixture = new Fixture();
		fixture.BeginFreshRun(Supplies(1), totalTraveled: 4200);

		fixture.Coordinator().Update();

		var report = Assert.Single(fixture.Reports);
		Assert.Equal(StartingSupplyGrantReport.Disposition.Granted, report.Outcome);
		Assert.Equal("light", report.Setting);
		Assert.Equal(["emergencylight"], report.Items);
	}

	[Fact]
	public void Update_ARunWithNoSupplies_ReportsDisabledAndGrantsNothing()
	{
		using var fixture = new Fixture();
		fixture.RestoreWorld(Supplies(0));

		fixture.Coordinator().Update();

		var report = Assert.Single(fixture.Reports);
		Assert.Equal(StartingSupplyGrantReport.Disposition.Disabled, report.Outcome);
		Assert.Equal("none", report.Setting);
		Assert.Empty(fixture.Placed);
	}

	[Fact]
	public void Update_WithNoPublishedBaseline_JudgesNothingAndRetries()
	{
		// The one reason that is not a verdict: nothing describes this generation yet, so
		// the body stays unjudged. A later frame — the generation boundary publishes the
		// baseline — must still be able to supply it. Treating this as "no supplies" would
		// silently deny the grace window of a fast world entry.
		using var fixture = new Fixture();
		var coordinator = fixture.Coordinator();

		fixture.World.WorldParams = null;
		coordinator.Update();
		Assert.Empty(fixture.Reports);
		Assert.Empty(fixture.Placed);

		fixture.RestoreWorld(Supplies(2));
		coordinator.Update();

		var report = Assert.Single(fixture.Reports);
		Assert.Equal(StartingSupplyGrantReport.Disposition.Granted, report.Outcome);
		Assert.Equal(4, fixture.Placed.Count);
	}

	[Fact]
	public void Update_WithNoLocalBody_DoesNothing()
	{
		using var fixture = new Fixture();
		fixture.RestoreWorld(Supplies(2));
		fixture.DetachBody();

		fixture.Coordinator().Update();

		Assert.Empty(fixture.Reports);
	}

	[Fact]
	public void Update_AnItemTheGameCannotPlace_IsNamedInsteadOfClaimed()
	{
		// PickUpItem refuses an unusable slot SILENTLY (Body.cs:1390), so the item stays at
		// the body's feet. The account must say so: a player who cannot find a "given" item
		// must not have to read the line twice to learn it never landed.
		using var fixture = new Fixture();
		fixture.RestoreWorld(Supplies(2));
		fixture.RefuseSlot(4);

		fixture.Coordinator().Update();

		var report = Assert.Single(fixture.Reports);
		Assert.Equal(StartingSupplyGrantReport.Disposition.Granted, report.Outcome);
		Assert.False(report.Complete);
		Assert.Equal(["dogfood"], report.Unplaced);
		Assert.Contains("dogfood", report.Describe(), StringComparison.Ordinal);
	}

	[Fact]
	public void Update_AContentIdThatProducesNoItem_IsNamedInsteadOfClaimed()
	{
		using var fixture = new Fixture();
		fixture.RestoreWorld(Supplies(1));
		fixture.RefuseCreation("emergencylight");

		fixture.Coordinator().Update();

		var report = Assert.Single(fixture.Reports);
		Assert.Equal(StartingSupplyGrantReport.Disposition.Granted, report.Outcome);
		Assert.Empty(report.Items);
		Assert.Equal(["emergencylight"], report.Unplaced);
	}

	[Fact]
	public void Clear_ForgetsTheBodiesOfTheRunBeingLeft()
	{
		// RunSaveCoordinator.BeginRun clears the tracker when a run this client owns takes
		// over: the bodies it recorded belong to the world being left. Without it the rule
		// would be "supplied once per process" for a body a scene reload happened to reuse.
		using var fixture = new Fixture();
		fixture.RestoreWorld(Supplies(1));
		var coordinator = fixture.Coordinator();

		coordinator.Update();
		Assert.Single(fixture.Reports);

		coordinator.Clear();
		coordinator.Update();

		Assert.Equal(2, fixture.Reports.Count);
	}

	// ---- fixture: real Runtime half, proxied Unity half ----

	/// <summary>
	/// The production composition root (<see cref="TestNode"/>) with a real world
	/// repository, so the whole Runtime half of the decision is the real one: the world
	/// control whose published baseline it reads, the save service whose
	/// restored-generation flag comes from an ACTUAL continue, and the audit the console
	/// subscribes to. The Unity half is the two emitted proxies
	/// (<see cref="Proxies"/>) — which is exactly why the seam was drawn there.
	/// </summary>
	private sealed class Fixture : IDisposable
	{
		internal const ulong HostId = 1001UL;

		private readonly ServiceProvider _services;
		private readonly FakeSteamService _steam;
		private readonly FakeTransport _transport;
		private readonly FakeStartingSupplyBehaviour _client;

		/// <summary>
		/// The production composition root with the same substitutions <see cref="TestNode"/>
		/// makes (fake transport/Steam/clock, no rolling file sink) PLUS a real saves root:
		/// that last one is what the save service needs to open an archive, and it is the
		/// reason this fixture builds its own provider rather than borrowing a node.
		/// </summary>
		internal Fixture()
		{
			_steam = new FakeSteamService(HostId);
			_transport = new FakeTransport(HostId, new FakeNetwork());
			var clock = new FakeClock();
			_services = CuoBootstrap.BuildServiceProvider(
				new ManualLogSource("test"),
				Path.Combine(Path.GetTempPath(), "cuo-supplies-tests", Guid.NewGuid().ToString("N"), "logs"),
				savesRoot: Path.Combine(Path.GetTempPath(), "cuo-supplies-tests", Guid.NewGuid().ToString("N"), "cuo", "saves"),
				gameBuild: "test",
				extraRegistrations: services =>
				{
					services.Replace(ServiceDescriptor.Singleton<INetworkTransport>(_transport));
					services.Replace(ServiceDescriptor.Singleton<ISteamService>(_steam));
					services.Replace(ServiceDescriptor.Singleton<ITimeSource>(clock));
					// Transport IDENTITY is its own seam and the save layer reads it (the world
					// folder's display name and the key space a character is written in). The
					// router's real identity asks Steam for the persona name, and Steamworks is
					// not loadable here — so the identity is a fake, and the router's own
					// Steam-backed path is never entered.
					services.Replace(ServiceDescriptor.Singleton<ITransportIdentity>(new FakeTransportIdentity
					{
						LocalPeerId = HostId,
						LocalDisplayName = "Host",
						IsIpDirect = false,
					}));
					// The real SteamService/SteamTransport stay in the graph but must never
					// initialize (they would load steam_api64): each Replace below swaps the
					// FIRST remaining ICuoService match — the Steam service, then the transport.
					services.Replace(ServiceDescriptor.Singleton(_ => (ICuoService)_steam));
					services.Replace(ServiceDescriptor.Singleton(_ => (ICuoService)_transport));
					TestLogging.RemoveFileSink(services);
				});
			foreach (var service in _services.GetServices<ICuoService>())
			{
				service.Initialize();
			}

			World = _services.GetRequiredService<IWorldControl>();
			Kernel = _services.GetRequiredService<ItemKernelAuthority>();
			Save = _services.GetRequiredService<IWorldSaveControl>();
			Publisher = _services.GetRequiredService<IStartingSupplyPublisher>();
			Restore = new LocalCharacterRestoreQueue();
			_services.GetRequiredService<IStartingSupplyControl>().Reported += report => Reports.Add(report);
			_client = new FakeStartingSupplyBehaviour(Placed);
			Body = new object();
			_client.LocalBody = Body;
			Behaviour = _client;
		}

		internal IWorldControl World { get; }

		internal ItemKernelAuthority Kernel { get; }

		internal IWorldSaveControl Save { get; }

		internal IStartingSupplyPublisher Publisher { get; }

		internal LocalCharacterRestoreQueue Restore { get; }

		internal object Body { get; private set; }

		internal IStartingSupplyBehaviour Behaviour { get; }

		/// <summary>What the fake body was asked to hold.</summary>
		internal List<(string ItemId, int Slot)> Placed { get; } = [];

		/// <summary>Every account the Runtime surface received (the console's own subscription, observed).</summary>
		internal List<StartingSupplyGrantReport> Reports { get; } = [];

		/// <summary>
		/// A REAL continue, through the production save service and a real archive on disk:
		/// the world is created, cut, and reopened — which is what sets the
		/// restored-generation flag and makes the Runtime project the restored run baseline
		/// into <see cref="IWorldControl.WorldParams"/>.
		///
		/// The archive's run carries the run's <c>startingsupplies</c> setting and NO
		/// character: the player continuing it is the one S4.3 is about.
		/// </summary>
		internal void RestoreWorld(Dictionary<string, object> settings)
		{
			Assert.True(Save.TryBeginRun(isTutorial: false), "the fixture's world could not be created");
			Assert.True(Kernel.TryStartRun(HostId, Run(settings), out _, out _));
			Assert.True(Save.TryRequestCut(WorldCutReason.MenuReturn, out var refusal), refusal);
			Assert.NotNull(Save.TryCaptureArmedCut(null, frame: 0));
			Assert.True(Save.TryContinue(out var outcome), outcome.Summary);
			Assert.True(Save.RestoredGeneration);
		}

		/// <summary>A fresh run's generation: the host clicked start, so nothing belongs to an archive.</summary>
		internal void BeginFreshRun(Dictionary<string, object> settings, int totalTraveled)
		{
			Assert.True(Save.TryBeginRun(isTutorial: false));
			var run = Run(settings);
			Assert.True(Kernel.TryStartRun(HostId, run, out _, out _));
			Assert.False(Save.RestoredGeneration);
			var restored = World.WorldParams!;
			World.WorldParams = new WorldStartParams
			{
				RandomState = restored.RandomState,
				BiomeOverride = restored.BiomeOverride,
				BiomeDepth = restored.BiomeDepth,
				TotalTraveled = totalTraveled,
				RunSettings = settings,
			};
		}

		/// <summary>A death: the player is under a different body, and that body is judged on its own entry.</summary>
		internal void ReplaceBody()
		{
			Body = new object();
			_client.LocalBody = Body;
		}

		/// <summary>No body at all (a menu scene, a scene swap in flight).</summary>
		internal void DetachBody() => _client.LocalBody = null;

		internal void RefuseSlot(int slot) => _client.RefusedSlot = slot;

		internal void RefuseCreation(string itemId) => _client.RefusedCreation = itemId;

		internal CoordinatorHandle Coordinator() => new(
			CoordinatorType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single().Invoke(
			[
				_services.GetRequiredService<ISessionControl>(),
				World,
				Restore,
				Save,
				Publisher,
				Behaviour,
				ReflectionLogger(),
			]));

		public void Dispose() => _services.Dispose();
	}

	/// <summary>
	/// The engine half of the grant, faked: a body handle, an item handle and a placement
	/// answer. It implements <see cref="IStartingSupplyBehaviour"/> — the abstraction the
	/// adapter's Unity implementation also implements — which is what lets this suite drive
	/// the whole decision without a Unity type in sight.
	///
	/// The body handle is a plain object, exactly as the interface promises, and
	/// <see cref="TryPlace"/> answers for that same handle: a coordinator that used anything
	/// but the handle's identity for its once-per-body rule would fail the tests that swap a
	/// body.
	/// </summary>
	private sealed class FakeStartingSupplyBehaviour(List<(string ItemId, int Slot)> placed) : IStartingSupplyBehaviour
	{
		public object? LocalBody { get; set; }

		public int RefusedSlot { get; set; } = -1;

		public string? RefusedCreation { get; set; }

		public object? Create(string itemId) => itemId == RefusedCreation ? null : itemId;

		public bool TryPlace(object body, object item, int slot)
		{
			if (!ReferenceEquals(body, LocalBody) || slot == RefusedSlot)
			{
				return false;
			}

			placed.Add(((string)item, slot));
			return true;
		}
	}

	/// <summary>
	/// The run baseline the fixture cuts: the archive's own run, carrying the run's
	/// <c>startingsupplies</c> setting — the value a restore replays and the decision reads.
	/// </summary>
	private static RunState Run(Dictionary<string, object> settings) =>
		new(
			RunId: 77,
			RandomState: [1, 2, 3, 4],
			BiomeOverride: 0,
			BiomeDepth: 0,
			TotalTraveled: 0,
			LoadedRun: false,
			RunSettings: [.. settings.Select(pair => new RunSetting(pair.Key, RunSettingKind.Int, IntValue: (int)pair.Value))],
			LayerIndex: 0);

	/// <summary>Build the coordinator for a fixture's fakes, through the adapter's own constructor.</summary>
	private static CoordinatorHandle Coordinator(Fixture fixture) => fixture.Coordinator();

	/// <summary>
	/// An <c>ILogger&lt;StartingSupplyCoordinator&gt;</c> for the reflection-built instance. The
	/// type is only known by name here, so the logger is closed over it by reflection — the
	/// non-generic <c>NullLogger</c> cannot be passed to that constructor at all.
	/// </summary>
	private static object ReflectionLogger()
	{
		var closed = typeof(NullLogger<>).MakeGenericType(CoordinatorType);
		return closed.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) is { } property
			? property.GetValue(null)!
			: closed.GetField("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) is { } field
				? field.GetValue(null)!
				: throw new InvalidOperationException($"{closed.FullName} exposes no Instance member.");
	}

	private static readonly Type CoordinatorType = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.StartingSupplyCoordinator",
		throwOnError: true)!;

	/// <summary>
	/// The reflection handle to the coordinator. <c>Update</c> and <c>Clear</c> are
	/// parameterless instance methods, so nothing on this side of the fence needs the
	/// adapter's own types.
	/// </summary>
	private sealed class CoordinatorHandle(object instance)
	{
		private readonly MethodInfo _update = Method("Update");
		private readonly MethodInfo _clear = Method("Clear");

		internal void Update() => _update.Invoke(instance, null);

		internal void Clear() => _clear.Invoke(instance, null);

		private static MethodInfo Method(string name) =>
			CoordinatorType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException($"StartingSupplyCoordinator.{name} not found.");
	}

	/// <summary>The run's <c>startingsupplies</c> dictionary, keyed by the policy's own setting name so a rename cannot quietly pass.</summary>
	private static Dictionary<string, object> Supplies(int value) =>
		new() { [SettingName] = value };

	private static string SettingName => (string)GameAssemblyHost.Adapter
		.GetType("CasualtiesUnknownOnline.GameAdapter.Character.StartingSupplyPolicy", throwOnError: true)!
		.GetField("SettingName", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
		.GetValue(null)!;
}



