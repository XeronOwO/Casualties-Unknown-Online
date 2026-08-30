using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class FluidKernelReadProjectionTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static readonly RunEpoch Epoch = new(1);
	private static readonly ActorId HostActor = new(1001);

	private static readonly FluidRegionState RegionA = new(0, 0, 5, 1, 100);
	private static readonly FluidRegionState RegionB = new(1, 2, 3, 2, 200);

	private static CommittedBatch Batch(ulong revision, params GameEvent[] events) =>
		new(
			new OperationId(revision),
			revision,
			HostActor,
			AuthorityKind.HostOnly,
			Epoch,
			[],
			events);

	private static (TestNode Host, TestNode Guest) CreateSession()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);
		return (host, guest);
	}

	[Fact]
	public void GuestCheckpointRestore_RebuildsRegionFacts()
	{
		var (_, guest) = CreateSession();
		var authority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		var projection = guest.Services.GetRequiredService<FluidKernelReadProjection>();
		var projected = 0;
		projection.RegionsProjected += regions => projected++;

		var checkpoint = new GameCheckpoint(
			Epoch,
			10,
			[],
			Fluids: new FluidStateTable([RegionA, RegionB]));

		authority.Restore(checkpoint);

		Assert.Equal(2, projection.Regions.Count);
		Assert.Contains(projection.Regions, r => r.ChunkX == 0 && r.ChunkY == 0 && r.TotalAmount == 5);
		Assert.Contains(projection.Regions, r => r.ChunkX == 1 && r.ChunkY == 2 && r.TotalAmount == 3);
		Assert.Equal(1, projected);
		Assert.Equal(2, guest.Services.GetRequiredService<WorldService>().FluidRegionFacts.Count);
	}

	[Fact]
	public void GuestBatchApplied_UpsertsAndReplacesRegionFacts()
	{
		var (_, guest) = CreateSession();
		var authority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		var projection = guest.Services.GetRequiredService<FluidKernelReadProjection>();
		var projected = 0;
		projection.RegionsProjected += _ => projected++;

		Assert.True(authority.Apply(Batch(1, new FluidRegionUpdatedEvent(RegionA))).Success);
		Assert.True(authority.Apply(Batch(2, new FluidRegionUpdatedEvent(RegionB))).Success);
		Assert.True(authority.Apply(Batch(3, new FluidRegionUpdatedEvent(RegionA with { TotalAmount = 9 }))).Success);

		Assert.Equal(2, projection.Regions.Count);
		Assert.Equal(9, projection.Regions.Single(r => r.ChunkX == 0 && r.ChunkY == 0).TotalAmount);
		Assert.Equal(3, projected);
	}

	[Fact]
	public void GuestBatchApplied_ResetClearsRegionFacts()
	{
		var (_, guest) = CreateSession();
		var authority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		var projection = guest.Services.GetRequiredService<FluidKernelReadProjection>();

		Assert.True(authority.Apply(Batch(1, new FluidRegionUpdatedEvent(RegionA))).Success);
		Assert.Single(projection.Regions);

		Assert.True(authority.Apply(Batch(2, new FluidsResetEvent())).Success);

		Assert.Empty(projection.Regions);
	}

	[Fact]
	public void HostRole_DoesNotProjectFluidRegions()
	{
		var (host, _) = CreateSession();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var projection = host.Services.GetRequiredService<FluidKernelReadProjection>();

		var checkpoint = new GameCheckpoint(
			Epoch,
			10,
			[],
			Fluids: new FluidStateTable([RegionA]));

		authority.Restore(checkpoint);

		Assert.Empty(projection.Regions);
	}
}
