using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Session;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Application.Kernel;

namespace CasualtiesUnknownOnline.Tests.World;

public class WorldRunStateProjectionTests
{
	private const ulong HostId = 1001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void HostPublishWorldParams_CommitsKernelRunState()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var world = host.Services.GetRequiredService<IWorldControl>();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var parameters = new WorldStartParams
		{
			RandomState = [1, 2, 3],
			BiomeOverride = 1,
			BiomeDepth = 2,
			TotalTraveled = 10,
			RunSettings = new Dictionary<string, object>
			{
				["speed"] = 1.5f,
			},
		};

		world.PublishWorldParams(parameters);

		var run = authority.QueryRun();
		Assert.NotNull(run);
		Assert.Equal(1ul, run!.RunId);
		Assert.Equal([1, 2, 3], run.RandomState);
		Assert.Equal(2, run.BiomeDepth);
		Assert.Equal(1.5f, Assert.Single(run.RunSettings!).FloatValue);
		Assert.Same(parameters, world.WorldParams);
	}

	[Fact]
	public void GuestHandshake_ReceivesRunBaselineViaKernelCheckpoint()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, 2001];

		var hostWorld = host.Services.GetRequiredService<IWorldControl>();
		hostWorld.PublishWorldParams(new WorldStartParams
		{
			RandomState = [4, 5, 6],
			TotalTraveled = 9,
		});

		guest.Steam.FireLobbyEntered(LobbyId);

		var guestWorld = guest.Services.GetRequiredService<IWorldControl>();
		Assert.NotNull(guestWorld.WorldParams);
		Assert.Equal([4, 5, 6], guestWorld.WorldParams!.RandomState);
		Assert.Equal(9, guestWorld.WorldParams.TotalTraveled);
	}

	[Fact]
	public void GuestAppliesRunBatch_ProjectsWorldParams()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		var guestWorld = guest.Services.GetRequiredService<IWorldControl>();
		var run = WorldRunStateMapper.ToRunState(1, new WorldStartParams
		{
			RandomState = [4, 5, 6],
			BiomeOverride = 0,
			TotalTraveled = 7,
		});

		Assert.True(hostAuthority.TryStartRun(HostId, run, out var batch, out _));
		Assert.True(guestAuthority.Apply(batch!).Success);

		var projected = guestWorld.WorldParams;
		Assert.NotNull(projected);
		Assert.Equal([4, 5, 6], projected!.RandomState);
		Assert.Equal(7, projected.TotalTraveled);
	}

	/// <summary>
	/// The rarity multipliers are world-generation inputs, so they have to survive
	/// every hop a guest receives the baseline through: the adapter's capture, the
	/// kernel's run state, the wire checkpoint and back to the adapter's projection.
	/// A guest that generated with the game's fresh 1f would build a different layer
	/// than the host's.
	/// </summary>
	[Fact]
	public void RunBaseline_CarriesTheRarityMultipliersThroughTheWireAndBack()
	{
		var run = WorldRunStateMapper.ToRunState(7, new WorldStartParams
		{
			RandomState = [1, 2, 3],
			LootRarityMultiplier = 2.5f,
			TrapRarityMultiplier = 3.5f,
		}, layerIndex: 4);

		var wire = KernelDomainWireMapper.ToWireRun(run);
		Assert.Equal(2.5f, wire.LootRarityMultiplier);
		Assert.Equal(3.5f, wire.TrapRarityMultiplier);

		var roundTripped = KernelDomainWireMapper.FromWireRun(wire);
		Assert.Equal(2.5f, roundTripped.LootRarityMultiplier);
		Assert.Equal(3.5f, roundTripped.TrapRarityMultiplier);

		var projected = WorldRunStateMapper.ToWorldStartParams(roundTripped);
		Assert.Equal(2.5f, projected.LootRarityMultiplier);
		Assert.Equal(3.5f, projected.TrapRarityMultiplier);
	}

	/// <summary>
	/// A sender that predates the field carries none, which means the game's own
	/// starting value — the value that sender generated with — and never a guessed
	/// non-neutral one.
	/// </summary>
	[Fact]
	public void RunBaseline_WithoutTheWireMultipliers_DegradesToTheGameDefault()
	{
		var restored = KernelDomainWireMapper.FromWireRun(new WireRunState { RunId = 9, RandomState = [5] });

		Assert.Equal(RunRarityMultipliers.Neutral, restored.LootRarityMultiplier);
		Assert.Equal(RunRarityMultipliers.Neutral, restored.TrapRarityMultiplier);
	}
}
