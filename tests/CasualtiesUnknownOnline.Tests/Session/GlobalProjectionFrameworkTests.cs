using System;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Regression surface for the global projection framework inventory. The typed
/// domains that hold real rebuildable read models must all appear in
/// <see cref="ProjectionHealthCoordinator.Snapshot"/>, and a failed projection
/// must be contained and recovered by calling the real registered rebuild path.
/// </summary>
[Trait("Category", "Integration")]
public class GlobalProjectionFrameworkTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong OtherGuestId = 2002;
	private const ulong LobbyId = 9001;

	[Fact]
	public void Snapshot_ContainsRunCarryAndRemotePresentationDomains()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			// Resolve the composition roots that own the newly registered domains.
			_ = host.Services.GetRequiredService<IWorldControl>();
			_ = host.Services.GetRequiredService<IPlayerInteractionControl>();
			_ = host.Services.GetRequiredService<RemoteCharacterPresentationStore>();
			_ = host.Services.GetRequiredService<ModStatusProjectionReadModel>();

			var coordinator = host.Services.GetRequiredService<ProjectionHealthCoordinator>();
			var domains = coordinator.Snapshot().Select(i => i.Domain).ToHashSet();

			Assert.Contains("run", domains);
			Assert.Contains("players-carry", domains);
			Assert.Contains("remote-character-presentation", domains);
			Assert.Contains("mod-status", domains);
		}
	}

	[Fact]
	public void Run_RegisteredDomain_RecoversFromInjectedFailure()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var world = host.Services.GetRequiredService<IWorldControl>();
			world.PublishWorldParams(new WorldStartParams
			{
				RandomState = [1, 2, 3],
				TotalTraveled = 10,
			});
			// Simulate a lost/detached read model; the rebuild must restore it
			// from the kernel RunState, not merely clear the dirty bit.
			world.WorldParams = null;

			var coordinator = host.Services.GetRequiredService<ProjectionHealthCoordinator>();
			coordinator.Run("run", 42, static () => throw new InvalidOperationException("run projection boom"));

			var dirty = Assert.Single(coordinator.Snapshot(), i => i.Domain == "run");
			Assert.True(dirty.Dirty);
			Assert.Equal(42ul, dirty.LastFailedRevision);
			Assert.Equal("run projection boom", dirty.LastError);

			coordinator.Pump();

			var recovered = Assert.Single(coordinator.Snapshot(), i => i.Domain == "run");
			Assert.False(recovered.Dirty);
			Assert.False(recovered.Degraded);
			Assert.Null(recovered.LastError);
			Assert.NotNull(world.WorldParams);
			Assert.Equal([1, 2, 3], world.WorldParams!.RandomState);
			Assert.Equal(10, world.WorldParams.TotalTraveled);
		}
	}

	[Fact]
	public void PlayersCarry_RegisteredDomain_RecoversFromInjectedFailure()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var status = host.Services.GetRequiredService<PlayerKernelStatusProjection>();
			status.Ensure(HostId);
			status.Ensure(GuestId);
			var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
			Assert.True(authority.TrySetPlayerCarry(HostId, HostId, GuestId, out _, out _));

			var interaction = host.Services.GetRequiredService<IPlayerInteractionControl>();
			Assert.True(interaction.TryGetCarrier(GuestId, out var committedCarrier));
			Assert.Equal(HostId, committedCarrier);

			// Drop the local carry mirror as if the projection had been lost;
			// the registered rebuild must restore it from the kernel table.
			var carryField = typeof(PlayerInteractionService).GetField("_carry", BindingFlags.Instance | BindingFlags.NonPublic)
				?? throw new InvalidOperationException("PlayerInteractionService._carry not found.");
			((PlayerCarryService)carryField.GetValue(interaction)!).ResetCarryMirror();
			Assert.False(interaction.TryGetCarrier(GuestId, out _));

			var coordinator = host.Services.GetRequiredService<ProjectionHealthCoordinator>();
			coordinator.Run("players-carry", 7, static () => throw new InvalidOperationException("carry projection boom"));

			var dirty = Assert.Single(coordinator.Snapshot(), i => i.Domain == "players-carry");
			Assert.True(dirty.Dirty);
			Assert.Equal(7ul, dirty.LastFailedRevision);

			coordinator.Pump();

			var recovered = Assert.Single(coordinator.Snapshot(), i => i.Domain == "players-carry");
			Assert.False(recovered.Dirty);
			Assert.False(recovered.Degraded);
			Assert.Null(recovered.LastError);
			Assert.True(interaction.TryGetCarrier(GuestId, out var carrier));
			Assert.Equal(HostId, carrier);
		}
	}

	[Fact]
	public void RemotePresentation_RegisteredDomain_RecoversFromInjectedFailureAndKeepsCache()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var store = host.Services.GetRequiredService<RemoteCharacterPresentationStore>();
			var characters = host.Services.GetRequiredService<ICharacterDataControl>();
			var sourceData = new CharacterDataMsg
			{
				Health = new CharacterHealthMsg
				{
					Alive = true,
					Conscious = true,
					HeartRate = 80f,
					RespiratoryRate = 40f,
				},
				Limbs = [new CharacterLimbMsg { Index = 0, IsHead = true }],
			};
			characters.FireCharacterDataReceived(GuestId, sourceData);

			Assert.True(store.TryGet(GuestId, out var before));
			Assert.NotNull(before.Medical);
			// The cache must deep-copy the authority-owned wire snapshot, not
			// retain an alias that later in-place source mutations can pollute.
			Assert.False(ReferenceEquals(before.Source, sourceData));
			sourceData.Health!.HeartRate = 999f;
			Assert.Equal(80f, before.Medical!.HeartRate, 3);
			Assert.Equal(80f, before.Source.Health!.HeartRate, 3);

			var coordinator = host.Services.GetRequiredService<ProjectionHealthCoordinator>();
			coordinator.Run("remote-character-presentation", 11, static () => throw new InvalidOperationException("presentation boom"));

			var dirty = Assert.Single(coordinator.Snapshot(), i => i.Domain == "remote-character-presentation");
			Assert.True(dirty.Dirty);
			Assert.Equal(11ul, dirty.LastFailedRevision);

			ulong? rebuiltRevision = null;
			store.PresentationsRebuilt += revision => rebuiltRevision = revision;
			coordinator.Pump();

			Assert.True(store.TryGet(GuestId, out var after));
			Assert.NotNull(after.Medical);
			Assert.Equal(80f, after.Medical!.HeartRate, 3);
			Assert.NotNull(rebuiltRevision);
			var recovered = Assert.Single(coordinator.Snapshot(), i => i.Domain == "remote-character-presentation");
			Assert.False(recovered.Dirty);
			Assert.False(recovered.Degraded);
			Assert.Null(recovered.LastError);
		}
	}

	[Fact]
	public void RemotePresentation_FullEmptySnapshotClearsPreviousInventory()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var store = host.Services.GetRequiredService<RemoteCharacterPresentationStore>();
			var characters = host.Services.GetRequiredService<ICharacterDataControl>();

			characters.FireCharacterDataReceived(GuestId, new CharacterDataMsg
			{
				Items = [new CharacterItemMsg { InstanceId = 9, ItemId = "medkit", SlotIndex = 0 }],
			});
			Assert.True(store.TryGet(GuestId, out var withItems));
			Assert.Single(withItems.Inventory!.Items);

			characters.FireCharacterDataReceived(GuestId, new CharacterDataMsg
			{
				Items = [],
			});

			Assert.True(store.TryGet(GuestId, out var cleared));
			Assert.Empty(cleared.Inventory!.Items);
		}
	}

	[Fact]
	public void RemotePresentation_GuestResolvesHostThirdPartyAndSessionLifecycle()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var store = guest.Services.GetRequiredService<RemoteCharacterPresentationStore>();
			var characters = guest.Services.GetRequiredService<ICharacterDataControl>();

			characters.FireHostCharacterDataReceived(new CharacterDataMsg
			{
				Health = new CharacterHealthMsg { Alive = true, Conscious = true },
			});
			characters.FireCharacterDataReceived(HostId, new CharacterDataMsg
			{
				OwnerSteamId = OtherGuestId,
				Health = new CharacterHealthMsg { Alive = false, Conscious = false },
			});

			Assert.True(store.TryGet(HostId, out _));
			Assert.True(store.TryGet(OtherGuestId, out _));
			Assert.Equal(2, store.Count);

			((ISessionControl)guest.Session).FireRemoteSceneChanged(HostId, false);
			Assert.False(store.TryGet(HostId, out _));
			Assert.True(store.TryGet(OtherGuestId, out _));

			((ISessionControl)guest.Session).EndSession();
			Assert.Equal(0, store.Count);
		}
	}

	[Fact]
	public void ModStatus_RegisteredDomain_RecoversFromInjectedFailureAndKeepsReadModel()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var store = host.Services.GetRequiredService<ModService>().StatusStore;
			Assert.True(store.TryDeclare(
				"test.mod",
				"body.proj",
				ModStatusScope.Body,
				ModDataScope.LocalOnly,
				1,
				ModStatusProjectionKind.BodyFormula));
			Assert.True(store.TrySetBodyValue("test.mod", "body.proj", HostId, [1, 2, 3]));

			var readModel = host.Services.GetRequiredService<ModStatusProjectionReadModel>();
			Assert.Contains(readModel.ProjectionSnapshots, s => s.StatusId == "body.proj" && s.PlayerSteamId == HostId);

			var coordinator = host.Services.GetRequiredService<ProjectionHealthCoordinator>();
			coordinator.Run("mod-status", 42, static () => throw new InvalidOperationException("mod-status boom"));

			var dirty = Assert.Single(coordinator.Snapshot(), i => i.Domain == "mod-status");
			Assert.True(dirty.Dirty);
			Assert.Equal(42ul, dirty.LastFailedRevision);
			Assert.Equal("mod-status boom", dirty.LastError);

			coordinator.Pump();

			var recovered = Assert.Single(coordinator.Snapshot(), i => i.Domain == "mod-status");
			Assert.False(recovered.Dirty);
			Assert.False(recovered.Degraded);
			Assert.Null(recovered.LastError);
			Assert.Contains(readModel.ProjectionSnapshots, s => s.StatusId == "body.proj" && s.PlayerSteamId == HostId);
		}
	}

	[Fact]
	public void ModStatus_GuestHidesHostAuthoritativeProjectionSnapshots()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var store = guest.Services.GetRequiredService<ModService>().StatusStore;
			Assert.True(store.TryDeclare(
				"test.mod",
				"host.secret",
				ModStatusScope.Body,
				ModDataScope.HostAuthoritative,
				1,
				ModStatusProjectionKind.BodyFormula));
			Assert.True(store.TrySetBodyValue("test.mod", "host.secret", GuestId, [1]));

			var readModel = guest.Services.GetRequiredService<ModStatusProjectionReadModel>();
			Assert.DoesNotContain(readModel.ProjectionSnapshots, s => s.StatusId == "host.secret");
			Assert.DoesNotContain(readModel.StatusPresences, p => p.StatusId == "host.secret");
		}
	}

	[Fact]
	public void ModStatus_ReadModelRefreshesWhenLateSteamIdArrives()
	{
		var steam = new FakeSteamService(0);
		var node = TestNode.Create(0, new FakeNetwork(), steam, pumpFirstFrame: true);
		using (node)
		{
			var store = node.Services.GetRequiredService<ModService>().StatusStore;
			Assert.True(store.TryDeclare(
				"test.mod",
				"late.id",
				ModStatusScope.Body,
				ModDataScope.LocalOnly,
				1,
				ModStatusProjectionKind.BodyFormula));
			Assert.True(store.TrySetBodyValue("test.mod", "late.id", 0, [1]));

			var readModel = node.Services.GetRequiredService<ModStatusProjectionReadModel>();
			Assert.Contains(readModel.ProjectionSnapshots, s => s.StatusId == "late.id" && s.PlayerSteamId == 0);

			steam.LocalSteamId = HostId;
			node.Update();

			Assert.DoesNotContain(readModel.ProjectionSnapshots, s => s.PlayerSteamId == 0);
			Assert.True(store.TrySetBodyValue("test.mod", "late.id", HostId, [2]));
			Assert.Contains(readModel.ProjectionSnapshots, s => s.StatusId == "late.id" && s.PlayerSteamId == HostId);
		}
	}
}
