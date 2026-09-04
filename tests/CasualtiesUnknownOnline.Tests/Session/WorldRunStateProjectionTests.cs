using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Session;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using System.Collections.Generic;

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
}
